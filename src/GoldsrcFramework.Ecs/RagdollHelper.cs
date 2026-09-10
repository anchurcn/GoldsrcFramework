using GoldsrcFramework.LinearMath;
using Stride.Engine;
using Stride.Engine.Design;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Helper for creating ragdoll physics from an existing entity with a PhysicsController.
/// Creates an independent temp entity (Dynamic) by cloning the original's physics skeleton,
/// then detaches the original's skeleton so it becomes a NullPhysicsSkeleton.
/// </summary>
public static class RagdollHelper
{
    /// <summary>
    /// Creates a ragdoll temp entity for the given entity.
    /// Clones the original's physics bones, creates a Dynamic PhysicsController,
    /// initializes the pose, adds the entity to the scene, and detaches the original's skeleton.
    /// </summary>
    /// <param name="originalEntity">The entity that died.</param>
    /// <param name="initialPose">The current animation pose to initialize the ragdoll with.</param>
    public static void CreateRagdollFor(Entity originalEntity, ReadOnlySpan<Matrix3x4> initialPose)
    {
        var physController = originalEntity.Get<PhysicsController>();
        if (physController is null || physController.IsNullSkeleton || physController.RagdollRigged)
            return;

        // 1. Clone physics bones (deep clone includes components, children)
        var clonedBones = new List<Entity>(physController.PhysicsBones.Count);
        foreach (var bone in physController.PhysicsBones)
        {
            var cloned = EntityCloner.Clone(bone);
            clonedBones.Add(cloned);
        }

        // 2. Create temp ragdoll entity
        int entindex = originalEntity.Get<Components.ClEntityComponent>()?.Index ?? -1;
        string modelName = physController.IsPlayer ? physController.ModelName ?? "unknown" : "model";
        var ragdollEntity = new Entity($"Ragdoll@{entindex}(\"{modelName}\")");

        foreach (var bone in clonedBones)
            bone.Transform.Parent = ragdollEntity.Transform;

        // 3. Create Dynamic PhysicsController
        var ragdollPhys = new PhysicsController
        {
            MotionType = PhysicsMotionType.Dynamic
        };
        ragdollEntity.Components.Add(ragdollPhys);

        if (physController.IsPlayer)
            ragdollPhys.LoadSkeleton(clonedBones, physController.ModelName!, true);
        else
            ragdollPhys.LoadSkeleton(clonedBones, physController.ModelPointer, false);

        // 4. Initialize pose from current animation
        ragdollPhys.SetPose(initialPose);

        // 5. Add lightweight behavior for LateUpdate root transform extraction
        ragdollEntity.Components.Add(new RagdollBehavior());

        // 6. Add to scene (emit as effect)
        var rootScene = GetRootScene(originalEntity);
        rootScene?.Entities.Add(ragdollEntity);

        // 7. Detach original entity's skeleton
        physController.DetachSkeleton();
    }

    private static Scene? GetRootScene(Entity entity)
    {
        var scene = entity.Scene;
        if (scene is null)
            return null;
        while (scene.Parent is not null)
            scene = scene.Parent;
        return scene;
    }
}
