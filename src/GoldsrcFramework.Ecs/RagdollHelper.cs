using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Helper for creating ragdoll physics from an existing entity with a PhysicsController.
/// Full TempEnt-based ragdoll creation will be implemented later. For now, switches the
/// physics controller to dynamic mode.
/// </summary>
public static class RagdollHelper
{
    /// <summary>
    /// Creates a ragdoll for the given entity by switching its PhysicsController to dynamic mode.
    /// Full implementation will allocate a TEMPENTITY and set up skeletal physics.
    /// </summary>
    public static void CreateRagdollFor(Entity entity, PhysicsController controller)
    {
        controller.MotionType = PhysicsMotionType.Dynamic;

        // Switch authority to Stride so the physics simulation drives transforms.
        var link = entity.Get<GoldsrcTransformLinkComponent>();
        if (link is not null)
            link.Authority = TransformAuthority.Stride;

        controller.Enable();
    }
}