using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// CS-level networked player movement with:
///   - Fixed 64 Hz tick simulation
///   - Client-side prediction with input buffering
///   - Server reconciliation with input replay
///   - Entity interpolation for remote players
///   - Adaptive jitter buffer
///   - Zero GC in hot paths
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private Transform followTarget;

    [Header("Camera & Shooting")]
    [SerializeField] private Gun gun;
    [SerializeField] private Animator animator;

    [Header("Prediction")]
    [SerializeField] private float reconciliationThreshold = 0.01f;
    [SerializeField] private float smoothCorrectionThreshold = 0.5f;
    [SerializeField] private float smoothCorrectionRate = 10f;

    // ── Animation hash IDs (cached, zero alloc) ────────────────────
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimStrafe = Animator.StringToHash("Strafe");
    private static readonly int AnimIsAiming = Animator.StringToHash("IsAiming");

    // ── Components ────────────────────────────────────────────────
    private CharacterController _controller;
    private AutoAimSystem _autoAimSystem;
    private CinemachineCamera[] _freeLookCam;

    // ── Movement state ────────────────────────────────────────────
    private Vector3 _velocity;
    private bool _isAiming;
    private float _lookYaw;
    private float _lookPitch;

    // ── Client prediction ─────────────────────────────────────────
    private readonly InputBuffer _inputBuffer = new InputBuffer();
    private readonly StateBuffer _stateBuffer = new StateBuffer();
    private int _lastProcessedTick;          // last tick server confirmed
    private Vector3 _smoothCorrectionOffset; // residual correction for lerp

    // ── Server input processing ────────────────────────────────────
    private readonly Queue<InputState> _serverInputQueue = new Queue<InputState>(32);
    private int _serverLastProcessedTick;
    private int _sendStateCounter;

    // ── Remote player interpolation ───────────────────────────────
    private readonly SnapshotBuffer _snapshotBuffer = new SnapshotBuffer();

    // ── Network animation sync ────────────────────────────────────
    [SyncVar(hook = nameof(OnSpeedChanged))]
    private float _networkSpeed;

    [SyncVar(hook = nameof(OnStrafeChanged))]
    private float _networkStrafe;

    [SyncVar(hook = nameof(OnIsAimingChanged))]
    private bool _networkIsAiming;

    // ── Input caching (read once per frame) ───────────────────────
    private float _frameHorizontal;
    private float _frameVertical;
    private float _frameMouseX;
    private float _frameMouseY;
    private bool _frameFire;
    private bool _frameFireUp;
    private bool _frameCancelFire;

    // ════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;

        _autoAimSystem = GetComponent<AutoAimSystem>();
        _freeLookCam = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        System.Array.Sort(_freeLookCam, (a, b) => b.Priority.Value.CompareTo(a.Priority.Value));

        for (int i = 0; i < _freeLookCam.Length; i++)
        {
            if (_freeLookCam[i])
            {
                _freeLookCam[i].Follow = followTarget;
                _freeLookCam[i].LookAt = followTarget;
            }
        }

        // Initialize look angles from current rotation
        _lookYaw = transform.eulerAngles.y;
        _lookPitch = followTarget != null ? followTarget.localEulerAngles.x : 0f;
        if (_lookPitch > 180f) _lookPitch -= 360f;

        // Subscribe to tick
        NetworkTickManager.OnTick += OnClientTick;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Remote players: disable CharacterController (interpolation only)
        if (!isLocalPlayer && !isServer)
        {
            _controller.enabled = false;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _prevServerPosition = transform.position;
        NetworkTickManager.OnTick += OnServerTick;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        NetworkTickManager.OnTick -= OnServerTick;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (isLocalPlayer)
            NetworkTickManager.OnTick -= OnClientTick;
    }

    // ════════════════════════════════════════════════════════════════
    // INPUT SAMPLING (once per frame, consumed by tick)
    // ════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (isLocalPlayer)
        {
            SampleInput();
            ApplyMouseLook();
            ApplySmoothCorrection();
        }
        else if (isClient && !isServer)
        {
            InterpolateRemotePlayer();
        }
    }

    /// <summary>Sample input state from Unity's input system (once per frame).</summary>
    private void SampleInput()
    {
        _frameHorizontal = Input.GetAxisRaw("Horizontal");
        _frameVertical = Input.GetAxisRaw("Vertical");
        _frameMouseX = Input.GetAxis("Mouse X");
        _frameMouseY = Input.GetAxis("Mouse Y");

        // Edge-triggered fire detection
        if (Input.GetButtonDown("Fire1"))
            _frameFire = true;
        if (Input.GetButtonUp("Fire1"))
            _frameFireUp = true;
        if (Input.GetButtonDown("Fire2"))
            _frameCancelFire = true;
    }

    /// <summary>
    /// Apply mouse look every frame for responsive camera.
    /// Rotation values are captured into the next tick's InputState.
    /// </summary>
    private void ApplyMouseLook()
    {
        _lookYaw += _frameMouseX;
        _lookPitch -= _frameMouseY;

        // Auto-aim adjustment
        if (_isAiming && _autoAimSystem)
        {
            Vector3 forward = followTarget ? followTarget.forward : transform.forward;
            Transform target = _autoAimSystem.GetBestTarget(forward);
            if (target)
            {
                Vector3 adjusted = _autoAimSystem.GetAdjustedAimDirection(forward, target);
                Vector3 euler = Quaternion.LookRotation(adjusted).eulerAngles;
                _lookYaw = euler.y;
                _lookPitch = euler.x;
                if (_lookPitch > 180f) _lookPitch -= 360f;
            }
        }

        // Clamp pitch
        _lookPitch = Mathf.Clamp(_lookPitch, -60f, 70f);

        // Apply rotation visually (immediate for local player)
        transform.rotation = Quaternion.Euler(0f, _lookYaw, 0f);
        if (followTarget)
            followTarget.localRotation = Quaternion.Euler(_lookPitch, 0f, 0f);
    }

    /// <summary>
    /// Smoothly drain residual correction offset for small server corrections.
    /// </summary>
    private void ApplySmoothCorrection()
    {
        if (_smoothCorrectionOffset.sqrMagnitude > 0.000001f)
        {
            _smoothCorrectionOffset = Vector3.Lerp(
                _smoothCorrectionOffset, Vector3.zero,
                Time.deltaTime * smoothCorrectionRate);
        }
        else
        {
            _smoothCorrectionOffset = Vector3.zero;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // CLIENT TICK (prediction)
    // ════════════════════════════════════════════════════════════════

    /// <summary>Fixed 64 Hz client tick — prediction + input send.</summary>
    private void OnClientTick(int tick)
    {
        if (!isLocalPlayer) return;

        // ── Build input state for this tick ─────────────────────────
        InputState input;
        input.tick = tick;
        input.horizontal = _frameHorizontal;
        input.vertical = _frameVertical;
        input.lookYaw = _lookYaw;
        input.lookPitch = _lookPitch;
        input.fire = _frameFire;
        input.cancelFire = _frameCancelFire || _frameFireUp;
        input.useItem = Input.GetKey(KeyCode.E);

        // Clear edge-triggered flags (consumed)
        _frameFire = false;
        _frameFireUp = false;
        _frameCancelFire = false;

        // ── Store input for replay ──────────────────────────────────
        _inputBuffer.Set(tick, in input);

        // ── Apply movement locally (prediction) ─────────────────────
        SimulateMovement(in input);

        // ── Store predicted state ───────────────────────────────────
        PlayerStateSnapshot predicted;
        predicted.tick = tick;
        predicted.position = transform.position;
        predicted.yaw = _lookYaw;
        predicted.pitch = _lookPitch;
        predicted.velocity = _velocity;
        predicted.isGrounded = _controller.isGrounded;
        _stateBuffer.Set(tick, in predicted);

        // ── Handle shooting (client-side) ───────────────────────────
        HandleShootingClient(in input);

        // ── Send input batch to server (last 3 inputs for redundancy) ──
        InputState input1 = default;
        InputState input2 = default;
        byte sendCount = 1;

        if (_inputBuffer.TryGet(tick - 1, out input1))
        {
            sendCount = 2;
            if (_inputBuffer.TryGet(tick - 2, out input2))
                sendCount = 3;
        }

        CmdSendInputBatch(input, input1, input2, sendCount);
    }

    /// <summary>
    /// Handle charge/fire/cancel on the client side.
    /// Actual shot validation happens on server.
    /// </summary>
    private void HandleShootingClient(in InputState input)
    {
        if (gun == null) return;

        if (input.fire && !_isAiming)
        {
            if (gun.Charge())
            {
                _isAiming = true;
                if (_freeLookCam != null && _freeLookCam.Length > 0 && _freeLookCam[0])
                    _freeLookCam[0].gameObject.SetActive(false);
            }
        }

        if (input.cancelFire && _isAiming)
        {
            gun.Fire();
            _isAiming = false;
            if (_freeLookCam != null && _freeLookCam.Length > 0 && _freeLookCam[0])
                _freeLookCam[0].gameObject.SetActive(true);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // SERVER TICK (authoritative processing)
    // ════════════════════════════════════════════════════════════════

    // ── Anti-cheat ─────────────────────────────────────────────────
    [Header("Anti-Cheat")]
    [SerializeField] private float moveSpeedTolerance = 1.15f; // 15% tolerance for network jitter
    private Vector3 _prevServerPosition;
    private int _speedViolationCount;
    private const int MaxSpeedViolations = 5; // strikes before corrective action

    /// <summary>Fixed 64 Hz server tick — process one input per player.</summary>
    private void OnServerTick(int tick)
    {
        if (!isServer) return;
        if (!_controller || !_controller.enabled) return;

        // Process queued inputs (may have multiple if packets arrived between ticks)
        while (_serverInputQueue.Count > 0)
        {
            InputState input = _serverInputQueue.Dequeue();

            // Skip stale / duplicate inputs
            if (input.tick <= _serverLastProcessedTick) continue;

            _serverLastProcessedTick = input.tick;

            // ── Record position before movement (for speed validation) ──
            _prevServerPosition = transform.position;

            // ── Authoritative movement ──────────────────────────────
            SimulateMovement(in input);

            // ── Speed validation (anti-cheat) ───────────────────────
            ValidateMoveSpeed();

            // ── Update animation SyncVars ───────────────────────────
            UpdateAnimationState(in input);

            // ── Send state to owning client (every 2nd tick = 32Hz) ─
            _sendStateCounter++;
            if (_sendStateCounter >= 2)
            {
                _sendStateCounter = 0;

                PlayerStateSnapshot serverState;
                serverState.tick = input.tick;
                serverState.position = transform.position;
                serverState.yaw = input.lookYaw;
                serverState.pitch = input.lookPitch;
                serverState.velocity = _velocity;
                serverState.isGrounded = _controller.isGrounded;

                TargetRpcServerState(serverState);
            }

            // ── Broadcast position to all non-owning clients (interpolation) ──
            RpcRemoteSnapshot(
                NetworkTime.localTime,
                transform.position,
                transform.rotation,
                _networkSpeed,
                _networkStrafe,
                _networkIsAiming
            );
        }
    }

    /// <summary>Update animation SyncVars from processed input.</summary>
    private void UpdateAnimationState(in InputState input)
    {
        float speed = input.vertical * moveSpeed;
        float strafe = input.horizontal * moveSpeed;

        _networkSpeed = speed;
        _networkStrafe = strafe;
        _networkIsAiming = _isAiming;

        // Server/host: apply to own animator directly (hooks don't fire on server)
        if (animator)
        {
            animator.SetFloat(AnimSpeed, speed);
            animator.SetFloat(AnimStrafe, strafe);
            animator.SetBool(AnimIsAiming, _networkIsAiming);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // SHARED MOVEMENT SIMULATION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Deterministic movement step. Used by both client (prediction) and server (authority).
    /// Runs at fixed TickDuration (1/64s).
    /// </summary>
    private void SimulateMovement(in InputState input)
    {
        if (!_controller || !_controller.enabled) return;

        // ── Rotation ────────────────────────────────────────────────
        transform.rotation = Quaternion.Euler(0f, input.lookYaw, 0f);
        if (followTarget)
            followTarget.localRotation = Quaternion.Euler(input.lookPitch, 0f, 0f);

        // ── Movement ────────────────────────────────────────────────
        Vector2 rawInput = new Vector2(input.horizontal, input.vertical);
        if (rawInput.sqrMagnitude > 1f) rawInput.Normalize();

        Vector3 moveDirection = Vector3.zero;
        if (rawInput.sqrMagnitude > 0.0001f)
        {
            moveDirection = transform.TransformDirection(
                new Vector3(rawInput.x, 0f, rawInput.y)) * moveSpeed;
        }

        // ── Gravity ─────────────────────────────────────────────────
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f; // small downward force to stay grounded

        _velocity.y += gravity * NetworkTickManager.TickDuration;

        // ── Apply ───────────────────────────────────────────────────
        Vector3 finalMove = (moveDirection + _velocity) * NetworkTickManager.TickDuration;
        _controller.Move(finalMove);
    }

    // ════════════════════════════════════════════════════════════════
    // ANTI-CHEAT: MOVE SPEED VALIDATION (SERVER)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validate that player didn't move faster than allowed per tick.
    /// If violation detected, snap back to previous position.
    /// </summary>
    [Server]
    private void ValidateMoveSpeed()
    {
        // Only check horizontal distance (vertical includes gravity/falling)
        Vector3 delta = transform.position - _prevServerPosition;
        delta.y = 0f;
        float distanceSq = delta.sqrMagnitude;
        float maxAllowed = moveSpeed * NetworkTickManager.TickDuration * moveSpeedTolerance;
        float maxAllowedSq = maxAllowed * maxAllowed;

        if (distanceSq > maxAllowedSq)
        {
            _speedViolationCount++;
            if (_speedViolationCount >= MaxSpeedViolations)
            {
                // Corrective action: snap back to previous valid position
                Debug.LogWarning($"[AntiCheat] Player {netId} speed violation #{_speedViolationCount}. Snapping back.");
                _controller.enabled = false;
                transform.position = _prevServerPosition;
                _controller.enabled = true;
            }
        }
        else
        {
            // Decay violations over time (1 forgiven per valid tick)
            if (_speedViolationCount > 0) _speedViolationCount--;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // NETWORK COMMANDS & RPCS
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Client → Server: send last 3 inputs for redundancy (handles packet loss).
    /// Server deduplicates by tick number — only processes new inputs.
    /// </summary>
    [Command(channel = Channels.Unreliable)]
    private void CmdSendInputBatch(InputState input0, InputState input1, InputState input2, byte count)
    {
        // Process in chronological order, skip stale/duplicate inputs
        if (count >= 3 && input2.tick > _serverLastProcessedTick)
            EnqueueServerInput(in input2);
        if (count >= 2 && input1.tick > _serverLastProcessedTick)
            EnqueueServerInput(in input1);
        if (count >= 1 && input0.tick > _serverLastProcessedTick)
            EnqueueServerInput(in input0);
    }

    /// <summary>Enqueue a server input, preventing unbounded growth.</summary>
    private void EnqueueServerInput(in InputState input)
    {
        _serverInputQueue.Enqueue(input);
        while (_serverInputQueue.Count > 10)
            _serverInputQueue.Dequeue();
    }

    /// <summary>Server → Owning client: authoritative state for reconciliation.</summary>
    [TargetRpc(channel = Channels.Unreliable)]
    private void TargetRpcServerState(PlayerStateSnapshot serverState)
    {
        if (!isLocalPlayer) return;
        Reconcile(in serverState);
    }

    /// <summary>Server → All non-owning clients: position snapshot for interpolation.</summary>
    [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
    private void RpcRemoteSnapshot(double serverTime, Vector3 position, Quaternion rotation,
                                    float speed, float strafe, bool aiming)
    {
        if (isLocalPlayer || isServer) return;
        _snapshotBuffer.Add(serverTime, position, rotation, speed, strafe, aiming);
    }

    // ════════════════════════════════════════════════════════════════
    // CLIENT RECONCILIATION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Compare predicted state with server authority.
    /// If diverged, snap + replay inputs to re-predict.
    /// </summary>
    private void Reconcile(in PlayerStateSnapshot serverState)
    {
        // Get our prediction at the server's tick
        if (!_stateBuffer.TryGet(serverState.tick, out PlayerStateSnapshot predicted))
            return; // no prediction for that tick (too old)

        float error = PlayerStateSnapshot.PositionError(in predicted, in serverState);

        if (error < reconciliationThreshold)
        {
            // Prediction was accurate — no correction needed
            _lastProcessedTick = serverState.tick;
            return;
        }

        // ── Correction needed ───────────────────────────────────────

        if (error < smoothCorrectionThreshold)
        {
            // Small correction: accumulate offset, lerp it out visually
            _smoothCorrectionOffset += transform.position - serverState.position;
        }

        // Snap to server authoritative position
        _controller.enabled = false;
        transform.position = serverState.position;
        _controller.enabled = true;

        _velocity = serverState.velocity;

        // ── Replay all inputs from serverTick+1 to current tick ─────
        for (int t = serverState.tick + 1; t <= NetworkTickManager.Tick; t++)
        {
            if (_inputBuffer.TryGet(t, out InputState input))
            {
                SimulateMovement(in input);

                // Update predicted state buffer
                PlayerStateSnapshot repredicted;
                repredicted.tick = t;
                repredicted.position = transform.position;
                repredicted.yaw = input.lookYaw;
                repredicted.pitch = input.lookPitch;
                repredicted.velocity = _velocity;
                repredicted.isGrounded = _controller.isGrounded;
                _stateBuffer.Set(t, in repredicted);
            }
        }

        _lastProcessedTick = serverState.tick;
    }

    // ════════════════════════════════════════════════════════════════
    // REMOTE PLAYER INTERPOLATION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Interpolate remote player position between server snapshots.
    /// Called every frame in Update() for smooth visual rendering.
    /// </summary>
    private void InterpolateRemotePlayer()
    {
        double renderTime = NetworkTime.time - _snapshotBuffer.InterpolationDelay;

        if (_snapshotBuffer.Sample(renderTime, out Vector3 pos, out Quaternion rot,
                                   out float speed, out float strafe, out bool aiming))
        {
            transform.SetPositionAndRotation(pos, rot);

            // Update animator for remote player
            if (animator)
            {
                animator.SetFloat(AnimSpeed, speed);
                animator.SetFloat(AnimStrafe, strafe);
                animator.SetBool(AnimIsAiming, aiming);
            }
        }

        // Prune old snapshots (keep last 1 second)
        _snapshotBuffer.Prune(renderTime - 1.0);
    }

    // ════════════════════════════════════════════════════════════════
    // SYNCVAR HOOKS (fallback for clients without snapshot stream)
    // ════════════════════════════════════════════════════════════════

    private void OnSpeedChanged(float oldVal, float newVal)
    {
        if (!isLocalPlayer && animator)
            animator.SetFloat(AnimSpeed, newVal);
    }

    private void OnStrafeChanged(float oldVal, float newVal)
    {
        if (!isLocalPlayer && animator)
            animator.SetFloat(AnimStrafe, newVal);
    }

    private void OnIsAimingChanged(bool oldVal, bool newVal)
    {
        if (!isLocalPlayer && animator)
            animator.SetBool(AnimIsAiming, newVal);
    }

    // ════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Teleport player (used by respawn system).
    /// Resets prediction state.
    /// </summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        _controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        _controller.enabled = true;

        _velocity = Vector3.zero;
        _lookYaw = rotation.eulerAngles.y;
        _lookPitch = 0f;
        _smoothCorrectionOffset = Vector3.zero;
        _stateBuffer.Clear();
        _inputBuffer.Clear();
        _snapshotBuffer.Clear();
    }

    /// <summary>Enable or disable the CharacterController (for death/respawn).</summary>
    public void SetControllerEnabled(bool enabled)
    {
        if (_controller) _controller.enabled = enabled;
    }
}
