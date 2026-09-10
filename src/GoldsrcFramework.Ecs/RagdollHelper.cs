using GoldsrcFramework.LinearMath;
using Stride.Engine;
using Stride.Engine.Design;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Helper for creating a ragdoll from an entity that owns a <see cref="PhysicsController"/>.
/// Clones the original entity's physics bones into an independent dynamic temp entity. The original
/// entity's skeleton is left untouched; the caller is expected to <see cref="PhysicsController.Disable"/>
/// it afterwards (see <see cref="HalfLifeBehavior"/>).
/// </summary>
public static class RagdollHelper
{
    /// <summary>
    /// Creates a ragdoll temp entity for the given entity and adds it to the same root scene.
    /// </summary>
    /// <param name="originalEntity">The entity whose physics skeleton is cloned.</param>
    /// <param name="modelName">Optional model name, only used to name the temp entity.</param>
    /// <param name="initialPose">The current animation pose used to initialize the ragdoll.</param>
    /// <returns>The ragdoll entity, or null when it could not be created.</returns>
    public static Entity? CreateRagdollFor(Entity originalEntity, string? modelName, ReadOnlySpan<Matrix3x4> initialPose)
    {
        var originPhysics = originalEntity.Get<PhysicsController>();
        if (originPhysics is null || originPhysics.IsNullSkeleton || !originPhysics.IsEnabled)
            return null;

        var rootScene = GetRootScene(originalEntity);
        if (rootScene is null)
            return null;

        // 1. Clone the physics bones (deep clone: components, constraints and children included).
        var clonedBones = new List<Entity>(originPhysics.PhysicsBones.Count);
        foreach (var bone in originPhysics.PhysicsBones)
            clonedBones.Add(EntityCloner.Clone(bone));

        // 2. Create the temp entity.
        var entindex = originalEntity.Get<Components.ClEntityComponent>()?.Index ?? -1;
        var ragdollEntity = modelName is null
            ? new Entity($"Ragdoll@{entindex}")
            : new Entity($"Ragdoll@{entindex}(\"{modelName}\")");

        var ragdollPhysics = new PhysicsController { MotionType = PhysicsMotionType.Dynamic };
        ragdollEntity.Components.Add(ragdollPhysics);

        // 3. Bring it into the scene first so the bodies land in the same simulation.
        rootScene.Entities.Add(ragdollEntity);

        // 4. Load and attach the cloned skeleton, then initialize its pose.
        //    Enable() must come before SetPose so the bodies exist in the simulation and Teleport
        //    actually writes into them.
        ragdollPhysics.LoadSkeleton(clonedBones);
        ragdollPhysics.Enable();
        ragdollPhysics.SetPose(initialPose);

        // 5. Lightweight behavior that keeps the entity transform in sync with the pivot bone.
        ragdollEntity.Components.Add(new RagdollBehavior());

        return ragdollEntity;
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
