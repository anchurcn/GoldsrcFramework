using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Loads strongly-typed GoldSrc content resources.
/// Implemented by GoldsrcContentManager (brush model and studio model parts).
/// </summary>
public interface IContentManager
{
    /// <summary>
    /// Checks whether a physics resource exists for the given key.
    /// For studio models, this verifies the .gpd file exists and its checksum matches.
    /// </summary>
    bool IsExist(string key);

    /// <summary>
    /// Loads a strongly-typed resource. T may be <see cref="Stride.Engine.Model"/> (visual),
    /// <see cref="Prefab"/> (physics skeleton template) or
    /// <see cref="GoldsrcFramework.Engine.Native.StudioModel"/> (studio model header, by model name).
    /// Call <see cref="IsExist"/> first to confirm the resource exists.
    /// </summary>
    T? Load<T>(string key) where T : class;

    /// <summary>
    /// Pre-caches the physics prefabs of every brush model of the current map.
    /// </summary>
    /// <remarks>
    /// The native model list is read by the implementation itself, so no native data is passed
    /// through this interface. Only prefab templates are built here; no entity is instantiated.
    /// </remarks>
    void PreloadBrushModels();

    /// <summary>
    /// Drops every resource that is scoped to the current map. Must be called on map change, before
    /// the scene is rebuilt for the new map.
    /// </summary>
    void NewMap();
}
