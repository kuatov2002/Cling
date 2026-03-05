using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Fixed-rate tick simulation loop (64 Hz) decoupled from Unity's frame rate.
/// All game logic (movement, combat, timers) should subscribe to OnTick.
/// Visual rendering uses TickAlpha for interpolation between ticks.
/// </summary>
public class NetworkTickManager : MonoBehaviour
{
    public static NetworkTickManager Instance { get; private set; }

    // ── Configuration ──────────────────────────────────────────────
    public const int TickRate = 64;
    public const float TickDuration = 1f / TickRate;   // 0.015625 s
    private const int MaxTicksPerFrame = 5;             // prevent spiral of death

    // ── State ──────────────────────────────────────────────────────
    /// <summary>Current simulation tick (wraps at int.MaxValue).</summary>
    public static int Tick { get; private set; }

    /// <summary>Fractional remainder after last tick drain (0..1) for visual interpolation.</summary>
    public static float TickAlpha { get; private set; }

    /// <summary>True while inside a tick callback (OnTick / OnPostTick).</summary>
    public static bool IsInsideTick { get; private set; }

    // ── Events ─────────────────────────────────────────────────────
    /// <summary>Fired once per simulation tick. Arg = current tick number.</summary>
    public static event Action<int> OnTick;

    /// <summary>Fired after all OnTick listeners have run. Good for physics sync.</summary>
    public static event Action OnPostTick;

    // ── Internals ──────────────────────────────────────────────────
    private float _accumulator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Align Mirror tick rate with our simulation
        if (NetworkServer.active)
            NetworkServer.tickRate = TickRate;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            OnTick = null;
            OnPostTick = null;
        }
    }

    private void Update()
    {
        _accumulator += Time.deltaTime;

        int ticksThisFrame = 0;

        while (_accumulator >= TickDuration && ticksThisFrame < MaxTicksPerFrame)
        {
            _accumulator -= TickDuration;
            ticksThisFrame++;
            Tick++;

            IsInsideTick = true;
            try
            {
                OnTick?.Invoke(Tick);
                OnPostTick?.Invoke();
            }
            finally
            {
                IsInsideTick = false;
            }
        }

        // Clamp accumulator if we hit the spiral-of-death cap
        if (_accumulator > TickDuration * MaxTicksPerFrame)
            _accumulator = 0f;

        // Alpha for visual interpolation: 0 = last tick, 1 = next tick
        TickAlpha = _accumulator / TickDuration;
    }

    /// <summary>
    /// Reset tick state. Called when returning to lobby / stopping network.
    /// </summary>
    public static void ResetTick()
    {
        Tick = 0;
        TickAlpha = 0f;
    }
}
