using System.Text;
using GoldsrcFramework.Content.Gpd;
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
/// The currently supported content types are:
/// - Brush model physics skeleton prefab. Its name is the model name, e.g. Get<Prefab>("*3").
/// - Studio model physics skeleton prefab. Its name is the model name with its extension replaced by ".gpd", e.g. Get<Prefab>("models/player/gordon.gpd").
/// </remarks>
public sealed unsafe class GoldsrcContentManager : IContentManager
{
    /// <summary>Hard cap on the model table scan, matching gsphysics' lookup table size.</summary>
    private const int MaxModelIndex = 1024;

    private readonly BspWorldGeometry worldGeometry = new();
    private readonly Dictionary<int, Prefab> brushPrefabs = [];
    private readonly HashSet<int> unusableBrushModels = [];

    /// <inheritdoc/>
    public bool IsExist(string key)
    {
        if (TryParseBrushKey(key, out int modelIndex))
            return GetBrushModel(modelIndex) is not null;

        // A studio model's physics data is its .gpd. A model the engine has not loaded yet reports
        // false without being remembered, so the caller retries once the map is up.
        return ResolveStudioPath(key, out var studioPath) && LoadStudioPhysicsAsset(studioPath) is not null;
    }

    /// <inheritdoc/>
    public T? Load<T>(string key) where T : class
    {
        if (typeof(T) == typeof(StudioModel))
            return LoadStudioModel(ResolveStudioModelName(key)) as T;

        if (typeof(T) != typeof(Prefab))
            return null;

        return LoadPrefab(key) as T;
    }

    /// <summary>
    /// Model name behind a studio key: the key itself when it already names a model, otherwise the
    /// model the key's physics data belongs to.
    /// </summary>
    /// <remarks>
    /// A studio key is either <c>models/barney.mdl</c> or <c>models/barney.gpd</c>, and only the
    /// former may be handed to the engine: naming the physics data takes the model loader through
    /// its format dispatch, which reports the file as an unknown format and loads nothing. A key
    /// that names neither (the engine may still resolve it on its own) is passed through unchanged.
    /// </remarks>
    private static string ResolveStudioModelName(string key)
        => ResolveStudioPath(key, out var path) ? path.Model : key;

