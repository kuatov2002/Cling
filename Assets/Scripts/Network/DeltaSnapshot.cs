using Mirror;
using UnityEngine;

/// <summary>
/// Quantized position: 3 shorts for millimeter precision (range ±32.767 m per axis).
/// For larger maps, use int with 0.01 precision.
/// </summary>
public struct Vector3Short
{
    public short x, y, z;

    public Vector3Short(Vector3 v)
    {
        x = (short)Mathf.Clamp(v.x * 1000f, short.MinValue, short.MaxValue);
        y = (short)Mathf.Clamp(v.y * 1000f, short.MinValue, short.MaxValue);
        z = (short)Mathf.Clamp(v.z * 1000f, short.MinValue, short.MaxValue);
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x / 1000f, y / 1000f, z / 1000f);
    }
}

/// <summary>
/// Delta-compressed state snapshot with dirty bitmask.
/// Only changed fields are serialized, reducing bandwidth.
///
/// Dirty bits:
///   0x01 = position
///   0x02 = yaw
///   0x04 = pitch
///   0x08 = health
///   0x10 = speed (animation)
///   0x20 = strafe (animation)
///   0x40 = isAiming
/// </summary>
public struct DeltaSnapshot
{
    public byte dirtyMask;
    public Vector3Short position;
    public short yaw;      // degrees * 100
    public short pitch;    // degrees * 100
    public byte health;    // 0-255 mapped from 0-maxHealth
    public short speed;    // animation speed * 100
    public short strafe;   // animation strafe * 100
    public bool isAiming;

    public static short QuantizeAngle(float degrees)
    {
        return (short)(degrees * 100f);
    }

    public static float DequantizeAngle(short q)
    {
        return q / 100f;
    }

    public static byte QuantizeHealth(float current, float max)
    {
        return (byte)(Mathf.Clamp01(current / max) * 255f);
    }

    public static float DequantizeHealth(byte q, float max)
    {
        return (q / 255f) * max;
    }

    /// <summary>
    /// Compare two snapshots and return a delta with only changed fields.
    /// </summary>
    public static DeltaSnapshot CreateDelta(in DeltaSnapshot prev, in DeltaSnapshot current)
    {
        DeltaSnapshot delta = current;
        delta.dirtyMask = 0;

        if (current.position.x != prev.position.x ||
            current.position.y != prev.position.y ||
            current.position.z != prev.position.z)
            delta.dirtyMask |= 0x01;

        if (current.yaw != prev.yaw)
            delta.dirtyMask |= 0x02;

        if (current.pitch != prev.pitch)
            delta.dirtyMask |= 0x04;

        if (current.health != prev.health)
            delta.dirtyMask |= 0x08;

        if (current.speed != prev.speed)
            delta.dirtyMask |= 0x10;

        if (current.strafe != prev.strafe)
            delta.dirtyMask |= 0x20;

        if (current.isAiming != prev.isAiming)
            delta.dirtyMask |= 0x40;

        return delta;
    }
}

/// <summary>
/// Custom Mirror serialization for DeltaSnapshot.
/// Only serializes fields marked as dirty in the bitmask.
/// </summary>
public static class DeltaSnapshotSerializer
{
    public static void WriteDeltaSnapshot(this NetworkWriter writer, DeltaSnapshot value)
    {
        writer.WriteByte(value.dirtyMask);

        if ((value.dirtyMask & 0x01) != 0)
        {
            writer.WriteShort(value.position.x);
            writer.WriteShort(value.position.y);
            writer.WriteShort(value.position.z);
        }

        if ((value.dirtyMask & 0x02) != 0)
            writer.WriteShort(value.yaw);

        if ((value.dirtyMask & 0x04) != 0)
            writer.WriteShort(value.pitch);

        if ((value.dirtyMask & 0x08) != 0)
            writer.WriteByte(value.health);

        if ((value.dirtyMask & 0x10) != 0)
            writer.WriteShort(value.speed);

        if ((value.dirtyMask & 0x20) != 0)
            writer.WriteShort(value.strafe);

        if ((value.dirtyMask & 0x40) != 0)
            writer.WriteBool(value.isAiming);
    }

    public static DeltaSnapshot ReadDeltaSnapshot(this NetworkReader reader)
    {
        DeltaSnapshot snap;
        snap.dirtyMask = reader.ReadByte();
        snap.position = default;
        snap.yaw = 0;
        snap.pitch = 0;
        snap.health = 0;
        snap.speed = 0;
        snap.strafe = 0;
        snap.isAiming = false;

        if ((snap.dirtyMask & 0x01) != 0)
        {
            snap.position.x = reader.ReadShort();
            snap.position.y = reader.ReadShort();
            snap.position.z = reader.ReadShort();
        }

        if ((snap.dirtyMask & 0x02) != 0)
            snap.yaw = reader.ReadShort();

        if ((snap.dirtyMask & 0x04) != 0)
            snap.pitch = reader.ReadShort();

        if ((snap.dirtyMask & 0x08) != 0)
            snap.health = reader.ReadByte();

        if ((snap.dirtyMask & 0x10) != 0)
            snap.speed = reader.ReadShort();

        if ((snap.dirtyMask & 0x20) != 0)
            snap.strafe = reader.ReadShort();

        if ((snap.dirtyMask & 0x40) != 0)
            snap.isAiming = reader.ReadBool();

        return snap;
    }
}
