using Stride.Core;
using Stride.Engine;
using Stride.Engine.Processors;
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
        SceneInstance.Processors.Add(new TransformProcessor());
        SceneInstance.Processors.Add(new LateUpdateScriptProcessor());
    }

    public Scene RootScene { get; }

    public SceneInstance SceneInstance { get; private set; }

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
