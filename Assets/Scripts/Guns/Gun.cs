using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// CS-style automatic hitscan weapon with a two-layer spread model matching
/// the architecture used in CS2, Valorant, and R6:
///
///   LAYER 1 — Deterministic recoil pattern
///     A fixed, per-weapon Vector2 array of angular offsets (degrees).
///     Advances with each shot (ShotIndex). Learnable and counterstrafable:
///     moving the crosshair down compensates the upward kick automatically
///     because baseDirection already encodes the player's mouse input.
///     Resets after patternResetTime seconds without firing.
///
///   LAYER 2 — Random inaccuracy cone
///     Small Gaussian cone applied ON TOP of the pattern result.
///     Driven by movement speed and sustained-fire heat. Cannot be
///     eliminated by skill — only by standing still and waiting.
///     Significantly smaller than before because the pattern now
///     owns the "spray expansion" feeling.
///
///   Additional changes vs original:
///     - Crouching reduces both base inaccuracy and the movement penalty.
///     - First-shot-accurate requires ShotIndex == 0 (pattern hasn't started).
///     - SpreadState carries ShotIndex so client + server stay deterministic.
///     - CmdFireAutomatic receives isCrouching from the local player.
///     - All spread logic remains pure/static — zero side-effects.
/// </summary>
public class Gun : NetworkBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────
    [Header("Base")]
    [SerializeField] protected float damage = 20f;
    [SerializeField] protected Transform gunTransform;
    [SerializeField] private float maxRange = 150f;
    [SerializeField] private ParticleSystem hitEffectPrefab;
    [SerializeField] private BulletTracer tracerPrefab;

    [Header("Fire Rate")]
    [Tooltip("Ticks between shots. 6 @ 64Hz ≈ 600 RPM (AK-47 style).")]
    [SerializeField] private int fireRateTicks = 3;

    // ── LAYER 1: Recoil Pattern ────────────────────────────────────
    [Header("Recoil Pattern  (Layer 1 — Deterministic)")]
    [Tooltip(
        "Per-shot angular kick in degrees. X = horizontal (+ right), Y = vertical (+ up).\n" +
        "Each element is a cumulative offset from shot 0 — i.e. shot N fires toward\n" +
        "pattern[N], not pattern[N-1]+delta. This matches how CS2 stores its patterns.\n\n" +
        "Once the array is exhausted, the last entry is held (no wrapping).\n" +
        "Pattern resets to index 0 after patternResetTime seconds without firing.")]
    [SerializeField] private Vector2[] recoilPattern =
    {
        //  X (horiz)   Y (vert)   shot
        new Vector2( 0.0f,  1.5f), // 1  — first shot barely rises (first-shot-accurate if standing)
        new Vector2( 0.5f,  3.5f), // 2
        new Vector2( 0.8f,  5.5f), // 3
        new Vector2( 1.2f,  7.0f), // 4
        new Vector2( 0.5f,  8.5f), // 5  — starts pulling left
        new Vector2(-0.5f,  9.5f), // 6
        new Vector2(-1.5f, 10.0f), // 7
        new Vector2(-2.5f, 10.5f), // 8
        new Vector2(-2.0f, 11.0f), // 9
        new Vector2(-1.0f, 11.5f), // 10 — crosses back right
        new Vector2( 0.5f, 12.0f), // 11
        new Vector2( 2.0f, 12.5f), // 12
        new Vector2( 2.5f, 13.0f), // 13
        new Vector2( 1.5f, 13.5f), // 14
        new Vector2( 0.5f, 14.0f), // 15
        new Vector2(-0.5f, 14.5f), // 16
        new Vector2(-1.5f, 14.5f), // 17 — plateau: vertical stops rising
        new Vector2(-1.0f, 14.5f), // 18
        new Vector2( 0.5f, 14.5f), // 19
        new Vector2( 1.5f, 14.5f), // 20
    };

    [Tooltip("Global scale applied to every entry in the recoil pattern. " +
             "1 = default. Reduce for SMGs/pistols. Increase for LMGs.")]
    [SerializeField] private float patternScale = 1f;

    [Tooltip("Seconds without firing before the spray pattern resets to shot 0. " +
             "CS2 uses ~0.4 s for most rifles.")]
    [SerializeField] private float patternResetTime = 0.4f;

    // ── LAYER 2: Random Inaccuracy ─────────────────────────────────
    [Header("Random Inaccuracy  (Layer 2 — Noise)")]
    [Tooltip("Base inaccuracy half-angle (degrees) when standing and cooled-down. " +
             "Much smaller than before because the pattern owns the spray shape.")]
    [SerializeField] private float baseInaccuracy = 0.5f;

    [Tooltip("Max additional spread from movement at full sprint (degrees).")]
    [SerializeField] private float moveInaccuracyMax = 8f;

    [Tooltip("Max movement inaccuracy while CROUCHING at full crouch-walk speed.")]
    [SerializeField] private float crouchMoveInaccuracyMax = 2f;

    [Tooltip("Multiplier on base + sustained inaccuracy while crouching. " +
             "0.6 means 40% reduction — matches CS2 crouch accuracy bonus.")]
    [SerializeField] [Range(0f, 1f)] private float crouchInaccuracyMultiplier = 0.6f;

    [Tooltip("Degrees of random spread added per shot (sustained fire heat).")]
    [SerializeField] private float fireInaccuracyPerShot = 1.5f;

    [Tooltip("Max accumulated sustained-fire random spread (degrees). " +
             "Smaller than before — pattern already widens the effective spray.")]
    [SerializeField] private float fireInaccuracyMax = 8f;

    [Tooltip("Half-life of random spread recovery in seconds.")]
    [SerializeField] private float fireInaccuracyHalfLife = 0.12f;

    [Tooltip("Seconds of stillness + no sustained fire + ShotIndex == 0 " +
             "required before the first shot is perfectly accurate (0° spread, 0° pattern).")]
    [SerializeField] private float firstShotThreshold = 0.3f;

    [Header("Lag Compensation")]
    [SerializeField] private LayerMask hitLayerMask = -1;
    [SerializeField] private float hitTolerance = 0.5f;

    // ── SyncVar ───────────────────────────────────────────────────
    [SyncVar(hook = nameof(OnLastFireTimeChanged))]
    protected float LastFireTime = -Mathf.Infinity;

    [SyncVar] private float _damageMultiplier = 1f;

    public float DamageMultiplier
    {
        get => _damageMultiplier;
        set { if (isServer) _damageMultiplier = Mathf.Max(0f, value); }
    }

    // ══════════════════════════════════════════════════════════════
    // SPREAD STATE
    //
    // Deterministic — computed independently on client + server from
    // the same inputs. Zero sync required beyond LastFireTime.
    // ShotIndex is the critical addition: it tracks which pattern
    // entry fires next, so both sides advance the pattern identically.
    // ══════════════════════════════════════════════════════════════

    private readonly struct SpreadState
    {
        /// <summary>
        /// Accumulated random inaccuracy from sustained fire (degrees).
        /// Decays exponentially when not firing. Range: [0, fireInaccuracyMax].
        /// </summary>
        public readonly float SustainedInaccuracy;

        /// <summary>Tick on which the last shot was fired.</summary>
        public readonly int LastFireTick;

        /// <summary>
        /// Current position in the recoil pattern (0 = first shot ever / after reset).
        /// Monotonically increasing per burst; resets after patternResetTime.
        /// Clamped to pattern.Length-1 inside ApplyRecoilPattern — no wrapping.
        /// </summary>
        public readonly int ShotIndex;

        public SpreadState(float sustainedInaccuracy, int lastFireTick, int shotIndex)
        {
            SustainedInaccuracy = sustainedInaccuracy;
            LastFireTick        = lastFireTick;
            ShotIndex           = shotIndex;
        }

        public static readonly SpreadState Default = new SpreadState(0f, int.MinValue / 2, 0);
    }

    // Client and server maintain their own SpreadState independently.
    // On host (isLocalPlayer && isServer) both paths share this field.
    private SpreadState _spreadState = SpreadState.Default;

    // ── Client state ──────────────────────────────────────────────
    private Camera _playerCamera;
    private float _lastReportedProgress = -1f;

    // ── Pooling ───────────────────────────────────────────────────
    private static NetworkPool<BulletTracer> _tracerPool;

    // ── Pre-allocated for lag compensation (zero GC) ──────────────
    private static readonly List<LagCompensator> _lagCompensators = new List<LagCompensator>(8);
    private static readonly Collider[] _raycastResults = new Collider[16];
    private static (LagCompensator comp, Vector3 pos, Quaternion rot)[] _lagCompBuffer =
        new (LagCompensator, Vector3, Quaternion)[16];

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

        if (_tracerPool == null && tracerPrefab != null)
            _tracerPool = new NetworkPool<BulletTracer>(tracerPrefab, 16);
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        _tracerPool = null;
        StopAllCoroutines();
    }

    // ════════════════════════════════════════════════════════════════
    // CLIENT API — TICK-DRIVEN AUTO-FIRE
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by PlayerController every tick while fireHeld == true.
    /// Returns true if a shot was actually fired this tick.
    ///
    /// <paramref name="isCrouching"/> reduces both movement inaccuracy and
    /// base spread — pass PlayerMovement.IsCrouching here.
    /// </summary>
    [Client]
    public bool TryFireTick(int currentTick, float inputSpeedSqr, float moveSpeedMax, bool isCrouching = false)
    {
        int ticksSinceLastFire = currentTick - _spreadState.LastFireTick;
        if (ticksSinceLastFire < fireRateTicks) return false;

        float timeSinceLastShot = ticksSinceLastFire * NetworkTickManager.TickDuration;
        int   activeShotIndex   = ResolveActiveShotIndex(_spreadState, timeSinceLastShot, patternResetTime);

        Vector3 baseDirection = GetShootDirection();
        Vector3 origin        = gunTransform.position;
        uint    seed          = GenerateSeed(currentTick);

        Vector3 finalDirection = CalculateFinalShotDirection(
            baseDirection, _spreadState, activeShotIndex, currentTick,
            inputSpeedSqr, moveSpeedMax, isCrouching,
            baseInaccuracy, moveInaccuracyMax, crouchMoveInaccuracyMax, crouchInaccuracyMultiplier,
            fireInaccuracyHalfLife, firstShotThreshold,
            recoilPattern, patternScale,
            seed,
            out float decayedSustained);

        CmdFireAutomatic(origin, baseDirection, currentTick, isCrouching);
        SpawnLocalTracer(origin, finalDirection);

        // On host, CmdFireAutomatic ran inline and already advanced state — skip.
        if (!isServer)
            _spreadState = AdvanceSpreadState(
                _spreadState, currentTick, activeShotIndex,
                decayedSustained, fireInaccuracyPerShot, fireInaccuracyMax);

        return true;
    }

    /// <summary>No-op — decay is implicit via timeSinceLastShot on next call.</summary>
    public void TickNoFire(int currentTick) { }

    /// <summary>
    /// Returns the current random inaccuracy half-angle in degrees (for crosshair UI).
    /// Does NOT include the recoil pattern offset — the crosshair should show the
    /// inaccuracy cone independently, with a separate visual for pattern kick.
    /// </summary>
    public float GetCurrentSpreadAngle(float inputSpeedSqr, float moveSpeedMax, int currentTick, bool isCrouching = false)
    {
        int   ticksSinceLast    = currentTick - _spreadState.LastFireTick;
        float timeSinceLastShot = ticksSinceLast * NetworkTickManager.TickDuration;
        int   activeShotIndex   = ResolveActiveShotIndex(_spreadState, timeSinceLastShot, patternResetTime);

        return CalculateInaccuracyAngle(
            _spreadState, activeShotIndex, currentTick,
            inputSpeedSqr, moveSpeedMax, isCrouching,
            baseInaccuracy, moveInaccuracyMax, crouchMoveInaccuracyMax, crouchInaccuracyMultiplier,
            fireInaccuracyHalfLife, firstShotThreshold,
            out _);
    }

    // ════════════════════════════════════════════════════════════════
    // SPREAD — PURE, STATIC, DETERMINISTIC
    //
    // All methods are static and take every input explicitly.
    // Client and server call them with the same arguments and converge
    // on the same bullet direction without any additional sync.
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves the active shot index for this tick, applying the pattern reset if
    /// the player hasn't fired within patternResetTime.
    /// Pure — no state mutation.
    /// </summary>
    private static int ResolveActiveShotIndex(in SpreadState state, float timeSinceLastShot, float patternResetTime)
        => timeSinceLastShot >= patternResetTime ? 0 : state.ShotIndex;

    /// <summary>
    /// Computes the final bullet direction by composing both spread layers:
    ///
    ///   1. baseDirection + recoil pattern offset  →  patternDirection
    ///   2. patternDirection + random inaccuracy cone  →  finalDirection
    ///
    /// The separation is intentional: the pattern models the weapon's
    /// mechanical behaviour (learnable), while the cone models irreducible
    /// imprecision from movement and heat (irreducible by skill).
    ///
    /// For the first-shot-accurate case (standing still, cooled down, ShotIndex 0)
    /// both layers are skipped and baseDirection is returned unchanged.
    /// </summary>
    private static Vector3 CalculateFinalShotDirection(
        Vector3        baseDirection,
        in SpreadState state,
        int            activeShotIndex,
        int            currentTick,
        float          inputSpeedSqr,
        float          moveSpeedMax,
        bool           isCrouching,
        float          baseInaccuracy,
        float          moveInaccuracyMax,
        float          crouchMoveInaccuracyMax,
        float          crouchInaccuracyMultiplier,
        float          fireInaccuracyHalfLife,
        float          firstShotThreshold,
        Vector2[]      recoilPattern,
        float          patternScale,
        uint           seed,
        out float      outDecayedSustained)
    {
        float inaccuracyAngle = CalculateInaccuracyAngle(
            state, activeShotIndex, currentTick,
            inputSpeedSqr, moveSpeedMax, isCrouching,
            baseInaccuracy, moveInaccuracyMax, crouchMoveInaccuracyMax, crouchInaccuracyMultiplier,
            fireInaccuracyHalfLife, firstShotThreshold,
            out outDecayedSustained);

        // First-shot-accurate: pattern and cone are both suppressed.
        // Condition is inaccuracyAngle == 0 AND activeShotIndex == 0
        // (CalculateInaccuracyAngle already checks both internally).
        bool isFirstShotAccurate = inaccuracyAngle < 0.001f && activeShotIndex == 0;
        if (isFirstShotAccurate) return baseDirection.normalized;

        // ── Layer 1: deterministic recoil pattern ─────────────────────
        Vector3 patternDirection = ApplyRecoilPattern(baseDirection, recoilPattern, activeShotIndex, patternScale);

        // ── Layer 2: random inaccuracy cone ───────────────────────────
        if (inaccuracyAngle < 0.001f) return patternDirection;
        return ApplySpreadToDirection(patternDirection, inaccuracyAngle, seed);
    }

    /// <summary>
    /// Computes the random inaccuracy half-angle (degrees). Pure layer 2 only.
    /// Returns 0 when the first-shot-accurate conditions are all met.
    ///
    /// <paramref name="outDecayedSustained"/> is the heat value AFTER exponential
    /// decay — pass it to AdvanceSpreadState when committing a shot.
    /// </summary>
    private static float CalculateInaccuracyAngle(
        in SpreadState state,
        int            activeShotIndex,
        int            currentTick,
        float          inputSpeedSqr,
        float          moveSpeedMax,
        bool           isCrouching,
        float          baseInaccuracy,
        float          moveInaccuracyMax,
        float          crouchMoveInaccuracyMax,
        float          crouchInaccuracyMultiplier,
        float          fireInaccuracyHalfLife,
        float          firstShotThreshold,
        out float      outDecayedSustained)
    {
        float timeSinceLastShot = (currentTick - state.LastFireTick) * NetworkTickManager.TickDuration;

        // ── Exponential decay of sustained-fire heat ───────────────────
        float decayK = (fireInaccuracyHalfLife > 0.0001f)
            ? 0.693147f / fireInaccuracyHalfLife
            : 1000f;
        outDecayedSustained = state.SustainedInaccuracy * Mathf.Exp(-decayK * timeSinceLastShot);

        // ── First-shot-accurate ────────────────────────────────────────
        // Requires: stationary + cooled down + pattern reset (ShotIndex == 0).
        // The ShotIndex check is the key addition vs the original — in CS2 you
        // must also wait for the spray pattern to reset before getting the
        // first-shot accuracy bonus back.
        bool isFirstShotAccurate = inputSpeedSqr        < 0.01f
                                   && timeSinceLastShot >= firstShotThreshold
                                   && outDecayedSustained < 0.01f
                                   && activeShotIndex    == 0;
        if (isFirstShotAccurate) return 0f;

        // ── Movement inaccuracy — quadratic curve ──────────────────────
        // Quadratic (not linear) means small movements barely hurt, but
        // sprinting is heavily penalised. Crouching uses a separate, smaller cap.
        float maxSpeedSqr      = moveSpeedMax * moveSpeedMax;
        float speedRatio       = maxSpeedSqr > 0.0001f ? Mathf.Clamp01(inputSpeedSqr / maxSpeedSqr) : 0f;
        float effectiveMoveMax = isCrouching ? crouchMoveInaccuracyMax : moveInaccuracyMax;
        float moveInaccuracy   = effectiveMoveMax * (speedRatio * speedRatio);

        // ── Base + sustained heat, with crouch bonus ───────────────────
        float basePlusSustained = baseInaccuracy + outDecayedSustained;
        if (isCrouching) basePlusSustained *= crouchInaccuracyMultiplier;

        return basePlusSustained + moveInaccuracy;
    }

    /// <summary>
    /// Applies the deterministic recoil pattern for <paramref name="shotIndex"/>.
    ///
    /// Pattern offsets are cumulative absolute directions, not deltas — entry N
    /// is where shot N lands relative to the aim point. This means the weapon
    /// can be fully counter-controlled: if the player aims down 5° before firing
    /// shot 3, the bullet returns to the original aim point (just like CS2 AK spray
    /// control). The math works because baseDirection already encodes mouse input.
    ///
    /// Once shotIndex exceeds the array length the last entry is held,
    /// matching CS2's behaviour at the end of a mag.
    /// </summary>
    private static Vector3 ApplyRecoilPattern(Vector3 baseDirection, Vector2[] pattern, int shotIndex, float scale)
    {
        if (pattern == null || pattern.Length == 0 || scale < 0.001f)
            return baseDirection.normalized;

        // Clamp, never wrap — matches CS2's "last row hold" beyond the pattern end
        int     i    = Mathf.Min(shotIndex, pattern.Length - 1);
        float   hDeg = pattern[i].x * scale; // positive = right
        float   vDeg = pattern[i].y * scale; // positive = up

        if (Mathf.Abs(hDeg) < 0.001f && Mathf.Abs(vDeg) < 0.001f)
            return baseDirection.normalized;

        // Build a local frame around the aim direction and rotate by the pattern angles.
        // Using Tan keeps the math consistent with ApplySpreadToDirection below.
        Vector3 dir   = baseDirection.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(Vector3.forward, dir);
        right.Normalize();
        Vector3 up = Vector3.Cross(dir, right);

        float hRad = hDeg * Mathf.Deg2Rad;
        float vRad = vDeg * Mathf.Deg2Rad;

        return (dir + right * Mathf.Tan(hRad) + up * Mathf.Tan(vRad)).normalized;
    }

    /// <summary>
    /// Returns a new SpreadState after committing one shot.
    /// Increments ShotIndex and accumulates random heat. Pure — no mutations.
    /// </summary>
    private static SpreadState AdvanceSpreadState(
        in SpreadState state,
        int            currentTick,
        int            activeShotIndex,
        float          decayedSustained,
        float          fireInaccuracyPerShot,
        float          fireInaccuracyMax)
    {
        float newSustained = Mathf.Min(decayedSustained + fireInaccuracyPerShot, fireInaccuracyMax);
        // ShotIndex advances by 1; clamped inside ApplyRecoilPattern, not here,
        // so the state accurately reflects how many shots have been fired.
        int nextShotIndex = activeShotIndex + 1;
        return new SpreadState(newSustained, currentTick, nextShotIndex);
    }

    /// <summary>
    /// Apply a Gaussian random offset within a clamped cone (Box-Muller transform).
    /// 99.7% of shots land within the half-angle cone (3σ rule).
    /// Hard-clamped at the boundary so no bullet ever exits the cone.
    /// Zero GC. Deterministic given the same seed.
    /// </summary>
    private static Vector3 ApplySpreadToDirection(Vector3 baseDirection, float spreadDegrees, uint seed)
    {
        if (spreadDegrees < 0.001f) return baseDirection.normalized;

        // xorshift32 — fast, no alloc, deterministic
        static uint Next(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return s;
        }

        uint h = seed;
        const float UintToFloat = 1f / 4294967295f;
        float u1 = Mathf.Max(Next(ref h) * UintToFloat, 1e-7f); // guard Log(0)
        float u2 = Next(ref h) * UintToFloat;

        float sigma     = spreadDegrees * (1f / 3f);
        float magnitude = sigma * Mathf.Sqrt(-2f * Mathf.Log(u1));
        float angle2D   = u2 * (2f * Mathf.PI);

        float dx = magnitude * Mathf.Cos(angle2D);
        float dy = magnitude * Mathf.Sin(angle2D);

        // Hard-clamp to the full cone radius
        float distSqr = dx * dx + dy * dy;
        if (distSqr > spreadDegrees * spreadDegrees)
        {
            float inv = spreadDegrees / Mathf.Sqrt(distSqr);
            dx *= inv;
            dy *= inv;
        }

        Vector3 dir   = baseDirection.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(Vector3.forward, dir);
        right.Normalize();
        Vector3 up = Vector3.Cross(dir, right);

        float dxRad = dx * Mathf.Deg2Rad;
        float dyRad = dy * Mathf.Deg2Rad;

        return (dir + right * Mathf.Tan(dxRad) + up * Mathf.Tan(dyRad)).normalized;
    }

    /// <summary>Deterministic seed. Identical on client and server for the same netId + tick.</summary>
    private uint GenerateSeed(int tick) => (uint)((netId * 1000003) ^ (tick * 7919));

    /// <summary>Aim direction from screen-centre raycast.</summary>
    private Vector3 GetShootDirection()
    {
        if (_playerCamera == null) _playerCamera = Camera.main;
        if (_playerCamera == null) return gunTransform.forward;

        Ray ray = _playerCamera.ScreenPointToRay(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, maxRange)
            ? hit.point
            : ray.origin + ray.direction * maxRange;

        return (targetPoint - gunTransform.position).normalized;
    }

    // ════════════════════════════════════════════════════════════════
    // SERVER — HITSCAN WITH LAG COMPENSATION + TWO-LAYER SPREAD
    // ════════════════════════════════════════════════════════════════

    [Command]
    protected virtual void CmdFireAutomatic(Vector3 origin, Vector3 baseDirection, int clientTick, bool isCrouching)
    {
        // ── Block shooting while dead ──────────────────────────────────
        PlayerState playerState = GetComponent<PlayerState>();
        if (playerState != null && !playerState.IsAlive) return;

        // ── Fire-rate gate ─────────────────────────────────────────────
        int ticksSinceLastFire = clientTick - _spreadState.LastFireTick;
        if (ticksSinceLastFire < fireRateTicks - 1) return; // -1 tick tolerance for network jitter

        // ── Origin anti-cheat ──────────────────────────────────────────
        if (Vector3.Distance(origin, gunTransform.position) > 3f)
            origin = gunTransform.position;
        baseDirection = baseDirection.normalized;

        // ── Server independently reconstructs spread ───────────────────
        float inputSpeedSqr = 0f;
        float moveSpeedMax  = 5f;
        if (TryGetComponent<PlayerMovement>(out var playerMovement))
        {
            inputSpeedSqr = playerMovement.LastInputSpeedSqr;
            moveSpeedMax  = playerMovement.MaxMoveSpeed;
        }

        float timeSinceLastShot = ticksSinceLastFire * NetworkTickManager.TickDuration;
        int   activeShotIndex   = ResolveActiveShotIndex(_spreadState, timeSinceLastShot, patternResetTime);
        uint  seed              = GenerateSeed(clientTick);

        Vector3 finalDirection = CalculateFinalShotDirection(
            baseDirection, _spreadState, activeShotIndex, clientTick,
            inputSpeedSqr, moveSpeedMax, isCrouching,
            baseInaccuracy, moveInaccuracyMax, crouchMoveInaccuracyMax, crouchInaccuracyMultiplier,
            fireInaccuracyHalfLife, firstShotThreshold,
            recoilPattern, patternScale,
            seed,
            out float decayedSustained);

        _spreadState = AdvanceSpreadState(
            _spreadState, clientTick, activeShotIndex,
            decayedSustained, fireInaccuracyPerShot, fireInaccuracyMax);
        LastFireTime = (float)NetworkTime.time;

        // ── Lag compensation ───────────────────────────────────────────
        bool needsLagComp = connectionToClient != null &&
                            !(connectionToClient is LocalConnectionToClient);
        int targetCount = 0;

        if (needsLagComp)
        {
            _lagCompensators.Clear();
            FindAllLagCompensators(_lagCompensators);

            if (_lagCompBuffer.Length < _lagCompensators.Count)
                _lagCompBuffer = new (LagCompensator, Vector3, Quaternion)[_lagCompensators.Count * 2];

            for (int i = 0; i < _lagCompensators.Count; i++)
            {
                var lc = _lagCompensators[i];
                if (lc.netIdentity == netIdentity) continue;

                var targetTeam = lc.GetComponent<PlayerTeam>();
                var ownerTeam  = GetComponent<PlayerTeam>();
                if (targetTeam && ownerTeam &&
                    ownerTeam.CurrentTeam != Team.None &&
                    targetTeam.CurrentTeam == ownerTeam.CurrentTeam)
                    continue;

                if (lc.Sample(connectionToClient, out Capture3D sample))
                {
                    _lagCompBuffer[targetCount++] = (lc, lc.transform.position, lc.transform.rotation);
                    lc.transform.position         = sample.position;
                }
            }
        }

        bool didHit = Physics.Raycast(origin, finalDirection, out RaycastHit hitInfo, maxRange, hitLayerMask);

        if (needsLagComp)
        {
            for (int i = 0; i < targetCount; i++)
            {
                var (comp, pos, rot) = _lagCompBuffer[i];
                comp.transform.position = pos;
                comp.transform.rotation = rot;
            }
        }

        if (didHit)
        {
            Vector3 hitPoint = hitInfo.point;

            var hitIdentity = hitInfo.collider.GetComponentInParent<NetworkIdentity>();
            if (hitIdentity != null && hitIdentity == netIdentity)
            { RpcOnMiss(hitPoint, origin, finalDirection); return; }

            var hitTeam = hitInfo.collider.GetComponentInParent<PlayerTeam>();
            var myTeam  = GetComponent<PlayerTeam>();
            if (hitTeam != null && myTeam != null &&
                myTeam.CurrentTeam != Team.None &&
                hitTeam.CurrentTeam == myTeam.CurrentTeam)
            { RpcOnMiss(hitPoint, origin, finalDirection); return; }

            var damageable = hitInfo.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            { OnServerHit(damageable, hitInfo, damage); RpcOnHit(hitPoint, origin, finalDirection); return; }

            RpcOnMiss(hitPoint, origin, finalDirection);
        }
        else
        {
            RpcOnMiss(origin + finalDirection * maxRange, origin, finalDirection);
        }
    }

    [Server]
    protected virtual void OnServerHit(IDamageable target, RaycastHit hitInfo, float hitDamage)
    {
        float finalDamage = hitDamage * _damageMultiplier;
        if (target is PlayerHealth playerHealth)
            playerHealth.TakeDamageFrom(finalDamage, netId);
        else
            target.TakeDamage(finalDamage);
    }

    private void FindAllLagCompensators(List<LagCompensator> results)
    {
        foreach (var kvp in NetworkServer.spawned)
        {
            if (kvp.Value == null) continue;
            if (kvp.Value.TryGetComponent<LagCompensator>(out var lc))
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
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-direction));
        if (!isLocalPlayer)
            SpawnLocalTracer(origin, direction, hitPoint);
    }

    [ClientRpc]
    private void RpcOnMiss(Vector3 endPoint, Vector3 origin, Vector3 direction)
    {
        if (!isLocalPlayer)
            SpawnLocalTracer(origin, direction, endPoint);
    }

    // ════════════════════════════════════════════════════════════════
    // TRACER
    // ════════════════════════════════════════════════════════════════

    private void SpawnLocalTracer(Vector3 origin, Vector3 direction, Vector3 endpoint = default)
    {
        if (_tracerPool == null) return;

        if (endpoint == default)
        {
            endpoint = Physics.Raycast(origin, direction, out RaycastHit hit, maxRange)
                ? hit.point
                : origin + direction * maxRange;
        }

        BulletTracer tracer = _tracerPool.Get();
        tracer.Fire(origin, endpoint, t => _tracerPool.Release(t));
    }

    // ════════════════════════════════════════════════════════════════
    // COOLDOWN UI
    // ════════════════════════════════════════════════════════════════

    private System.Collections.IEnumerator CooldownUIRoutine()
    {
        var   wait         = new WaitForSeconds(0.017f);
        float fireRateSecs = fireRateTicks * NetworkTickManager.TickDuration;

        while (true)
        {
            float timeSinceLast    = (float)NetworkTime.time - LastFireTime;
            float cooldownProgress = Mathf.Clamp01(timeSinceLast / fireRateSecs);

            if (Mathf.Abs(cooldownProgress - _lastReportedProgress) > 0.01f)
            {
                UIManager.Instance?.UpdateGunCooldown(cooldownProgress);
                _lastReportedProgress = cooldownProgress;
            }

            yield return wait;
        }
    }

    private void OnLastFireTimeChanged(float oldVal, float newVal) => _lastReportedProgress = -1f;
}