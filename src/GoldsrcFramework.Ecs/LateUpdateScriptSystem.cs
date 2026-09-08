using Stride.Core;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Runs <see cref="ILateUpdate.LateUpdate"/> on all registered scripts after all
/// <see cref="EntityProcessor"/> instances have finished. Registered in
/// <see cref="GoldsrcClientGame"/>'s <see cref="IGameSystemCollection"/> with
/// <see cref="GameSystemBase.UpdateOrder"/> = <see cref="int.MaxValue"/> so it
/// executes after <see cref="GoldsrcSceneSystem"/> and all other systems.
/// </summary>
public sealed class LateUpdateScriptSystem : GameSystemBase
{
    private readonly HashSet<ILateUpdate> scripts = [];

    public LateUpdateScriptSystem(IServiceRegistry registry) : base(registry)
    {
        Enabled = true;
        UpdateOrder = int.MaxValue;
    }

    internal void Register(ILateUpdate script) => scripts.Add(script);
    internal void Unregister(ILateUpdate script) => scripts.Remove(script);

    public override void Update(GameTime gameTime)
    {
        foreach (var script in scripts)
            script.LateUpdate();
    }
}