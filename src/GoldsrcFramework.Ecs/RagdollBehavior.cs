using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Lightweight script attached to ragdoll temp entities. Only performs LateUpdate
/// to extract the root entity transform from the pivot physics bone.
/// Does not handle death/respawn logic — that lives on the original entity's
/// <see cref="HalfLifeBehavior"/>.
/// </summary>
public sealed class RagdollBehavior : ScriptComponentBase, ILateUpdate
{
    private PhysicsController? phys;

    public override void Start()
    {
        phys = Entity.Get<PhysicsController>();
    }

    public void LateUpdate()
    {
        if (phys is null || phys.IsNullSkeleton || phys.PivotPhysicsBone is null)
            return;

        phys.ApplyRootTransformFromPivot(Entity);
    }
}
