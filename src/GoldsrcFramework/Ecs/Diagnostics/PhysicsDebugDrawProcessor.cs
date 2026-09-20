using Stride.BepuPhysics;
using Stride.Engine;
using Stride.Rendering;

namespace GoldsrcFramework.Ecs.Diagnostics;

/// <summary>
/// Collects collider wireframes every Draw pass into <see cref="Commands"/>, to be
/// flushed by the host at <c>HUD_DrawNormalTriangles</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EntityProcessor{T}"/> matching is inheritance-aware
/// (<c>Accept =&gt; mainTypeInfo.IsAssignableFrom(type)</c>), so this single processor
/// automatically receives <c>BodyComponent</c>, <c>StaticComponent</c> and
/// <c>CharacterComponent</c> (all <c>CollidableComponent</c>) without a dummy component.
/// The set is kept in sync by <see cref="ComponentDatas"/> as entities are added/removed.
/// </para>
/// <para>
/// <see cref="Draw"/> runs inside <c>SceneInstance.Draw(null!)</c>, which GSF calls from
/// <c>GoldsrcSceneSystem.Draw</c> during <c>HUD_TempEntUpdate</c>. Physics poses are final
/// by then (the simulation stepped earlier in the same Update pass), and nothing steps
/// physics again before <c>HUD_DrawNormalTriangles</c>, so the collected geometry is
/// pose-accurate for the frame. The null <see cref="RenderContext"/> is never dereferenced.
/// </para>
/// </remarks>
public sealed class PhysicsDebugDrawProcessor : EntityProcessor<CollidableComponent>
{
    public PhysicsDebugDrawProcessor()
    {
        // After TransformProcessor(-200); matches Stride's Bepu debug processor order.
        Order = 1000;
    }

    public DebugDrawCommandList Commands { get; } = new();

    // TData == TComponent: we carry the component itself as its associated data.
    protected override CollidableComponent GenerateComponentData(Entity entity, CollidableComponent component)
        => component;

    public override void Draw(RenderContext context)
    {
        Commands.Clear();
        foreach (var (collidable, _) in ComponentDatas)
            ColliderWireframeBuilder.Emit(Commands, collidable);
    }
}
