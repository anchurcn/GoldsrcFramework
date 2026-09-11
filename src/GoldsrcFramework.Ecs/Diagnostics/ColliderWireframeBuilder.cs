using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Core.Mathematics;

namespace GoldsrcFramework.Ecs.Diagnostics;

/// <summary>
/// Emits analytic wireframe line segments for BEPU collider shapes into a
/// <see cref="DebugDrawCommandList"/>. Generates line geometry directly per shape
/// type (box edges, sphere great circles, cylinder/capsule rings) rather than
/// extracting edges from tessellated meshes: lower vertex count, clearer debug read.
/// </summary>
/// <remarks>
/// Stride's three debug-draw implementations all render <em>solid triangles</em>
/// and use a geometry/pixel shader to fake wireframe; on the GoldSrc triangle API
/// there is no shader, so we must produce actual line geometry.
/// </remarks>
internal static class ColliderWireframeBuilder
{
    private const int SphereSegments = 16;

    public static void Emit(DebugDrawCommandList commands, CollidableComponent collidable)
    {
        // Skip collidables not yet attached to a simulation: they have no pose.
        if (collidable.Simulation is null)
            return;

        // CollidableComponent.Pose is protected-internal, so we read the public
        // Body/Static position+orientation and undo the CenterOfMass offset to get
        // the shape origin (same formula as Stride's DebugRenderProcessor.cs:123).
        Vector3 worldPosition;
        Quaternion worldRotation;
        if (collidable is BodyComponent body)
        {
            worldPosition = body.Position;
            worldRotation = body.Orientation;
        }
        else if (collidable is StaticComponent stat)
        {
            worldPosition = stat.Position;
            worldRotation = stat.Orientation;
        }
        else
        {
            return; // Unknown collidable kind; skip.
        }

        var com = collidable.CenterOfMass;
        var shapeOrigin = worldPosition - Vector3.Transform(com, worldRotation);
        var one = Vector3.One;
        Matrix.Transformation(ref one, ref worldRotation, ref shapeOrigin, out var worldMatrix);

        var color = collidable is BodyComponent b
            ? (b.Awake ? new Color4(0.2f, 1f, 0.2f, 1f) : new Color4(0f, 0.4f, 0f, 1f))
            : new Color4(0.75f, 0.75f, 0.75f, 1f); // static

        switch (collidable.Collider)
        {
            case CompoundCollider compound:
                foreach (var child in compound.Colliders)
                    EmitLeaf(commands, child, worldMatrix, color);
                break;
            case ColliderBase leaf:
                EmitLeaf(commands, leaf, worldMatrix, color);
                break;
            // MeshCollider (ICollider, not ColliderBase) and future non-ColliderBase
            // IColliders are skipped here; world-mesh wireframe is a later phase.
            default:
                break;
        }
    }

    private static void EmitLeaf(DebugDrawCommandList commands, ColliderBase leaf, in Matrix worldMatrix, Color4 color)
    {
        // Generators emit geometry at the shape's actual size, so Scale is One and
        // only the collider's local PositionLocal/RotationLocal transform applies.
        var one = Vector3.One;
        var rot = leaf.RotationLocal;
        var pos = leaf.PositionLocal;
        Matrix.Transformation(ref one, ref rot, ref pos, out var localMatrix);
        var shapeMatrix = localMatrix * worldMatrix;

        switch (leaf)
        {
            case BoxCollider box:
                WireBox(commands, shapeMatrix, box.Size, color);
                break;
            case SphereCollider sph:
                WireSphere(commands, shapeMatrix, sph.Radius, color);
                break;
            case CapsuleCollider cap:
                WireCapsule(commands, shapeMatrix, cap.Radius, cap.Length, color);
                break;
            case CylinderCollider cyl:
                WireCylinder(commands, shapeMatrix, cyl.Radius, cyl.Length, color);
                break;
            case TriangleCollider tri:
                WireTriangle(commands, shapeMatrix, tri.A, tri.B, tri.C, color);
                break;
            // ConvexHull needs hull points (DecomposedHulls.InternalPoints); deferred.
            // EmptyCollider draws nothing.
            default:
                break;
        }
    }

    private static void AddLine(DebugDrawCommandList commands, Matrix matrix, Vector3 a, Vector3 b, Color4 color)
    {
        Vector3.Transform(ref a, ref matrix, out Vector3 ta);
        Vector3.Transform(ref b, ref matrix, out Vector3 tb);
        commands.AddLine(ta, tb, color);
    }

