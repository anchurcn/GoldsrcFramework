using System.Collections.Generic;
using System.Runtime.InteropServices;
using Stride.Core.Mathematics;

namespace GoldsrcFramework.Ecs.Diagnostics;

/// <summary>
/// A world-space line segment vertex: position plus per-vertex color. The two
/// vertices of a line share a color.
/// </summary>
public readonly struct DebugVertex
{
    public readonly Vector3 Position;
    public readonly Color4 Color;

    public DebugVertex(Vector3 position, Color4 color)
    {
        Position = position;
        Color = color;
    }
}

/// <summary>
/// Records world-space line segments emitted by debug sources, to be flushed later
/// into the engine's triangle API. Lives in the ECS layer (no GoldSrc native types);
/// the host reads <see cref="LineVertices"/> and submits it through pTriAPI.
/// </summary>
/// <remarks>
/// Filled during the Stride Draw pass (physics poses are final by then) and flushed
/// at <c>HUD_DrawNormalTriangles</c>. Nothing mutates the list between fill and flush
/// within a frame, so exposing a <see cref="ReadOnlySpan{DebugVertex}"/> over the
/// internal list is safe for immediate consumption.
/// </remarks>
public sealed class DebugDrawCommandList
{
    private readonly List<DebugVertex> _lines = [];

    /// <summary>Pairs of vertices forming line segments (a0,b0,a1,b1,...).</summary>
    public ReadOnlySpan<DebugVertex> LineVertices => CollectionsMarshal.AsSpan(_lines);

    public int LineVertexCount => _lines.Count;

    public void Clear() => _lines.Clear();

    public void AddLine(Vector3 a, Vector3 b, Color4 color)
    {
        _lines.Add(new DebugVertex(a, color));
        _lines.Add(new DebugVertex(b, color));
    }
}
