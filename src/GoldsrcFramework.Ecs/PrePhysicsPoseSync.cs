using GoldsrcFramework.LinearMath;
using Stride.Core;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Syncs kinematic physics bone poses from GoldSrc transforms before the physics simulation step.
/// Runs at UpdateOrder = -200, after <see cref="GoldsrcTransformSyncSystem"/> (-500) and before
/// <see cref="Stride.BepuPhysics.Systems.PhysicsGameSystem"/> (-49).
/// </summary>
public sealed class PrePhysicsPoseSync : GameSystemBase
{
    private readonly GoldsrcSceneSystem sceneSystem;

    public PrePhysicsPoseSync(IServiceRegistry services, GoldsrcSceneSystem sceneSystem)
        : base(services)
    {
        this.sceneSystem = sceneSystem ?? throw new ArgumentNullException(nameof(sceneSystem));
        Enabled = true;
        UpdateOrder = -200;
    }

    public override void Update(GameTime gameTime)
    {
        var rootScene = sceneSystem.RootScene;
        if (rootScene is null)
            return;

        // Walk the scene hierarchy and sync all kinematic physics controllers.
        foreach (var entity in rootScene.Entities)
            ProcessEntity(entity);
    }

    private void ProcessEntity(Entity entity)
    {
        var phys = entity.Get<PhysicsController>();
        if (phys is not null &&
            phys.MotionType == PhysicsMotionType.Kinematic &&
            !phys.RagdollRigged &&
            !phys.IsNullSkeleton)
        {
            SyncKinematicPose(entity, phys);
        }

        // Recurse into children
        foreach (var child in entity.GetChildren())
            ProcessEntity(child);
    }

    private static void SyncKinematicPose(Entity rootEntity, PhysicsController phys)
    {
        // Brush model: single bone at bone index 0, pose is the root entity's world transform.
        // Studio model: pose should come from PreStudioModelRenderer.SetupBones (not implemented yet).
        // For now, handle the brush case (pose[0] = root world transform) and skip studio models
        // that have more than one bone.
        if (phys.PhysicsBones.Count == 1)
        {
            var pose = new Matrix3x4[1];
            pose[0] = rootEntity.Transform.WorldMatrix.ToMatrix3x4();
            phys.SetPose(pose);
        }
        // TODO: studio model pose from PreStudioModelRenderer.SetupBones
    }
}