    private static void WireBox(DebugDrawCommandList c, Matrix m, Vector3 size, Color4 col)
    {
        float hx = size.X * 0.5f, hy = size.Y * 0.5f, hz = size.Z * 0.5f;
        var v = new[]
        {
            new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz),
            new Vector3(hx, hy, -hz), new Vector3(-hx, hy, -hz),
            new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz),
            new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz),
        };
        int[,] e =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
        };
        for (int i = 0; i < 12; i++)
            AddLine(c, m, v[e[i, 0]], v[e[i, 1]], col);
    }

    private static void WireSphere(DebugDrawCommandList c, Matrix m, float radius, Color4 col)
    {
        // Three great circles in orthogonal planes.
        EmitRing(c, m, radius, col, plane: 0);
        EmitRing(c, m, radius, col, plane: 1);
        EmitRing(c, m, radius, col, plane: 2);
    }

    private static void EmitRing(DebugDrawCommandList c, Matrix m, float radius, Color4 col, int plane)
    {
        for (int i = 0; i < SphereSegments; i++)
        {
            float a0 = i * 2f * MathF.PI / SphereSegments;
            float a1 = (i + 1) * 2f * MathF.PI / SphereSegments;
            AddLine(c, m, PointOnRing(radius, a0, plane), PointOnRing(radius, a1, plane), col);
        }
    }

    private static Vector3 PointOnRing(float r, float angle, int plane) => plane switch
    {
        0 => new Vector3(r * MathF.Cos(angle), r * MathF.Sin(angle), 0f), // XY
        1 => new Vector3(r * MathF.Cos(angle), 0f, r * MathF.Sin(angle)), // XZ
        _ => new Vector3(0f, r * MathF.Cos(angle), r * MathF.Sin(angle)), // YZ
    };

    private static void WireCylinder(DebugDrawCommandList c, Matrix m, float radius, float length, Color4 col)
    {
        float h = length * 0.5f;
        EmitRingAtY(c, m, radius, h, col);
        EmitRingAtY(c, m, radius, -h, col);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI * 0.5f;
            var p = new Vector3(radius * MathF.Cos(a), -h, radius * MathF.Sin(a));
            var q = new Vector3(radius * MathF.Cos(a), h, radius * MathF.Sin(a));
            AddLine(c, m, p, q, col);
        }
    }

    private static void WireCapsule(DebugDrawCommandList c, Matrix m, float radius, float length, Color4 col)
    {
        // Capsule and cylinder axis is Y (matches GeometricPrimitive.Capsule: deltaY = ±length/2).
        float h = length * 0.5f;
        EmitRingAtY(c, m, radius, h, col);
        EmitRingAtY(c, m, radius, -h, col);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI * 0.5f;
            var bottom = new Vector3(radius * MathF.Cos(a), -h, radius * MathF.Sin(a));
            var top = new Vector3(radius * MathF.Cos(a), h, radius * MathF.Sin(a));
            AddLine(c, m, bottom, top, col);
        }
        // Two hemisphere meridians (one per cap) to suggest the domes, in the XY plane.
        EmitDome(c, m, radius, h, col, sign: +1);
        EmitDome(c, m, radius, h, col, sign: -1);
    }

    private static void EmitRingAtY(DebugDrawCommandList c, Matrix m, float r, float y, Color4 col)
    {
        for (int i = 0; i < SphereSegments; i++)
        {
            float a0 = i * 2f * MathF.PI / SphereSegments;
            float a1 = (i + 1) * 2f * MathF.PI / SphereSegments;
            var p0 = new Vector3(r * MathF.Cos(a0), y, r * MathF.Sin(a0));
            var p1 = new Vector3(r * MathF.Cos(a1), y, r * MathF.Sin(a1));
            AddLine(c, m, p0, p1, col);
        }
    }

    private static void EmitDome(DebugDrawCommandList c, Matrix m, float r, float h, Color4 col, int sign)
    {
        // Quarter circle from ring (equator) to dome top, in the XY plane.
        int seg = SphereSegments / 2;
        for (int i = 0; i < seg; i++)
        {
            float t0 = i * (MathF.PI * 0.5f) / seg;
            float t1 = (i + 1) * (MathF.PI * 0.5f) / seg;
            var p0 = new Vector3(r * MathF.Cos(t0), sign * h + sign * r * MathF.Sin(t0), 0f);
            var p1 = new Vector3(r * MathF.Cos(t1), sign * h + sign * r * MathF.Sin(t1), 0f);
            AddLine(c, m, p0, p1, col);
        }
    }

    private static void WireTriangle(DebugDrawCommandList c, Matrix m, Vector3 a, Vector3 b, Vector3 d, Color4 col)
    {
        AddLine(c, m, a, b, col);
        AddLine(c, m, b, d, col);
        AddLine(c, m, d, a, col);
    }
}
