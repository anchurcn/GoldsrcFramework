using System.Runtime.InteropServices;
using GoldsrcFramework.Engine.Native;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Graphics.Data;

namespace GoldsrcFramework.Content;

/// <summary>
/// Position-only vertex layout used by runtime brush collision meshes.
/// </summary>
/// <remarks>
/// Stride's own <c>VertexPosition3</c> is <c>internal</c> to Stride.BepuPhysics, so the framework
/// declares its own equivalent. Stride.BepuPhysics reads the mesh through the POSITION semantic
/// only, so a single <see cref="Vector3"/> per vertex is enough.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct BspVertex
{
    public Vector3 Position;

    public static readonly VertexDeclaration Declaration = new(VertexElement.Position<Vector3>());

    public BspVertex(Vector3 position)
    {
        Position = position;
    }
}

/// <summary>
/// The shared vertex pool of a loaded map, plus the triangle fan triangulation of individual brush
/// models against it.
/// </summary>
/// <remarks>
/// <para>
/// In GoldSrc every brush model (the world model and all <c>*N</c> submodels) is a struct copy of the
/// world model that only renames itself and narrows <c>firstmodelsurface</c>/<c>nummodelsurfaces</c>
/// (xash3d <c>Mod_SetupSubmodels</c>: <c>*submod = *mod;</c>). The <c>vertexes</c>, <c>edges</c>,
/// <c>surfedges</c> and <c>surfaces</c> arrays are therefore shared by every brush model, so the
/// vertex pool is built once per map and indexed by all of them. This mirrors gsphysics, which fills
/// a single <c>_vertexPool</c> from model index 1 and reuses it for every brush model.
/// </para>
/// <para>
/// Vertex positions are used verbatim, in GoldSrc map units. The engine draws a brush entity by
/// transforming those world-space positions with the entity origin/angles, so the mesh itself needs
/// no offset or scale.
/// </para>
/// </remarks>
internal sealed class BspWorldGeometry
{
    /// <summary>Model index of the world model (<c>sv.models[1] = sv.worldmodel</c>).</summary>
    public const int WorldspawnModelIndex = 1;

    private BspVertex[] vertices = [];

    public VertexBufferBinding Binding { get; private set; }

    public bool IsReady { get; private set; }

    /// <summary>Number of vertices in the shared pool, 0 before the map is read.</summary>
    public int VertexCount => vertices.Length;

    /// <summary>
    /// Copies the world vertex pool of <paramref name="worldModel"/> into a GPU-free Stride buffer.
    /// Returns false (and stays not-ready) when the model is not usable yet.
    /// </summary>
    public unsafe bool TryBuild(model_t* worldModel)
    {
        if (IsReady)
            return true;

        if (worldModel == null || worldModel->vertexes == null || worldModel->numvertexes <= 0)
            return false;

        int count = worldModel->numvertexes;
        var copied = new BspVertex[count];
        for (int i = 0; i < count; i++)
        {
            var position = worldModel->vertexes[i].position;
            copied[i] = new BspVertex(new Vector3(position.X, position.Y, position.Z));
        }

        // A "fake" buffer that carries CPU content and no graphics resource at all. Stride's
        // ShapeCacheSystem reads the attached BufferData directly, so no GraphicsDevice is involved.
        var bufferData = new BufferData(
            BufferFlags.VertexBuffer,
            MemoryMarshal.AsBytes(copied.AsSpan()).ToArray());

        vertices = copied;
        Binding = new VertexBufferBinding(
            bufferData.ToSerializableVersion(),
            BspVertex.Declaration,
            count);
        IsReady = true;
        return true;
    }

    public void Reset()
    {
        vertices = [];
        Binding = default;
        IsReady = false;
    }

