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
    /// Loads a strongly-typed resource. T may be <see cref="Stride.Engine.Model"/> (visual)
    /// or <see cref="Prefab"/> (physics skeleton template).
    /// Call <see cref="IsExist"/> first to confirm the resource exists.
    /// </summary>
    T? Load<T>(string key) where T : class;

    /// <summary>
    /// Pre-caches all brush model physics prefabs during map load.
    /// </summary>
    void PreloadBrushModels(object? nativeMapData);
}
