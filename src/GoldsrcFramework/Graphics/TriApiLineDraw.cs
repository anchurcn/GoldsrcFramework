using System;
using GoldsrcFramework.Ecs.Diagnostics;
using GoldsrcFramework.Engine.Native;
using Stride.Core.Mathematics;

namespace GoldsrcFramework.Graphics;

/// <summary>
/// Draws world-space line segments through the engine's triangle API (pTriAPI).
/// </summary>
/// <remarks>
/// Must be called from <c>HUD_DrawNormalTriangles</c> or
/// <c>HUD_DrawTransparentTriangles</c>, where the engine has already established
/// the world/view/projection matrices. In the normal pass depth testing is on and
/// blending is off; in the transparent pass blending is on.
/// <para>
/// No z-bias is applied: lines may z-fight against world surfaces. This is
/// accepted for debug visualization rather than introducing depth perturbation
/// that would make the shown geometry lie about the physics pose.
/// </para>
/// </remarks>
internal static unsafe class TriApiLineDraw
{
    /// <summary>
    /// Draws independent line segments. <paramref name="vertices"/> must hold an even
    /// count (a0,b0, a1,b1, ...). Color is taken from each segment's first vertex;
    /// the two vertices of a segment are assumed to share a color.
    /// </summary>
    public static void DrawLineVertices(ReadOnlySpan<DebugVertex> vertices)
    {
        if (vertices.IsEmpty)
            return;

        var client = EngineApi.PClient;
        if (client == null)
            return;

        var tri = client->pTriAPI;
        if (tri == null)
            return;
        if (tri->Begin == null || tri->End == null || tri->Color4f == null || tri->Vertex3f == null)
            return;

        tri->Begin((int)TriPrimitive.Lines);
        for (int i = 0; i + 1 < vertices.Length; i += 2)
        {
            ref readonly var a = ref vertices[i];
            ref readonly var b = ref vertices[i + 1];
            var color = a.Color;
            tri->Color4f(color.R, color.G, color.B, color.A);
            tri->Vertex3f(a.Position.X, a.Position.Y, a.Position.Z);
            tri->Vertex3f(b.Position.X, b.Position.Y, b.Position.Z);
        }
        tri->End();
    }
}
