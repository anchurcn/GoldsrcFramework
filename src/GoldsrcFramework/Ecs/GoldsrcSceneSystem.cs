using GoldsrcFramework.Ecs.Diagnostics;
using Stride.Core;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Runs a Stride scene without creating a Stride window or rendering pipeline.
/// </summary>
public sealed class GoldsrcSceneSystem : GameSystemBase
{
    public const int DefaultUpdateOrder = -100;

    public GoldsrcSceneSystem(IServiceRegistry services)
        : base(services)
    {
        Enabled = true;
        Visible = true;
        UpdateOrder = DefaultUpdateOrder;

        RootScene = new Scene();
        SceneInstance = new SceneInstance(services, RootScene);
        SceneInstance.Processors.Add(new GoldsrcScriptProcessor());
        // NOTE: TransformProcessor is intentionally NOT pre-registered here. It carries
        // [DefaultEntityComponentProcessor(typeof(TransformProcessor))] on TransformComponent, so it
        // is auto-registered on demand. Pre-registering it changes the timing of EntityManager.Add:
        // the first root entity with children is then processed while TransformProcessor is already
        // active, and its OnEntityComponentAdding re-enters InternalAddEntity for the child before the
        // parent's TransformComponent entry is inserted into MapComponentTypeToProcessors, which throws
        // "An item with the same key has already been added. Key: TransformComponent".
        SceneInstance.Processors.Add(new LateUpdateScriptProcessor());

        // Physics debug draw: collects collider wireframes each Draw pass; the host
        // flushes them through pTriAPI at HUD_DrawNormalTriangles. Handles only
        // CollidableComponent (not TransformComponent), so it does not trigger the
        // TransformProcessor reentrancy warned about above.
        PhysicsDebug = new PhysicsDebugDrawProcessor();
        SceneInstance.Processors.Add(PhysicsDebug);
    }

    public Scene RootScene { get; }

    public SceneInstance SceneInstance { get; private set; }

    public PhysicsDebugDrawProcessor PhysicsDebug { get; private set; }

    public override void Update(GameTime gameTime)
    {
        SceneInstance.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        // The processors used by the headless runtime do not dereference RenderContext.
        SceneInstance.Draw(null!);
    }

    protected override void Destroy()
    {
        if (SceneInstance is not null)
        {
            ((IReferencable)SceneInstance).Release();
            SceneInstance = null!;
        }

        base.Destroy();
    }
}
