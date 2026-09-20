using Stride.Engine;
using Stride.Engine.Processors;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Auto-registers <see cref="ScriptComponentBase"/> instances that implement
/// <see cref="ILateUpdate"/> with <see cref="LateUpdateScriptSystem"/>.
/// Runs at <see cref="EntityProcessor.Order"/> = <see cref="int.MaxValue"/>
/// so its <see cref="Update"/> executes after all other processors.
/// </summary>
internal sealed class LateUpdateScriptProcessor : EntityProcessor<ScriptComponentBase>
{
    private LateUpdateScriptSystem? lateUpdateSystem;

    public LateUpdateScriptProcessor()
    {
        Order = int.MaxValue;
    }

    protected override void OnSystemAdd()
    {
        lateUpdateSystem = Services.GetService<LateUpdateScriptSystem>();
    }

    protected override void OnEntityComponentAdding(Entity entity, ScriptComponentBase component, ScriptComponentBase data)
    {
        if (component is ILateUpdate late)
            lateUpdateSystem?.Register(late);
    }

    protected override void OnEntityComponentRemoved(Entity entity, ScriptComponentBase component, ScriptComponentBase data)
    {
        if (component is ILateUpdate late)
            lateUpdateSystem?.Unregister(late);
    }
}