using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Implements Half-Life-specific physics behavior for an entity.
/// Manages the PhysicsController lifecycle: creation on enter, disposal on exit,
/// kinematic↔dynamic switching for death/ragdoll states, and model validation.
/// </summary>
public sealed class HalfLifeBehavior : ScriptComponentBase, IEnterExitCallable
{
    /// <summary>
    /// The physics controller for this entity, created during <see cref="OnEnter"/>.
    /// </summary>
    public PhysicsController? PhysicsController { get; private set; }

    /// <summary>
    /// The model path for this entity. Used to validate that the physics controller
    /// matches the current model.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Whether the entity is currently playing a death sequence.
    /// Set externally by the entity state management code.
    /// </summary>
    public bool IsPlayingDeathSequence { get; set; }

    /// <summary>
    /// Whether a ragdoll has been rigged for the current death sequence.
    /// </summary>
    public bool RagdollRigged { get; set; }

    public void OnEnter()
    {
        if (PhysicsController is null)
        {
            PhysicsController = PhysicsController.CreateDefault(ModelPath ?? "unknown", Entity);
            Entity.Components.Add(PhysicsController);
        }

        PhysicsController.Enable();
    }

    public void OnExit()
    {
        PhysicsController?.Disable();
    }

    public override void Update(GameTime gameTime)
    {
        if (PhysicsController is null)
            return;

        // Validate model hasn't changed
        if (ModelPath is not null && !PhysicsController.ValidateModel(ModelPath))
        {
            PhysicsController.Disable();
            Entity.Components.Remove(PhysicsController);
            PhysicsController = PhysicsController.CreateDefault(ModelPath, Entity);
            Entity.Components.Add(PhysicsController);
            PhysicsController.Enable();
        }

        // Handle death sequence → ragdoll transition
        if (IsPlayingDeathSequence && PhysicsController.MotionType != PhysicsMotionType.Dynamic)
        {
            if (!RagdollRigged)
            {
                RagdollHelper.CreateRagdollFor(Entity, PhysicsController);
                RagdollRigged = true;
            }
        }
        else if (!IsPlayingDeathSequence)
        {
            RagdollRigged = false;

            if (PhysicsController.MotionType != PhysicsMotionType.Kinematic)
            {
                PhysicsController.MotionType = PhysicsMotionType.Kinematic;
                PhysicsController.Enable();
            }
        }

        // if kinematic, need PhysicsController.SetPose(). 
        // For brush, then PhysicsController.SetPose([ToMatrix(cl_entity_t.origin and angles)]);
        // For studio then PhysicsController.SetPose(PreStudioModelRenderer.SetupBones(studio model));
        // 考虑增加 Entity 级的 SetPose，那样 brush 就可以直接 PivotPhysicsBone.SetPose(Entity.Transform)了。（Entity是 RootEntity）
    }


    // LateUpdate() -- copy physics controller's simulated pose back to root entity.
    // if dynamic, 
    // Entity.Transform (root entity) = PivotPhysicsBone.Transform * ModelRootOffset
    // For studio， 延迟到 StudioModelRenderer 绘制模型时 SetupBones，来 PhysicsController.GetPose() 获取骨骼变换。
}