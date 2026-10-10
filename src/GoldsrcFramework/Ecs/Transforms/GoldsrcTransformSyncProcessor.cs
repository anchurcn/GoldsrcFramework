using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Owns the set of <see cref="GoldsrcTransformLinkComponent"/> instances in a scene instance.
/// </summary>
/// <remarks>
/// <para>
/// This type is about the set, not the sync: <see cref="GoldsrcTransformSyncSystem"/> still runs the two
/// passes, but it no longer walks the scene to find out what to work on. The component names this
/// processor through <see cref="Stride.Engine.Design.DefaultEntityComponentProcessorAttribute"/>, so the
/// entity manager creates it as soon as a link enters the scene, and from then on adds and removes
/// entries as links are attached and detached. A link attached to an entity that is already in the scene
/// is therefore picked up as well, which a listener on the scene's entity collection could not see.
/// </para>
/// <para>
/// The set is <see cref="EntityProcessor{TComponent,TData}.ComponentDatas"/>, so there is no second copy
/// to keep in step, and every entry leaves through the same place it came in.
/// </para>
/// </remarks>
public sealed class GoldsrcTransformSyncProcessor : EntityProcessor<GoldsrcTransformLinkComponent>
{
    /// <summary>
    /// The links currently in the scene - the components whose authority decides which way the sync
    /// goes. Empty while the scene holds none.
    /// </summary>
    public IReadOnlyCollection<GoldsrcTransformLinkComponent> Links => ComponentDatas.Keys;
}
