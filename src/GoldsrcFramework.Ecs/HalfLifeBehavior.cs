using GoldsrcFramework.LinearMath;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Implements Half-Life-specific physics behavior for an entity.
/// Manages the <see cref="PhysicsController"/> lifecycle: creation on enter,
/// disable on exit, model validation, and death/ragdoll transitions.
/// </summary>
public sealed class HalfLifeBehavior : ScriptComponentBase, IEnterExitCallable
{
    /// <summary>The physics controller for this entity, created during <see cref="OnEnter"/>.</summary>
    public PhysicsController? PhysicsController { get; private set; }

    /// <summary>Whether this entity is a player (uses model name instead of model pointer for validation).</summary>
    public bool IsPlayer { get; set; }

    /// <summary>Whether the entity is currently playing a death sequence. Set externally.</summary>
    public bool IsPlayingDeathSequence { get; set; }

    /// <summary>Content manager used to load physics prefabs. Set by the entity creation system.</summary>
    public IContentManager? ContentManager { get; set; }

    /// <summary>The model key used for loading physics prefab. Set externally each frame.</summary>
    public string? ModelKey { get; set; }

    public void OnEnter()
    {
        if (PhysicsController is null)
        {
            PhysicsController = new PhysicsController();
            Entity.Components.Add(PhysicsController);
        }

        EnsureSkeletonLoaded();
        PhysicsController.Enable();
    }

    public void OnExit()
    {
        PhysicsController?.Disable();
    }

    public override void Start()
    {
        
    }

    public override void Update(GameTime gameTime)
    {
        if (PhysicsController is null)
            return;

        // RagdollRigged state: only check for respawn
        if (PhysicsController.RagdollRigged)
        {
            if (!IsPlayingDeathSequence)
                PhysicsController.ReattachSkeleton();
            return;
        }

        EnsureSkeletonLoaded();

        // Death → ragdoll
        if (IsPlayingDeathSequence && !PhysicsController.RagdollRigged)
        {
            // Get current animation pose to initialize the ragdoll.
            // For studio models this comes from PreStudioModelRenderer.SetupBones;
            // for brush models it's the root entity's world transform.
            // Pose source is provided externally; here we use a placeholder.
            var initialPose = GetCurrentPose();
            if (initialPose is not null)
            {
                RagdollHelper.CreateRagdollFor(Entity, initialPose);
            }
        }
    }

    /// <summary>
    /// Validates the model hasn't changed and reloads the physics skeleton if needed.
    /// </summary>
    private void EnsureSkeletonLoaded()
    {
        if (ContentManager is null || ModelKey is null)
            return;

        bool modelChanged;
        if (IsPlayer)
        {
            string currentName = Entity.Get<PlayerInfoComponent>()?.ModelName ?? string.Empty;
            modelChanged = !PhysicsController!.ValidateModel(currentName);
        }
        else
        {
            IntPtr currentPointer = GetCurrentModelPointer();
            modelChanged = !PhysicsController!.ValidateModel(currentPointer);
        }

        if (!modelChanged && !PhysicsController.IsNullSkeleton)
            return;

        ReloadSkeleton();
    }

    private void ReloadSkeleton()
    {
        // Remove old physics bones
        foreach (var bone in PhysicsController!.PhysicsBones)
            bone.Transform.Parent = null;

        if (ContentManager!.IsExist(ModelKey!))
        {
            var prefab = ContentManager.Load<Prefab>(ModelKey!);
            if (prefab is not null)
            {
                var bones = Stride.Engine.Design.EntityCloner.Instantiate(prefab);
                foreach (var bone in bones)
                    bone.Transform.Parent = Entity.Transform;

                if (IsPlayer)
                {
                    string modelName = Entity.Get<PlayerInfoComponent>()?.ModelName ?? string.Empty;
                    PhysicsController.LoadSkeleton(bones, modelName, true);
                }
                else
                {
                    PhysicsController.LoadSkeleton(bones, GetCurrentModelPointer(), false);
                }
            }
            else
            {
                PhysicsController.LoadSkeleton([], GetCurrentModelPointer(), false);
            }
        }
        else
        {
            // NullPhysicsSkeleton
            if (IsPlayer)
                PhysicsController.LoadSkeleton([], Entity.Get<PlayerInfoComponent>()?.ModelName ?? string.Empty, true);
            else
                PhysicsController.LoadSkeleton([], GetCurrentModelPointer(), false);
        }
    }

    private IntPtr GetCurrentModelPointer()
    {
        // Native model pointer from cl_entity_t.model
        var clEntity = Entity.Get<Components.ClEntityComponent>();
        if (clEntity is not null && clEntity.HasNativeEntity)
        {
            unsafe
            {
                return (IntPtr)clEntity.NativeEntity->model;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Returns the current animation pose for initializing a ragdoll.
    /// For studio models, this should be provided by PreStudioModelRenderer.SetupBones.
    /// For brush models, it's the root entity's world transform.
    /// Returns null if pose is not available (ragdoll creation is skipped).
    /// </summary>
    private Matrix3x4[]? GetCurrentPose()
    {
        // TODO: integrate with PreStudioModelRenderer for studio models.
        // For now, return the root entity's world transform as pose[0] (brush model case).
        var pose = new Matrix3x4[1];
        pose[0] = Entity.Transform.WorldMatrix.ToMatrix3x4();
        return pose;
    }
}
