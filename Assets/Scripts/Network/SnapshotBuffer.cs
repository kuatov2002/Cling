using UnityEngine;

/// <summary>
/// Snapshot for entity interpolation — remote player state at a server timestamp.
/// Value type — zero GC.
/// </summary>
public struct RemoteSnapshot
{
    public double serverTime;
    public Vector3 position;
    public Quaternion rotation;
    public float speed;    // for animator
    public float strafe;   // for animator
    public bool isAiming;  // for animator
}

/// <summary>
/// Sorted buffer of remote player snapshots for entity interpolation.
/// Features:
///   - Linear interpolation between two flanking snapshots
///   - Extrapolation fallback (capped to 100ms)
///   - Adaptive jitter buffer (Phase 5)
/// Pre-allocated — zero GC after construction.
/// </summary>
public class SnapshotBuffer
{
    public const int Capacity = 32;

    // Sorted by serverTime (oldest first). Manual insertion sort.
    private readonly RemoteSnapshot[] _snapshots = new RemoteSnapshot[Capacity];
    private int _count;

    // ── Jitter tracking (Phase 5) ─────────────────────────────────
    private double _lastArrivalTime;
    private double _lastServerTime;
    private double _jitterEMA;          // exponential moving average of jitter
    private const double JitterAlpha = 0.1;  // EMA smoothing factor
    private const double MinDelay = 0.03125; // 2 ticks at 64Hz
    private const double MaxDelay = 0.09375; // 6 ticks at 64Hz
    private const double SafetyMultiplier = 2.0;

    /// <summary>Current adaptive interpolation delay (seconds).</summary>
    public double InterpolationDelay { get; private set; } = MinDelay;

    public int Count => _count;

    /// <summary>
    /// Add a new snapshot. Maintains time-sorted order.
    /// </summary>
    public void Add(double serverTime, Vector3 position, Quaternion rotation,
                    float speed = 0f, float strafe = 0f, bool isAiming = false)
    {
        // ── Jitter tracking ────────────────────────────────────────
        double now = Time.unscaledTimeAsDouble;
        if (_lastServerTime > 0)
        {
            double expectedInterval = serverTime - _lastServerTime;
            double actualInterval = now - _lastArrivalTime;
            double jitter = Mathf.Abs((float)(actualInterval - expectedInterval));
            _jitterEMA = _jitterEMA * (1.0 - JitterAlpha) + jitter * JitterAlpha;

            // Adaptive delay: base + jitter * safety
            double adaptiveDelay = MinDelay + _jitterEMA * SafetyMultiplier;
            // Smoothly lerp toward adaptive delay (don't snap)
            InterpolationDelay += (adaptiveDelay - InterpolationDelay) * 0.1;
            InterpolationDelay = System.Math.Clamp(InterpolationDelay, MinDelay, MaxDelay);
        }
        _lastArrivalTime = now;
        _lastServerTime = serverTime;

        // ── Insert sorted ──────────────────────────────────────────
        RemoteSnapshot snap;
        snap.serverTime = serverTime;
        snap.position = position;
        snap.rotation = rotation;
        snap.speed = speed;
        snap.strafe = strafe;
        snap.isAiming = isAiming;

        if (_count < Capacity)
        {
            _snapshots[_count] = snap;
            _count++;
        }
        else
        {
            // Buffer full — drop oldest (index 0), shift left, insert at end
            for (int i = 0; i < Capacity - 1; i++)
                _snapshots[i] = _snapshots[i + 1];
            _snapshots[Capacity - 1] = snap;
        }
    }

    /// <summary>
    /// Interpolate between two flanking snapshots at the given render time.
    /// Returns false if insufficient data.
    /// </summary>
    public bool Sample(double renderTime, out Vector3 position, out Quaternion rotation,
                       out float speed, out float strafe, out bool isAiming)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        speed = 0f;
        strafe = 0f;
        isAiming = false;

        if (_count < 2) return false;

        // Find two flanking snapshots
        for (int i = 0; i < _count - 1; i++)
        {
            ref readonly var a = ref _snapshots[i];
            ref readonly var b = ref _snapshots[i + 1];

            if (renderTime >= a.serverTime && renderTime <= b.serverTime)
            {
                double span = b.serverTime - a.serverTime;
                float t = span > 0.0001 ? (float)((renderTime - a.serverTime) / span) : 0f;

                position = Vector3.LerpUnclamped(a.position, b.position, t);
                rotation = Quaternion.SlerpUnclamped(a.rotation, b.rotation, t);
                speed = Mathf.Lerp(a.speed, b.speed, t);
                strafe = Mathf.Lerp(a.strafe, b.strafe, t);
                isAiming = t < 0.5f ? a.isAiming : b.isAiming;
                return true;
            }
        }

        // Extrapolation: renderTime is newer than newest snapshot
        if (renderTime > _snapshots[_count - 1].serverTime)
        {
            ref readonly var secondLast = ref _snapshots[_count - 2];
            ref readonly var last = ref _snapshots[_count - 1];

            double span = last.serverTime - secondLast.serverTime;
            double overshoot = renderTime - last.serverTime;

            // Cap extrapolation to 100ms
            if (overshoot > 0.1) overshoot = 0.1;

            if (span > 0.0001)
            {
                float t = (float)(overshoot / span);
                Vector3 vel = last.position - secondLast.position;
                position = last.position + vel * t;
                rotation = last.rotation; // don't extrapolate rotation
                speed = last.speed;
                strafe = last.strafe;
                isAiming = last.isAiming;
                return true;
            }
        }

        // Fallback: use latest snapshot
        if (_count > 0)
        {
            ref readonly var latest = ref _snapshots[_count - 1];
            position = latest.position;
            rotation = latest.rotation;
            speed = latest.speed;
            strafe = latest.strafe;
            isAiming = latest.isAiming;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Remove snapshots older than the given time.
    /// Keeps at least 2 snapshots for interpolation.
    /// </summary>
    public void Prune(double olderThan)
    {
        int removeCount = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_snapshots[i].serverTime < olderThan && (_count - removeCount) > 2)
                removeCount++;
            else
                break;
        }

        if (removeCount > 0)
        {
            for (int i = 0; i < _count - removeCount; i++)
                _snapshots[i] = _snapshots[i + removeCount];
            _count -= removeCount;
        }
    }

    public void Clear()
    {
        _count = 0;
        _jitterEMA = 0;
        _lastArrivalTime = 0;
        _lastServerTime = 0;
        InterpolationDelay = MinDelay;
    }
}
