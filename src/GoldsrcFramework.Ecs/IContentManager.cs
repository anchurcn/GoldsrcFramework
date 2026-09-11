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
    /// Returns the studio bone hierarchy of a model: for every studio bone, its parent bone index
    /// (-1 for a root bone). Brush models have a single implicit root bone, so they return <c>[-1]</c>.
    /// </summary>
    /// <remarks>
    /// Returns an empty array when the hierarchy is unknown. Skeletons still work, but bones without a
    /// rigid body can then only be anchored to the pivot bone instead of their real parent chain.
    /// </remarks>
    int[] GetStudioBoneParents(string key);

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
