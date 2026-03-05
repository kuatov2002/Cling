using UnityEngine;

/// <summary>
/// Snapshot of authoritative player state at a specific tick.
/// Value type — zero GC.
/// </summary>
public struct PlayerStateSnapshot
{
    public int tick;
    public Vector3 position;
    public float yaw;
    public float pitch;
    public Vector3 velocity;
    public bool isGrounded;

    public static float PositionError(in PlayerStateSnapshot a, in PlayerStateSnapshot b)
    {
        return Vector3.Distance(a.position, b.position);
    }
}

/// <summary>
/// Fixed-size ring buffer for predicted/authoritative state snapshots.
/// Indexed by tick % Capacity. Pre-allocated — zero GC after construction.
/// </summary>
public class StateBuffer
{
    public const int Capacity = 256;

    private readonly PlayerStateSnapshot[] _buffer = new PlayerStateSnapshot[Capacity];
    private readonly bool[] _valid = new bool[Capacity];

    public void Set(int tick, in PlayerStateSnapshot snapshot)
    {
        int idx = tick & (Capacity - 1); // tick % 256 via bitmask
        _buffer[idx] = snapshot;
        _valid[idx] = true;
    }

    public bool TryGet(int tick, out PlayerStateSnapshot snapshot)
    {
        int idx = tick & (Capacity - 1);
        if (_valid[idx] && _buffer[idx].tick == tick)
        {
            snapshot = _buffer[idx];
            return true;
        }
        snapshot = default;
        return false;
    }

    public void Clear()
    {
        for (int i = 0; i < Capacity; i++)
            _valid[i] = false;
    }
}