    /// <summary>
    /// Loads the physics skeleton prefab of a brush model (<c>*&lt;index&gt;</c>) or of a studio
    /// model (<c>*.mdl</c> / <c>*.gpd</c>).
    /// </summary>
    private Prefab? LoadPrefab(string key)
    {
        if (TryParseBrushKey(key, out int modelIndex))
            return LoadBrushPrefab(modelIndex);

        return ResolveStudioPath(key, out var studioPath) ? LoadStudioPhysicsAsset(studioPath) : null;
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

        // Studio physics data is bound to the header of a model the engine may unload with the map,
        // so it is map-scoped as well.
        studioPhysics.Clear();
        unusableStudioModels.Clear();
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

    /// <summary>
    /// Loads a studio model by name, e.g. <c>"models/player/gordon.mdl"</c>, and wraps the engine's
    /// studio model header in a <see cref="StudioModel"/>. Returns null when the name does not
    /// resolve to a studio model.
    /// </summary>
    private static StudioModel? LoadStudioModel(string modelName)
    {
        // The engine stores a model name in a char[MAX_MODEL_NAME] buffer, so a longer name cannot
        // name a model.
        const int MaxModelName = 64;

        var studio = EngineApi.PStudio;
        if (studio == null || studio->Mod_ForName == null || studio->Mod_Extradata == null)
            return null;
        if (string.IsNullOrEmpty(modelName) || modelName.Length >= MaxModelName)
            return null;

        Span<byte> buffer = stackalloc byte[MaxModelName];
        int length = Encoding.ASCII.GetBytes(modelName, buffer);
        buffer[length] = 0;

        model_t* model;
        fixed (byte* pointer = buffer)
            model = studio->Mod_ForName(NCharPtr.From(pointer), 0);

        if (model == null || model->type != modtype_t.mod_studio)
            return null;

        var header = studio->Mod_Extradata(model);
        return header == null ? null : new StudioModel((studiohdr_t*)header);
    }

    // ── Studio model physics data (.gpd) ──────────────────────────────────────────

    /// <summary>
    /// Model name plus physics data path of one studio model. The two are kept together so a key in
    /// either form resolves to the same cache entry.
    /// </summary>
    private readonly record struct StudioPath(string Model, string Data);

    /// <summary>
    /// A studio model's physics skeleton prefab together with the model header checksum it was built
    /// for, so a model reloaded with different bytes at the same path invalidates the entry.
    /// </summary>
    private sealed record StudioPhysicsEntry(string Checksum, Prefab Prefab);

    /// <summary>Physics skeleton prefabs of studio models, keyed by model name.</summary>
    private readonly Dictionary<string, StudioPhysicsEntry> studioPhysics = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Studio models that are known to have no usable physics data for this map. Remembered so a
    /// model without a <c>.gpd</c> is not looked up and parsed again for every entity that uses it.
    /// </summary>
    private readonly HashSet<string> unusableStudioModels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the physics skeleton prefab of a studio model, loading, validating and caching it on
    /// first use.
    /// </summary>
    /// <returns>
    /// The prefab template, or null when the model has no usable physics data. A failure is only
    /// remembered when the model itself was available: a model the engine has not loaded yet is
    /// reported as missing without being cached, so the next call retries.
    /// </returns>
    private Prefab? LoadStudioPhysicsAsset(StudioPath path)
    {
        if (studioPhysics.TryGetValue(path.Model, out var cached))
        {
            // The prefab is only valid for the header it was authored against.
            if (TryReadStudioChecksum(path.Model, out string liveChecksum) &&
                GpdFile.ChecksumMatches(cached.Checksum, liveChecksum))
            {
                return cached.Prefab;
            }

            studioPhysics.Remove(path.Model);
        }

        if (unusableStudioModels.Contains(path.Model))
            return null;

        var studio = LoadStudioModel(path.Model);
        if (studio is null)
            return null; // not a loaded studio model — retry later

        if (!GpdFile.TryComputeChecksum(studio.Header, out string checksum))
            return null; // the header is unreadable right now — retry later

        if (!GpdFile.TryRead(path.Data, out byte[] xml))
        {
            Print($"[gsf] studio physics: {path.Model} has no {path.Data}");
            unusableStudioModels.Add(path.Model);
            return null;
        }

        if (!GpdParser.TryParse(xml, out var asset, out string parseError))
        {
            Print($"[gsf] studio physics: {path.Data} is invalid: {parseError}");
            unusableStudioModels.Add(path.Model);
            return null;
        }

        if (!GpdFile.ChecksumMatches(asset!.Checksum, checksum))
        {
            Print($"[gsf] studio physics: {path.Data} was authored against a different model");
            unusableStudioModels.Add(path.Model);
            return null;
        }

        var prefab = StudioPhysicsPrefabFactory.TryCreate(asset, studio.Header, out string buildError);
        if (prefab is null)
        {
            Print($"[gsf] studio physics: {path.Data} cannot be built: {buildError}");
            unusableStudioModels.Add(path.Model);
            return null;
        }

        studioPhysics[path.Model] = new StudioPhysicsEntry(checksum, prefab);

        // One line per model per map, mirroring the brush diagnostic above.
        Print($"[gsf] studio physics: {path.Model} -> {asset.RigidBodies.Length} bodies, " +
              $"{asset.Constraints.Length} constraints, {studio.Header->numbones} bones");

        return prefab;
    }

    /// <summary>
    /// Checksum of the studio header the engine currently has loaded for a model, if it can be read.
    /// </summary>
    private bool TryReadStudioChecksum(string modelPath, out string checksum)
    {
        checksum = string.Empty;

        var studio = LoadStudioModel(modelPath);
        return studio is not null && GpdFile.TryComputeChecksum(studio.Header, out checksum);
    }

    /// <summary>
    /// Resolves a studio model key to the model name and the physics data path.
    /// </summary>
    /// <remarks>
    /// Both spellings are accepted so callers can use whichever name they already hold:
    /// <c>models/barney.mdl</c> and <c>models/barney.gpd</c> resolve to the same pair. Player models
    /// keep the legacy layout (<c>models/player.mdl</c> and
    /// <c>models/player/gordon/gordon.mdl</c>) because this only swaps the extension.
    /// </remarks>
    private static bool ResolveStudioPath(string key, out StudioPath path)
    {
        path = default;

        if (string.IsNullOrEmpty(key) || key.Length <= 4)
            return false;

        // The engine resolves these names inside its search path, so a drive or a directory traversal
        // must never reach it.
        if (key.Contains("..", StringComparison.Ordinal) || key.Contains(':') || key[0] is '/' or '\\')
            return false;

        ReadOnlySpan<char> name = key;
        if (name.EndsWith(".gpd", StringComparison.OrdinalIgnoreCase))
        {
            path = new StudioPath(key[..^4] + ".mdl", key);
            return true;
        }

        if (name.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
        {
            path = new StudioPath(key, key[..^4] + ".gpd");
            return true;
        }

        return false;
    }
}
