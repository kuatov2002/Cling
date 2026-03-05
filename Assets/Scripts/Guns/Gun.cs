using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// CS-style automatic hitscan weapon with:
///   - Tick-based fire rate (auto-fire while holding Fire1)
///   - Spread system: base + movement + sustained fire inaccuracy
///   - First shot accuracy (standing still, not recently fired)
///   - Deterministic spread (same seed on client + server, zero sync needed)
///   - Lag-compensated server-side hit registration
///   - Zero GC in hot paths
/// </summary>
public class Gun : NetworkBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────
    [Header("Base")]
    [SerializeField] protected float damage = 20f;
    [SerializeField] protected Transform gunTransform;
    [SerializeField] private float maxRange = 300f;
    [SerializeField] private ParticleSystem hitEffectPrefab;
    [SerializeField] private BulletTracer tracerPrefab;

    [Header("Fire Rate")]
    [Tooltip("Ticks between shots. 6 @ 64Hz ≈ 600 RPM (AK-47 style).")]
    [SerializeField] private int fireRateTicks = 6;

    [Header("Spread / Accuracy")]
    [Tooltip("Minimum spread in degrees when standing still.")]
    [SerializeField] private float baseInaccuracy = 0.4f;
    [Tooltip("Max additional spread from movement (degrees at full speed).")]
    [SerializeField] private float moveInaccuracyMax = 4.0f;
    [Tooltip("Degrees added per consecutive shot.")]
    [SerializeField] private float fireInaccuracyPerShot = 0.6f;
    [Tooltip("Maximum accumulated sustained fire spread (degrees).")]
    [SerializeField] private float fireInaccuracyMax = 7.0f;
    [Tooltip("Degrees per second of spread recovery when not firing.")]
    [SerializeField] private float fireInaccuracyDecayRate = 8.0f;
    [Tooltip("Seconds standing still before first-shot-accurate (0° spread).")]
    [SerializeField] private float firstShotThreshold = 0.35f;

    [Header("Lag Compensation")]
    [SerializeField] private LayerMask hitLayerMask = -1;
    [SerializeField] private float hitTolerance = 0.5f;

    // ── SyncVar ───────────────────────────────────────────────────
    [SyncVar(hook = nameof(OnLastFireTimeChanged))]
    protected float LastFireTime = -Mathf.Infinity;

    // ── Spread state (deterministic, computed independently on client + server) ──
    private float _sustainedFireInaccuracy;
    private int _lastFireTick;

    // ── Client state ──────────────────────────────────────────────
    private Camera _playerCamera;
    private float _lastReportedProgress = -1f;

    // ── Pooling ───────────────────────────────────────────────────
    private static NetworkPool<BulletTracer> _tracerPool;

    // ── Pre-allocated for lag compensation (zero GC) ──────────────
    private static readonly List<LagCompensator> _lagCompensators = new List<LagCompensator>(8);
    private static readonly Collider[] _raycastResults = new Collider[16];

    // ════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════════

    private void Start()
    {
        if (isLocalPlayer)
            StartCoroutine(CooldownUIRoutine());
    }

    public override void OnStartLocalPlayer()
    {
        _playerCamera = Camera.main;

        // Initialize tracer pool (once, shared across all guns)
        if (_tracerPool == null && tracerPrefab != null)
        {
            _tracerPool = new NetworkPool<BulletTracer>(tracerPrefab, 16);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // CLIENT API — TICK-DRIVEN AUTO-FIRE
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by PlayerController every tick while fireHeld == true.
    /// Returns true if a shot was actually fired this tick.
    /// </summary>
    [Client]
    public bool TryFireTick(int currentTick, float inputSpeedSqr, float moveSpeedMax)
    {
        // Check fire rate
        int ticksSinceLastFire = currentTick - _lastFireTick;
        if (ticksSinceLastFire < fireRateTicks) return false;

        // Calculate spread
        float timeSinceLastShot = ticksSinceLastFire * NetworkTickManager.TickDuration;
        float spreadAngle = CalculateSpreadAngle(inputSpeedSqr, moveSpeedMax, timeSinceLastShot);

        // Get base direction from screen center
        Vector3 baseDirection = GetShootDirection();
        Vector3 origin = gunTransform.position;

        // Apply spread with deterministic seed
        uint seed = GenerateSeed(currentTick);
        Vector3 spreadDirection = ApplySpreadToDirection(baseDirection, spreadAngle, seed);

        // Send to server (server will independently calculate same spread)
        CmdFireAutomatic(origin, baseDirection, currentTick);

        // Spawn local tracer immediately (responsive feel)
        SpawnLocalTracer(origin, spreadDirection);

        // Update spread state — only on pure client.
        // On host, CmdFireAutomatic runs inline and already updated the state.
        if (!isServer)
        {
            float decayed = Mathf.Max(0f, _sustainedFireInaccuracy - fireInaccuracyDecayRate * timeSinceLastShot);
            _sustainedFireInaccuracy = Mathf.Min(decayed + fireInaccuracyPerShot, fireInaccuracyMax);
            _lastFireTick = currentTick;
        }

        return true;
    }

    /// <summary>
    /// Called every tick when fireHeld == false. Spread decays implicitly
    /// via timeSinceLastShot on next CalculateSpreadAngle call.
    /// </summary>
    public void TickNoFire(int currentTick)
    {
        // Decay is implicit — no state updates needed here
    }

    /// <summary>
    /// Get current spread angle in degrees (for crosshair UI visualization).
    /// </summary>
    public float GetCurrentSpreadAngle(float inputSpeedSqr, float moveSpeedMax, int currentTick)
    {
        int ticksSinceLast = currentTick - _lastFireTick;
        float timeSinceLastShot = ticksSinceLast * NetworkTickManager.TickDuration;
        return CalculateSpreadAngle(inputSpeedSqr, moveSpeedMax, timeSinceLastShot);
    }

    // ════════════════════════════════════════════════════════════════
    // SPREAD CALCULATION (pure, deterministic)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate total spread angle in degrees. Pure function — deterministic.
    /// Used by both client (prediction) and server (authority).
    /// </summary>
    private float CalculateSpreadAngle(float inputSpeedSqr, float moveSpeedMax, float timeSinceLastShot)
    {
        // 1. Decay sustained fire inaccuracy based on time not firing
        float decayedFireInaccuracy = _sustainedFireInaccuracy -
                                       (fireInaccuracyDecayRate * timeSinceLastShot);
        if (decayedFireInaccuracy < 0f) decayedFireInaccuracy = 0f;

        // 2. Movement inaccuracy: proportional to speed ratio squared
        float maxSpeedSqr = moveSpeedMax * moveSpeedMax;
        float speedRatio = (maxSpeedSqr > 0.0001f)
            ? Mathf.Clamp01(inputSpeedSqr / maxSpeedSqr)
            : 0f;
        float moveInaccuracy = moveInaccuracyMax * speedRatio;

        // 3. First Shot Accuracy: standing still + enough time since last shot + no residual spray
        bool isFirstShotAccurate = (inputSpeedSqr < 0.01f) &&
                                    (timeSinceLastShot >= firstShotThreshold) &&
                                    (decayedFireInaccuracy < 0.01f);

        if (isFirstShotAccurate)
            return 0f; // perfect accuracy

        // 4. Total spread = base + movement + sustained fire
        return baseInaccuracy + moveInaccuracy + decayedFireInaccuracy;
    }

    /// <summary>
    /// Apply spread to a direction using a uniform cone distribution.
    /// Uses deterministic hash-based RNG — zero GC, no System.Random.
    /// </summary>
    private static Vector3 ApplySpreadToDirection(Vector3 baseDirection, float spreadDegrees, uint seed)
    {
        if (spreadDegrees < 0.001f) return baseDirection.normalized;

        // Hash-based deterministic random (xorshift32)
        uint hash = seed;
        hash ^= hash << 13;
        hash ^= hash >> 17;
        hash ^= hash << 5;
        float rand1 = (hash & 0xFFFF) / 65535f; // 0..1

        hash ^= hash << 13;
        hash ^= hash >> 17;
        hash ^= hash << 5;
        float rand2 = (hash & 0xFFFF) / 65535f; // 0..1

        // Uniform distribution within cone
        float halfAngleRad = spreadDegrees * 0.5f * Mathf.Deg2Rad;
        float angle = rand1 * halfAngleRad;
        float rotation = rand2 * 2f * Mathf.PI;

        // Build perpendicular frame
        Vector3 dir = baseDirection.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(Vector3.forward, dir);
        right.Normalize();
        Vector3 up = Vector3.Cross(dir, right);

        // Apply deviation
        float sinAngle = Mathf.Sin(angle);
        Vector3 spread = dir * Mathf.Cos(angle) +
                         right * (sinAngle * Mathf.Cos(rotation)) +
                         up * (sinAngle * Mathf.Sin(rotation));

        return spread.normalized;
    }

    /// <summary>Deterministic seed from netId + tick. Same on client and server.</summary>
    private uint GenerateSeed(int tick)
    {
        return (uint)((netId * 1000003) ^ (tick * 7919));
    }

    /// <summary>Calculate aim direction from screen center raycast.</summary>
    private Vector3 GetShootDirection()
    {
        if (_playerCamera == null) _playerCamera = Camera.main;
        if (_playerCamera == null) return gunTransform.forward;

        Ray ray = _playerCamera.ScreenPointToRay(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        Vector3 targetPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, maxRange))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.origin + ray.direction * maxRange;
        }

        return (targetPoint - gunTransform.position).normalized;
    }

    // ════════════════════════════════════════════════════════════════
    // SERVER — HITSCAN WITH LAG COMPENSATION + SPREAD
    // ════════════════════════════════════════════════════════════════

    [Command]
    protected virtual void CmdFireAutomatic(Vector3 origin, Vector3 baseDirection, int clientTick)
    {
        // ── Validate fire rate ────────────────────────────────────────
        int ticksSinceLastFire = clientTick - _lastFireTick;
        if (ticksSinceLastFire < fireRateTicks - 1) return; // -1 tick tolerance for jitter

        // ── Validate origin (anti-cheat: must be near gun) ────────────
        float originDist = Vector3.Distance(origin, gunTransform.position);
        if (originDist > 3f)
        {
            origin = gunTransform.position;
        }
        baseDirection = baseDirection.normalized;

        // ── Server independently calculates spread ─────────────────────
        // Use input-based speed: get from the player's last processed input
        var playerMovement = GetComponent<PlayerMovement>();
        float inputSpeedSqr = 0f;
        float moveSpeedMax = 5f;
        if (playerMovement != null)
        {
            inputSpeedSqr = playerMovement.LastInputSpeedSqr;
            moveSpeedMax = playerMovement.MaxMoveSpeed;
        }

        float timeSinceLastShot = ticksSinceLastFire * NetworkTickManager.TickDuration;
        float spreadAngle = CalculateSpreadAngle(inputSpeedSqr, moveSpeedMax, timeSinceLastShot);

        uint seed = GenerateSeed(clientTick);
        Vector3 spreadDirection = ApplySpreadToDirection(baseDirection, spreadAngle, seed);

        // ── Update server spread state ─────────────────────────────────
        float decayed = Mathf.Max(0f, _sustainedFireInaccuracy - fireInaccuracyDecayRate * timeSinceLastShot);
        _sustainedFireInaccuracy = Mathf.Min(decayed + fireInaccuracyPerShot, fireInaccuracyMax);
        _lastFireTick = clientTick;
        LastFireTime = (float)NetworkTime.time;

        // ── Lag compensation (skip for host — no latency to compensate) ──
        bool needsLagComp = connectionToClient != null &&
                            !(connectionToClient is LocalConnectionToClient);
        int targetCount = 0;
        (LagCompensator comp, Vector3 pos, Quaternion rot)[] originalPositions = null;

        if (needsLagComp)
        {
            _lagCompensators.Clear();
            FindAllLagCompensators(_lagCompensators);

            originalPositions = new (LagCompensator, Vector3, Quaternion)[_lagCompensators.Count];

            for (int i = 0; i < _lagCompensators.Count; i++)
            {
                var lc = _lagCompensators[i];
                if (lc.netIdentity == netIdentity) continue;

                // Skip teammates — no need to rewind friendly positions
                var targetTeam = lc.GetComponent<PlayerTeam>();
                var ownerTeam = GetComponent<PlayerTeam>();
                if (targetTeam && ownerTeam &&
                    ownerTeam.CurrentTeam != Team.None &&
                    targetTeam.CurrentTeam == ownerTeam.CurrentTeam)
                    continue;

                if (lc.Sample(connectionToClient, out Capture3D sample))
                {
                    originalPositions[targetCount] = (lc, lc.transform.position, lc.transform.rotation);
                    targetCount++;
                    lc.transform.position = sample.position;
                }
            }
        }

        // ── Perform server-side raycast with spread direction ─────────
        bool didHit = Physics.Raycast(origin, spreadDirection, out RaycastHit hitInfo, maxRange, hitLayerMask);

        // ── Restore all rewound positions ─────────────────────────────
        if (originalPositions != null)
        {
            for (int i = 0; i < targetCount; i++)
            {
                var (comp, pos, rot) = originalPositions[i];
                comp.transform.position = pos;
                comp.transform.rotation = rot;
            }
        }

        // ── Process hit ─────────────────────────────────────────────
        if (didHit)
        {
            Vector3 hitPoint = hitInfo.point;

            // Self-hit guard (gun origin outside own collider edge case)
            var hitIdentity = hitInfo.collider.GetComponentInParent<NetworkIdentity>();
            if (hitIdentity != null && hitIdentity == netIdentity)
            {
                RpcOnMiss(hitPoint, origin, spreadDirection);
                return;
            }

            // Friendly fire check — block damage to same-team players
            var hitTeam = hitInfo.collider.GetComponentInParent<PlayerTeam>();
            var myTeam = GetComponent<PlayerTeam>();
            if (hitTeam != null && myTeam != null &&
                myTeam.CurrentTeam != Team.None &&
                hitTeam.CurrentTeam == myTeam.CurrentTeam)
            {
                // Friendly fire — treat as miss
                RpcOnMiss(hitPoint, origin, spreadDirection);
                return;
            }

            var damageable = hitInfo.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                OnServerHit(damageable, hitInfo, damage);
                RpcOnHit(hitPoint, origin, spreadDirection);
                return;
            }
            RpcOnMiss(hitPoint, origin, spreadDirection);
        }
        else
        {
            Vector3 endPoint = origin + spreadDirection * maxRange;
            RpcOnMiss(endPoint, origin, spreadDirection);
        }
    }

    /// <summary>
    /// Called on server when hitscan hits a valid target.
    /// Override in character guns for special behavior (heal, double damage, etc.).
    /// </summary>
    [Server]
    protected virtual void OnServerHit(IDamageable target, RaycastHit hitInfo, float hitDamage)
    {
        if (target is PlayerHealth playerHealth)
        {
            playerHealth.TakeDamageFrom(hitDamage, netId);
        }
        else
        {
            target.TakeDamage(hitDamage);
        }
    }

    /// <summary>Collect all active LagCompensators in the scene (zero-alloc reusable list).</summary>
    private void FindAllLagCompensators(List<LagCompensator> results)
    {
        foreach (var kvp in NetworkServer.spawned)
        {
            if (kvp.Value == null) continue;
            var lc = kvp.Value.GetComponent<LagCompensator>();
            if (lc != null)
                results.Add(lc);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // CLIENT RPCS — VISUAL EFFECTS
    // ════════════════════════════════════════════════════════════════

    [ClientRpc]
    private void RpcOnHit(Vector3 hitPoint, Vector3 origin, Vector3 direction)
    {
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-direction));
        }

        if (!isLocalPlayer)
        {
            SpawnLocalTracer(origin, direction, hitPoint);
        }
    }

    [ClientRpc]
    private void RpcOnMiss(Vector3 endPoint, Vector3 origin, Vector3 direction)
    {
        if (!isLocalPlayer)
        {
            SpawnLocalTracer(origin, direction, endPoint);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // TRACER SPAWNING
    // ════════════════════════════════════════════════════════════════

    private void SpawnLocalTracer(Vector3 origin, Vector3 direction, Vector3 endpoint = default)
    {
        if (_tracerPool == null) return;

        if (endpoint == default)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange))
                endpoint = hit.point;
            else
                endpoint = origin + direction * maxRange;
        }

        BulletTracer tracer = _tracerPool.Get();
        tracer.Fire(origin, endpoint, t => _tracerPool.Release(t));
    }

    // ════════════════════════════════════════════════════════════════
    // COOLDOWN UI
    // ════════════════════════════════════════════════════════════════

    private System.Collections.IEnumerator CooldownUIRoutine()
    {
        var wait = new WaitForSeconds(0.017f);
        float fireRateSeconds = fireRateTicks * NetworkTickManager.TickDuration;
        while (true)
        {
            float timeSinceLast = (float)NetworkTime.time - LastFireTime;
            float cooldownProgress = Mathf.Clamp01(timeSinceLast / fireRateSeconds);

            if (Mathf.Abs(cooldownProgress - _lastReportedProgress) > 0.01f)
            {
                UIManager.Instance?.UpdateGunCooldown(cooldownProgress);
                _lastReportedProgress = cooldownProgress;
            }

            yield return wait;
        }
    }

    private void OnLastFireTimeChanged(float oldVal, float newVal)
    {
        _lastReportedProgress = -1f;
    }
}
