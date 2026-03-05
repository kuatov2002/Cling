using Mirror;

/// <summary>
/// Immutable input snapshot captured at a specific simulation tick.
/// Value type (struct) — zero GC. Mirror-serializable.
/// </summary>
public struct InputState
{
    /// <summary>Simulation tick this input was captured on.</summary>
    public int tick;

    // ── Movement axes (−1 .. 1) ────────────────────────────────
    public float horizontal;
    public float vertical;

    // ── Look direction (absolute, degrees) ─────────────────────
    public float lookYaw;
    public float lookPitch;

    // ── Actions ────────────────────────────────────────────────
    /// <summary>True while Fire1 is held down (level-triggered, auto-fire).</summary>
    public bool fireHeld;
    public bool useItem;
    /// <summary>True on the tick F was pressed (edge-triggered, active ability).</summary>
    public bool ability;
}

/// <summary>
/// Mirror custom serialization for InputState.
/// Bit-packed: movement as half-precision, angles as shorts.
/// </summary>
public static class InputStateSerializer
{
    public static void WriteInputState(this NetworkWriter writer, InputState value)
    {
        writer.WriteInt(value.tick);

        // Quantize movement axes: sbyte gives -128..127, map -1..1 → -127..127
        writer.WriteSByte((sbyte)(value.horizontal * 127f));
        writer.WriteSByte((sbyte)(value.vertical * 127f));

        // Quantize angles: short gives ±32767, 0.01° precision
        writer.WriteShort((short)(value.lookYaw * 100f));
        writer.WriteShort((short)(value.lookPitch * 100f));

        // Pack booleans into a single byte
        byte flags = 0;
        if (value.fireHeld) flags |= 1;
        if (value.useItem)  flags |= 4;
        if (value.ability)  flags |= 8;
        writer.WriteByte(flags);
    }

    public static InputState ReadInputState(this NetworkReader reader)
    {
        InputState state;
        state.tick = reader.ReadInt();

        state.horizontal = reader.ReadSByte() / 127f;
        state.vertical   = reader.ReadSByte() / 127f;

        state.lookYaw   = reader.ReadShort() / 100f;
        state.lookPitch = reader.ReadShort() / 100f;

        byte flags = reader.ReadByte();
        state.fireHeld = (flags & 1) != 0;
        state.useItem  = (flags & 4) != 0;
        state.ability  = (flags & 8) != 0;

        return state;
    }
}
