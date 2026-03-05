using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Hitscan weapon with lag-compensated server-side hit registration.
/// Uses Mirror's LagCompensator for Valve-model rewind.
/// Client sends shoot direction → server rewinds, raycasts, validates.
/// </summary>
public class Gun : NetworkBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────
    [SerializeField] protected float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] protected Transform gunTransform;
    [SerializeField] private float maxRange = 300f;
    [SerializeField] private ParticleSystem hitEffectPrefab;
    [SerializeField] private BulletTracer tracerPrefab;

    [Header("Lag Compensation")]
    [SerializeField] private LayerMask hitLayerMask = -1;
    [SerializeField] private float hitTolerance = 0.5f;

    // ── SyncVar ───────────────────────────────────────────────────
    [SyncVar(hook = nameof(OnLastFireTimeChanged))]
    protected float LastFireTime = -Mathf.Infinity;

    // ── Client state ──────────────────────────────────────────────
    private bool _isCharged;
    private Camera _playerCamera;
    private float _lastReportedProgress = -1f;

    // ── Pooling ───────────────────────────────────────────────────
    private static NetworkPool<BulletTracer> _tracerPool;
    // Hit effect pooling deferred (requires PooledParticle setup per prefab)

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
    // CLIENT API
    // ════════════════════════════════════════════════════════════════

    public bool Charge()
    {
        if ((float)NetworkTime.time - LastFireTime < cooldown) return false;
        _isCharged = true;
        return true;
    }

    public void CancelCharge()
    {
        _isCharged = false;
    }

    [Client]
    public bool Fire()
    {
        if (!_isCharged) return false;

        Vector3 origin = gunTransform.position;
        Vector3 direction = GetShootDirection();

        // Send to server for authoritative validation
        CmdFireHitscan(origin, direction);

        // Spawn local tracer immediately (responsive feel)
        SpawnLocalTracer(origin, direction);

        _isCharged = false;
        return true;
    }

    /// <summary>Calculate aim direction from screen center raycast.</summary>
    private Vector3 GetShootDirection()
    {
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
    // SERVER — HITSCAN WITH LAG COMPENSATION
    // ════════════════════════════════════════════════════════════════

    [Command]
    protected virtual void CmdFireHitscan(Vector3 origin, Vector3 direction)
    {
        // ── Validate cooldown ───────────────────────────────────────
        if ((float)NetworkTime.time - LastFireTime < cooldown * 0.9f) return;
        LastFireTime = (float)NetworkTime.time;

        // ── Validate origin (anti-cheat: must be near gun) ──────────
        float originDist = Vector3.Distance(origin, gunTransform.position);
        if (originDist > 3f)
        {
            origin = gunTransform.position; // snap to server gun position
        }
        direction = direction.normalized;

        // ── Lag compensation: estimate client's perceived time ──────
        double estimatedTime = LagCompensation.EstimateTime(
            NetworkTime.localTime,
            connectionToClient.rtt,
            NetworkClient.bufferTime);

        // ── Collect all enemy LagCompensators ───────────────────────
        _lagCompensators.Clear();
        FindAllLagCompensators(_lagCompensators);

        // ── Rewind & Raycast ────────────────────────────────────────
        // We use the BoundsCheck approach: sample each target's historical position,
        // then do a single server-side raycast from corrected origin.
        // For more precision, we temporarily move colliders to historical positions.

        // Store original positions to restore
        var originalPositions = new (LagCompensator comp, Vector3 pos, Quaternion rot)[_lagCompensators.Count];
        int targetCount = 0;

        for (int i = 0; i < _lagCompensators.Count; i++)
        {
            var lc = _lagCompensators[i];
            // Skip self
            if (lc.netIdentity == netIdentity) continue;

            // Skip teammates
            var targetTeam = lc.GetComponent<PlayerTeam>();
            var ownerTeam = GetComponent<PlayerTeam>();
            if (targetTeam && ownerTeam &&
                ownerTeam.CurrentTeam != Team.None &&
                targetTeam.CurrentTeam == ownerTeam.CurrentTeam)
                continue;

            // Sample historical position
            if (lc.Sample(connectionToClient, out Capture3D sample))
            {
                originalPositions[targetCount] = (lc, lc.transform.position, lc.transform.rotation);
                targetCount++;

                // Temporarily move to historical position
                lc.transform.position = sample.position;
            }
        }

        // ── Perform server-side raycast ─────────────────────────────
        bool didHit = Physics.Raycast(origin, direction, out RaycastHit hitInfo, maxRange, hitLayerMask);

        // ── Restore all positions ───────────────────────────────────
        for (int i = 0; i < targetCount; i++)
        {
            var (comp, pos, rot) = originalPositions[i];
            comp.transform.position = pos;
            comp.transform.rotation = rot;
        }

        // ── Process hit ─────────────────────────────────────────────
        if (didHit)
        {
            Vector3 hitPoint = hitInfo.point;

            // Try to get damageable
            var damageable = hitInfo.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                OnServerHit(damageable, hitInfo, damage);
                RpcOnHit(hitPoint, origin, direction);
                return;
            }

            // Hit non-damageable (wall, etc.)
            RpcOnMiss(hitPoint, origin, direction);
        }
        else
        {
            // Complete miss
            Vector3 endPoint = origin + direction * maxRange;
            RpcOnMiss(endPoint, origin, direction);
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
        // Use NetworkServer.spawned to iterate active players
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

    /// <summary>Hit confirmed — show impact effect + tracer on all clients.</summary>
    [ClientRpc]
    private void RpcOnHit(Vector3 hitPoint, Vector3 origin, Vector3 direction)
    {
        // Spawn hit effect
        if (hitEffectPrefab)
        {
            // TODO: pool hit effects
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-direction));
        }

        // Spawn tracer (non-owner clients; owner already spawned local tracer)
        if (!isLocalPlayer)
        {
            SpawnLocalTracer(origin, direction, hitPoint);
        }
    }

    /// <summary>Miss — show tracer on all non-owner clients.</summary>
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

    /// <summary>Spawn a local-only tracer visual (pooled, no networking).</summary>
    private void SpawnLocalTracer(Vector3 origin, Vector3 direction, Vector3 endpoint = default)
    {
        if (_tracerPool == null) return;

        if (endpoint == default)
        {
            // Calculate endpoint for local prediction
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
        var wait = new WaitForSeconds(0.017f); // ~60Hz UI update
        while (true)
        {
            float timeSinceLast = (float)NetworkTime.time - LastFireTime;
            float cooldownProgress = Mathf.Clamp01(timeSinceLast / cooldown);

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