    /// <summary>
    /// Triangulates the render surfaces of a brush model as a triangle fan and returns the vertex
    /// indices referencing <see cref="Binding"/>'s pool, or null when the model has no usable
    /// geometry.
    /// </summary>
    /// <remarks>
    /// Ported from gsphysics <c>GetTriangleIndeces</c>. A surfedge is signed: a positive index uses
    /// <c>edges[i].v[0]</c> and a negative one uses <c>edges[-i].v[1]</c>. That winding is what the
    /// renderer uses, so it is preserved rather than "fixed". Malformed BSP data (out of range
    /// indices, degenerate triangles) is dropped instead of trusted.
    /// </remarks>
    /// 
    /*
     * 
     
            if (!IsReady)
            return null;
        if (model == null || model->type != modtype_t.mod_brush)
            return null;

        const int MODEL_QBSP2 = 1 << 28;

        var surfaces = model->surfaces;
        var surfEdges = model->surfedges;
        if (surfaces == null || surfEdges == null)
            return null;

        // xash3d uses a union for edges: edges16 (unsigned short v[2]) for standard BSP,
        // edges32 (unsigned int v[2]) for QBSP2. Both share the same offset for v[0]/v[1].
        bool use32 = (model->flags & MODEL_QBSP2) != 0;
        var edges16 = model->edges_union.edges16;
        var edges32 = model->edges_union.edges32;
        if ((!use32 && edges16 == null) || (use32 && edges32 == null))
            return null;

        int firstSurface = model->firstmodelsurface;
        int numSurfaces = model->nummodelsurfaces;
        int numSurfEdges = model->numsurfedges;

        if (numSurfaces <= 0 || firstSurface < 0)
    */
    public unsafe int[]? Triangulate(model_t* model)
    {
        if (!IsReady)
            return null;
        if (model == null || model->type != modtype_t.mod_brush)
            return null;

        var surfaces = model->surfaces;
        var edges = model->edges;
        var surfEdges = model->surfedges;
        if (surfaces == null || edges == null || surfEdges == null)
            return null;

        int firstSurface = model->firstmodelsurface;
        int numSurfaces = model->nummodelsurfaces;
        int numSurfEdges = model->numsurfedges;

        if (numSurfaces <= 0 || firstSurface < 0)
            return null;
        if (firstSurface > model->numsurfaces - numSurfaces)
            return null;
        if (model->numvertexes <= 0 || model->numvertexes > vertices.Length)
            return null;

        var indices = new List<int>(numSurfaces * 3);

        for (int face = 0; face < numSurfaces; face++)
        {
            var surface = surfaces + firstSurface + face;
            int numEdges = surface->numedges;
            int firstEdge = surface->firstedge;

            if (numEdges < 3 || firstEdge < 0 || firstEdge > numSurfEdges - numEdges)
                continue;

            if (!TryGetVertex(model, edges, surfEdges[firstEdge], out int v0))
                continue;
            if (!TryGetVertex(model, edges, surfEdges[firstEdge + 1], out int v1))
                continue;

            for (int e = 2; e < numEdges; e++)
            {
                if (!TryGetVertex(model, edges, surfEdges[firstEdge + e], out int v2))
                    break;

                if (!IsDegenerate(v0, v1, v2))
                {
                    indices.Add(v0);
                    indices.Add(v1);
                    indices.Add(v2);
                }

                v1 = v2; // fan advance
            }
        }

        return indices.Count >= 3 ? indices.ToArray() : null;
    }

    /// <summary>Axis aligned bounds of the vertices referenced by <paramref name="indices"/>.</summary>
    public BoundingBox ComputeBounds(int[] indices)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (int index in indices)
        {
            var position = vertices[index].Position;
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        return new BoundingBox(min, max);
    }

    private unsafe bool TryGetVertex(model_t* model, medge_t* edges, int signedEdge, out int vertex)
    {
        vertex = -1;

        if (signedEdge == 0)
            return false;

        int edgeIndex = signedEdge > 0 ? signedEdge : -signedEdge;
        if (edgeIndex >= model->numedges)
            return false;

        int index = signedEdge > 0 ? edges[edgeIndex].v[0] : edges[edgeIndex].v[1];
        if (index < 0 || index >= model->numvertexes || index >= vertices.Length)
            return false;

        vertex = index;
        return true;
    }

    private bool IsDegenerate(int a, int b, int c)
    {
        var pa = vertices[a].Position;
        var cross = Vector3.Cross(vertices[b].Position - pa, vertices[c].Position - pa);
        if (!float.IsFinite(cross.X) || !float.IsFinite(cross.Y) || !float.IsFinite(cross.Z))
            return true;

        return cross.LengthSquared() < 1e-8f;
    }
}
