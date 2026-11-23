using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Atmos.AirFlow;

public static class AirFlowVisuals
{
    public const int ChunkSize = 8;

    /// <summary>
    /// Pressure difference on a tile at which the flow is shown at full strength.
    /// </summary>
    public const float FullDifference = 400f;

    /// <summary>
    /// Tiles with a smaller pressure difference are not reported to clients.
    /// </summary>
    public const float MinDifference = 3f;

    /// <summary>
    /// Flow converges on a tile only if its pressure difference is at least this, which keeps faint
    /// draughts from being treated as a breach.
    /// </summary>
    public const float MinSourceDifference = 20f;

    private const int StrengthMask = 31;
    private const int SourceFlag = 32;

    public static Vector2i GetChunkIndices(Vector2i tile)
    {
        return new Vector2i((int) MathF.Floor((float) tile.X / ChunkSize), (int) MathF.Floor((float) tile.Y / ChunkSize));
    }

    public static int GetTileIndex(Vector2i tile, Vector2i chunk)
    {
        return tile.X - chunk.X * ChunkSize + (tile.Y - chunk.Y * ChunkSize) * ChunkSize;
    }

    public static Vector2i GetTile(Vector2i chunk, int index)
    {
        return new Vector2i(chunk.X * ChunkSize + index % ChunkSize, chunk.Y * ChunkSize + index / ChunkSize);
    }

    /// <summary>
    /// Packs a flow direction, a pressure difference and whether the flow converges on this tile into one byte.
    /// Returns 0 when there is nothing to show.
    /// </summary>
    public static byte Encode(AtmosDirection direction, float difference, bool source)
    {
        if (difference < MinDifference)
            return 0;

        int dir;
        switch (direction)
        {
            case AtmosDirection.North:
                dir = 0;
                break;
            case AtmosDirection.South:
                dir = 1;
                break;
            case AtmosDirection.East:
                dir = 2;
                break;
            case AtmosDirection.West:
                dir = 3;
                break;
            default:
                return 0;
        }

        var strength = MathF.Sqrt(Math.Clamp(difference / FullDifference, 0f, 1f));
        var level = Math.Max(1, (int) MathF.Round(strength * StrengthMask));
        return (byte) (dir << 6 | (source ? SourceFlag : 0) | level);
    }

    /// <summary>
    /// Unpacks a byte made by <see cref="Encode"/> into a grid-local vector whose length is the flow strength, 0 to 1.
    /// </summary>
    public static Vector2 Decode(byte value, out bool source)
    {
        source = (value & SourceFlag) != 0;
        var level = value & StrengthMask;
        if (level == 0)
            return Vector2.Zero;

        var strength = (float) level / StrengthMask;
        return (value >> 6) switch
        {
            0 => new Vector2(0f, strength),
            1 => new Vector2(0f, -strength),
            2 => new Vector2(strength, 0f),
            _ => new Vector2(-strength, 0f),
        };
    }
}

[Serializable, NetSerializable]
public sealed class AirFlowVisualsUpdateEvent : EntityEventArgs
{
    public NetEntity Grid;

    /// <summary>
    /// Every flowing tile the client may see on the grid after an atmos cycle. Tiles that are not listed have
    /// no flow, so an empty list means the flow has stopped.
    /// </summary>
    public List<AirFlowChunk> Chunks = new();
}

/// <summary>
/// Flow on an 8x8 block of tiles, one byte per tile as packed by <see cref="AirFlowVisuals.Encode"/>.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct AirFlowChunk(Vector2i Index, byte[] Tiles);
