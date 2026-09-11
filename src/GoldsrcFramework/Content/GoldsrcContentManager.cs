using System.Text;
using GoldsrcFramework.Ecs;
using GoldsrcFramework.Engine.Native;
using NativeInterop;
using Stride.Engine;

namespace GoldsrcFramework.Content;

/// <summary>
/// Loads GoldSrc physics resources into Stride assets.
/// </summary>
/// <remarks>
/// <para>
/// Implements the brush model part of <see cref="IContentManager"/>: a key of the form
/// <c>*&lt;modelIndex&gt;</c> resolves to a <see cref="Prefab"/> holding one physics bone with a
/// triangle mesh collider built from the BSP surfaces of that model. Worldspawn is model index 1
/// (the world model itself); every other index is a <c>*N</c> submodel used by a moving brush
/// entity.
/// </para>
/// <para>
/// This type lives in <c>GoldsrcFramework</c> rather than <c>GoldsrcFramework.Ecs</c> because it
/// needs <see cref="EngineApi"/>; the dependency direction is
/// <c>GoldsrcFramework -&gt; GoldsrcFramework.Ecs</c>, so putting it in the ECS assembly would
/// create a cycle.
/// </para>
/// <para>
/// Model handles are read through <c>hudGetModelByIndex</c>. All native data is consumed inside the
/// unsafe triangulation and never escapes it: results are managed vertex/index arrays.
/// </para>
/// </remarks>
public sealed unsafe class GoldsrcContentManager : IContentManager
{
    /// <summary>Hard cap on the model table scan, matching gsphysics' lookup table size.</summary>
    private const int MaxModelIndex = 1024;

    private static readonly int[] BrushBoneParents = [-1];

    private readonly BspWorldGeometry worldGeometry = new();
    private readonly Dictionary<int, Prefab> brushPrefabs = [];
    private readonly HashSet<int> unusableBrushModels = [];

    /// <inheritdoc/>
    public bool IsExist(string key)
    {
        if (!TryParseBrushKey(key, out int modelIndex))
            return false;

        return GetBrushModel(modelIndex) is not null;
    }

    /// <inheritdoc/>
    public T? Load<T>(string key) where T : class
    {
        if (typeof(T) != typeof(Prefab))
            return null;

        return LoadBrushPrefab(key) as T;
    }

    /// <inheritdoc/>
    public int[] GetStudioBoneParents(string key)
    {
        // A brush model has a single implicit root bone. The studio hierarchy is a separate part.
        return [.. BrushBoneParents];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Mirrors gsphysics <c>PhysicsAssetManager</c>: walk the engine model table from index 1 until
    /// the engine reports no model and build the prefab of every brush model found.
    /// </remarks>
    public void PreloadBrushModels()
    {
        var engine = EngineApi.PClient;
        if (engine == null || engine->hudGetModelByIndex == null)
            return;

        int scanned = 0;
        for (int modelIndex = 1; modelIndex <= MaxModelIndex; modelIndex++)
        {
            if (engine->hudGetModelByIndex(modelIndex) == null)
                break; // end of the model table

            scanned = modelIndex;
            LoadBrushPrefab(modelIndex);
        }

        // One line per map so it is obvious from the console whether the brush pipeline ran, how big
        // the shared vertex pool is and whether the static world was built.
        PrintDiagnostics(scanned);
    }

    private void PrintDiagnostics(int scannedModels)
    {
        string worldState = brushPrefabs.ContainsKey(BspWorldGeometry.WorldspawnModelIndex)
            ? "worldspawn ok"
            : "worldspawn MISSING";

        Print($"[gsf] brush physics: {brushPrefabs.Count} prefabs / {scannedModels} models scanned, " +
              $"{worldGeometry.VertexCount} shared vertices, {worldState}");
    }

    private static void Print(string message)
    {
        var engine = EngineApi.PClient;
        if (engine == null || engine->Con_Printf == null)
            return;

        const int MaxLength = 500;
        if (message.Length > MaxLength)
            message = message[..MaxLength];

        Span<byte> buffer = stackalloc byte[MaxLength + 1];
        int length = Encoding.ASCII.GetBytes(message, buffer);
        buffer[length] = 0;

        fixed (byte* pointer = buffer)
            engine->Con_Printf((NChar*)pointer);
    }

    /// <inheritdoc/>
    public void NewMap()
    {
        brushPrefabs.Clear();
        unusableBrushModels.Clear();
        worldGeometry.Reset();
    }

    private Prefab? LoadBrushPrefab(string key)
    {
        return TryParseBrushKey(key, out int modelIndex) ? LoadBrushPrefab(modelIndex) : null;
    }

    private Prefab? LoadBrushPrefab(int modelIndex)
    {
        if (brushPrefabs.TryGetValue(modelIndex, out var cached))
            return cached;

        if (unusableBrushModels.Contains(modelIndex))
            return null;

        var model = GetBrushModel(modelIndex);
        if (model is null)
            return null; // not a brush model — retry later, the model table may not be populated yet

        // Every brush model shares the world model's vertex pool, so build it once from index 1.
        if (!worldGeometry.TryBuild(GetBrushModel(BspWorldGeometry.WorldspawnModelIndex)))
            return null; // map not ready yet — retry later

        var indices = worldGeometry.Triangulate(model);
        if (indices is null)
        {
            // The model is a brush model but carries no usable surfaces. That does not change for
            // the lifetime of the map, so remember it instead of re-triangulating every frame.
            unusableBrushModels.Add(modelIndex);
            return null;
        }

        var prefab = BrushPrefabFactory.TryCreate(
            indices,
            worldGeometry.Binding,
            worldGeometry.ComputeBounds(indices),
            asStatic: modelIndex == BspWorldGeometry.WorldspawnModelIndex);

        if (prefab is null)
            unusableBrushModels.Add(modelIndex);
        else
            brushPrefabs.Add(modelIndex, prefab);

        return prefab;
    }

    private static unsafe model_t* GetBrushModel(int modelIndex)
    {
        var engine = EngineApi.PClient;
        if (engine == null || engine->hudGetModelByIndex == null || modelIndex < 1)
            return null;

        var model = engine->hudGetModelByIndex(modelIndex);
        return model != null && model->type == modtype_t.mod_brush ? model : null;
    }

    private static bool TryParseBrushKey(string key, out int modelIndex)
    {
        modelIndex = 0;
        if (string.IsNullOrEmpty(key) || key.Length < 2 || key[0] != '*')
            return false;

        return int.TryParse(key.AsSpan(1), out modelIndex) && modelIndex >= 1;
    }
}
