using Stride.Engine;
using StrideVector3 = Stride.Core.Mathematics.Vector3;
using StrideQuaternion = Stride.Core.Mathematics.Quaternion;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Lightweight script attached to ragdoll temp entities. Only performs <see cref="ILateUpdate.LateUpdate"/>,
/// deriving the entity transform from the physics skeleton so the ragdoll entity follows its bones.
/// Death/respawn logic lives on the original entity's <see cref="HalfLifeBehavior"/>.
/// </summary>
public sealed class RagdollBehavior : ScriptComponentBase, ILateUpdate
{
    private PhysicsController? physics;

    public override void Start()
    {
        physics = Entity.Get<PhysicsController>();
    }

    public void LateUpdate()
    {
        if (physics is null || !physics.IsEnabled || physics.IsNullSkeleton)
            return;

        // Derived from the pivot bone's physics pose, so it is up to date right after the step.
        var rootWorld = physics.GetModelRootTransform();

        // Write translation/rotation rather than the WorldMatrix field: assigning that field does not
        // update LocalMatrix, and the next TransformProcessor pass would rebuild it from stale data.
        // The ragdoll entity is a scene root (no parent), so its local transform is the world transform.
        rootWorld.Decompose(out StrideVector3 _, out StrideQuaternion rotation, out StrideVector3 position);
        Entity.Transform.Position = position;
        Entity.Transform.Rotation = rotation;
    }
}
