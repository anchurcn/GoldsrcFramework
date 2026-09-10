using GoldsrcFramework.LinearMath;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Implements Half-Life-specific physics behavior for an entity.
/// <para>
/// Owns the entity level state (<see cref="PhysicsController"/> lifecycle, model identity tracking,
/// death/ragdoll state) and drives the physics skeleton: it reloads the skeleton when the model
/// changes and writes the current animation pose into it every frame.
/// </para>
/// <para>
/// Runs inside <see cref="GoldsrcScriptSystem"/> (UpdateOrder -90), which is scheduled after
/// <see cref="GoldsrcSceneSystem"/> and before <c>PhysicsGameSystem</c>, so anything written here
/// takes effect in the same physics step.
/// </para>
/// </summary>
public sealed class HalfLifeBehavior : ScriptComponentBase, IEnterExitCallable
{
    /// <summary>The physics controller for this entity, created during <see cref="OnEnter"/>.</summary>
    public PhysicsController? PhysicsController { get; private set; }

    /// <summary>Whether this entity is a player (model validated by name instead of by pointer).</summary>
    public bool IsPlayer { get; set; }

    /// <summary>Whether the entity is currently playing a death sequence. Set externally.</summary>
    public bool IsPlayingDeathSequence { get; set; }

    /// <summary>
    /// True while this entity's physics skeleton has been detached into a ragdoll temp entity.
    /// </summary>
    public bool RagdollRigged { get; private set; }

    /// <summary>Content manager used to load physics prefabs. Set by the entity creation system.</summary>
    public IContentManager? ContentManager { get; set; }

    /// <summary>The model key used for loading the physics prefab. Set externally each frame.</summary>
    public string? ModelKey { get; set; }

    private IntPtr modelPointer;
    private string modelName = string.Empty;
    private bool skeletonLoaded;

    public void OnEnter()
    {
        if (PhysicsController is null)
        {
            PhysicsController = new PhysicsController();
            Entity.Components.Add(PhysicsController);

            // First time we see this entity: load (and enable) the skeleton.
            EnsureSkeletonLoaded();
        }

        // Coming back into the PVS: the skeleton is still loaded, just re-attach it.
        PhysicsController.Enable();
    }

    public void OnExit()
    {
        PhysicsController?.Disable();
    }

    public override void Update(GameTime gameTime)
    {
        var physics = PhysicsController;
        if (physics is null)
            return;

        // The skeleton is currently living in a ragdoll temp entity: only watch for respawn.
        if (RagdollRigged)
        {
            if (!IsPlayingDeathSequence)
            {
                RagdollRigged = false;
                physics.Enable();
            }

            return;
        }

        EnsureSkeletonLoaded();

        // Only a kinematic, enabled skeleton is driven by the animation each frame.
        if (!physics.IsEnabled || physics.MotionType != PhysicsMotionType.Kinematic)
            return;

        if (IsPlayingDeathSequence)
        {
            // Hand the current pose over to the ragdoll. The snapshot keeps the pose of bones that
            // have no rigid body - a fist stays clenched instead of relaxing into the bind pose.
            var deathPose = GetCurrentPose();
            if (deathPose is not null)
            {
                var snapshot = physics.CapturePoseSnapshot(deathPose);
                if (RagdollHelper.CreateRagdollFor(Entity, ModelKey, deathPose, snapshot) is not null)
                {
                    physics.Disable();
                    RagdollRigged = true;
                }
            }

            return;
        }

        var pose = GetCurrentPose();
        if (pose is not null)
            physics.SetPose(pose);
    }

    /// <summary>
    /// Compares the current model identity against the last loaded one and reloads the skeleton when
    /// it changed. The first call always loads.
    /// </summary>
    private void EnsureSkeletonLoaded()
    {
        if (ContentManager is null || ModelKey is null)
            return;

        bool modelChanged;
        if (IsPlayer)
        {
            var currentName = Entity.Get<PlayerInfoComponent>()?.ModelName ?? string.Empty;
            modelChanged = !string.Equals(modelName, currentName, StringComparison.OrdinalIgnoreCase);
            modelName = currentName;
        }
        else
        {
            var currentPointer = GetCurrentModelPointer();
            modelChanged = modelPointer != currentPointer;
            modelPointer = currentPointer;
        }

        if (skeletonLoaded && !modelChanged)
            return;

        skeletonLoaded = true;
        ReloadSkeleton();
    }

    private void ReloadSkeleton()
    {
        var physics = PhysicsController!;

        if (ContentManager!.IsExist(ModelKey!))
        {
            var prefab = ContentManager.Load<Prefab>(ModelKey!);
            if (prefab is not null)
            {
                // LoadSkeleton detaches and releases the previous skeleton. The bone hierarchy is
                // needed so that bones without a rigid body can be anchored to a simulated ancestor.
                var boneParents = ContentManager.GetStudioBoneParents(ModelKey!);
                physics.LoadSkeleton(prefab.Instantiate(), boneParents);
                physics.Enable();
                return;
            }
        }

        // No physics data for this model: NullPhysicsSkeleton, rendering still works.
        physics.LoadSkeleton([], []);
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
    /// Returns the current animation pose used to drive the physics skeleton, or null when no pose
    /// source is available.
    /// </summary>
    /// <remarks>
    /// Brush models have a single implicit root bone that sits at the model origin, so a one element
    /// pose is the whole skeleton. Studio models need the full studio bone array, which comes from
    /// <c>PreStudioModelRenderer.SetupBones</c> - not implemented in this part.
    /// </remarks>
    private Matrix3x4[]? GetCurrentPose()
    {
        if (PhysicsController!.PhysicsBones.Count != 1)
            return null;

        // World matrices are only refreshed during the Draw phase, so refresh explicitly before
        // reading one inside the Update phase.
        Entity.Transform.UpdateWorldMatrix();
        return [Entity.Transform.WorldMatrix.ToMatrix3x4()];
    }
}
