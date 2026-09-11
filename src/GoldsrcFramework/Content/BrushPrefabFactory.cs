using System.Runtime.InteropServices;
using GoldsrcFramework.Ecs;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Graphics.Data;
using Stride.Rendering;

namespace GoldsrcFramework.Content;

/// <summary>
/// Builds the physics skeleton <see cref="Prefab"/> of a brush model from a triangle mesh.
/// </summary>
/// <remarks>
/// <para>
/// A brush model has a single implicit root bone sitting at the model origin, so the prefab holds
/// one bone entity (<c>PhysicsBone_0</c>) whose local transform stays identity: the mesh vertices
/// already are in map/world space and the root entity carries the origin/angles transform.
/// </para>
/// <para>
/// The worldspawn gets a <see cref="StaticComponent"/>; a moving brush entity (door, platform,
/// <c>func_wall</c>, ...) gets a kinematic <see cref="BodyComponent"/> that
/// <see cref="PhysicsController.SetPose"/> teleports to the entity transform every frame.
/// </para>
/// <para>
/// Both buffers are GPU-free "fake" buffers created through
/// <see cref="GraphicsSerializerExtensions.ToSerializableVersion(BufferData)"/>: Stride's
/// <c>ShapeCacheSystem</c> reads the attached <see cref="BufferData"/> content directly instead of
/// downloading the data from a graphics device, which is what makes runtime mesh colliders possible
/// without a rendering pipeline.
/// </para>
/// </remarks>
internal static class BrushPrefabFactory
{
    public static Prefab? TryCreate(
        int[] indices,
        VertexBufferBinding vertexBinding,
        in BoundingBox bounds,
        bool asStatic)
    {
        if (indices.Length < 3)
            return null;

        var indexBuffer = new BufferData(
            BufferFlags.IndexBuffer,
            MemoryMarshal.AsBytes(indices.AsSpan()).ToArray());

        var meshDraw = new MeshDraw
        {
            PrimitiveType = PrimitiveType.TriangleList,
            DrawCount = indices.Length,
            VertexBuffers = [vertexBinding],
            IndexBuffer = new IndexBufferBinding(indexBuffer.ToSerializableVersion(), is32Bit: true, indices.Length),
        };

        var model = new Model();
        model.Meshes.Add(new Mesh { Draw = meshDraw, BoundingBox = bounds });
        model.BoundingBox = bounds;

        // MeshCollider reports CenterOfMass == 0, so a bone's physics pose matches its entity
        // transform exactly and no compensation is needed when teleporting it.
        var collider = new MeshCollider { Model = model, Mass = 1f };

        var bone = new Entity("PhysicsBone_0");
        bone.Components.Add(new BoneLink { StudioBoneIndex = 0, IsAddon = false });

        if (asStatic)
        {
            bone.Components.Add(new StaticComponent { Collider = collider });
        }
        else
        {
            bone.Components.Add(new BodyComponent { Collider = collider, Kinematic = true });
        }

        var prefab = new Prefab();
        prefab.Entities.Add(bone);
        return prefab;
    }
}
