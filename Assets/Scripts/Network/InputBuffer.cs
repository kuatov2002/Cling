/// <summary>
/// Fixed-size ring buffer for input history.
/// Indexed by tick % Capacity. Pre-allocated — zero GC after construction.
/// Used for client-side prediction replay during server reconciliation.
/// </summary>
public class InputBuffer
{
    public const int Capacity = 256;

    private readonly InputState[] _buffer = new InputState[Capacity];
    private readonly bool[] _valid = new bool[Capacity];

    public void Set(int tick, in InputState input)
    {
        int idx = tick & (Capacity - 1); // tick % 256 via bitmask
        _buffer[idx] = input;
        _valid[idx] = true;
    }

    public bool TryGet(int tick, out InputState input)
    {
        int idx = tick & (Capacity - 1);
        if (_valid[idx] && _buffer[idx].tick == tick)
        {
            input = _buffer[idx];
            return true;
        }
        input = default;
        return false;
    }

    public ref InputState GetRef(int tick)
    {
        return ref _buffer[tick & (Capacity - 1)];
    }

    public bool HasInput(int tick)
    {
        int idx = tick & (Capacity - 1);
        return _valid[idx] && _buffer[idx].tick == tick;
    }

    public void Clear()
    {
        for (int i = 0; i < Capacity; i++)
            _valid[i] = false;
    }
}
