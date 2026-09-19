using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;
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
    public PhysicsController PhysicsController { get; private set; }

    /// <summary>
    /// Whether this entity is a player. Selects how the model cookie is read: a player model is
    /// identified by its name, every other model by its index.
    /// </summary>
    public bool IsPlayer => clEntity.IsPlayer;

    /// <summary>Whether the entity is currently playing a death sequence. Set externally.</summary>
    public bool IsPlayingDeathSequence { get; set; }

    /// <summary>
    /// True while this entity's physics skeleton has been detached into a ragdoll temp entity.
    /// </summary>
    public bool RagdollRigged { get; private set; }

    /// <summary>Content manager used to load physics prefabs. Set by the entity creation system.</summary>
    public IContentManager ContentManager { get; set; }

    /// <summary>The model key used for loading the physics prefab. Set externally each frame.</summary>
    public string? ModelKey { get; set; }

    private Components.ClEntityComponent clEntity;

    /// <summary>
    /// Identity of the model the currently loaded physics skeleton was built for - the record that
    /// <see cref="ValidateModelCookie"/> compares the live engine state against. Only written when
    /// the model actually changed, so it never drifts away from the skeleton it describes.
    /// <para>
    /// The value is a cookie: it is only ever compared, never interpreted as an address or an index -
    /// which of the two it is follows from <see cref="IsPlayer"/>. For players it is the native
    /// pointer to the model name (<c>model_t.name</c>); for every other entity the native
    /// <c>curstate.modelindex</c>, or <c>-1</c> when there is no native entity.
    /// </para>
    /// </summary>
    private IntPtr modelCookie;

    private bool skeletonLoaded;
    private bool onEnter;
    private const int MaxModelNameLength = 64; // MAX_MODEL_NAME

    public HalfLifeBehavior()
    {
        // Init on start.
        ContentManager = null!;
        clEntity = null!;
        PhysicsController = null!;
    }
    public override void Start()
    {
        ContentManager = this.Entity.EntityManager.Services.GetService<IContentManager>()
            ?? throw new NullReferenceException("IContentManager is not registered.");
        clEntity = Entity.Get<Components.ClEntityComponent>();
        PhysicsController = new PhysicsController();
        Entity.Components.Add(PhysicsController);
    }

    public void OnEnter()
    {
        onEnter = true;
    }

    public void OnExit()
    {
        PhysicsController?.Disable();
    }

    public override void Update(GameTime gameTime)
    {
        EnsureSkeletonLoaded();
        var physics = PhysicsController;

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

        if (onEnter)
        {
            physics.Enable();
            onEnter = false;
        }

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
    /// Reloads the physics skeleton when the entity switched to a different model. The first call
    /// always loads.
    /// <para>
    /// Change detection is <see cref="ValidateModelCookie"/>, which reads the live model cookie and
    /// compares it with the one the current skeleton was built for.
    /// </para>
    /// </summary>
    private unsafe void EnsureSkeletonLoaded()
    {
        var modelChanged = ValidateModelCookie(out var newModelCookie);
        if (modelChanged)
        {
            modelCookie = newModelCookie;
        }

        if (skeletonLoaded && !modelChanged)
            return;

        skeletonLoaded = true;
        ModelKey = GetPhysicsModelKey(clEntity.NativeEntity);
        ReloadSkeleton();
    }

    /// <summary>
    /// Reads the cookie of the model the engine has bound to this entity right now and reports
    /// whether it differs from <see cref="modelCookie"/> - i.e. whether the entity switched to a
    /// different model and the skeleton has to be rebuilt.
    /// </summary>
    /// <param name="newModelCookie">
    /// The freshly read cookie, for the caller to record when this returns <c>true</c>.
    /// </param>
    /// <returns><c>true</c> when the entity is no longer on the model the skeleton was built for.</returns>
    private unsafe bool ValidateModelCookie(out IntPtr newModelCookie)
    {
        if (IsPlayer)
        {
            newModelCookie = clEntity.PlayerInfo->model.GetNCharPtr();
            return ModelNameEquals(NCharPtr.From(newModelCookie), NCharPtr.From(modelCookie));
        }
        else
        {
            newModelCookie = clEntity.NativeEntity->curstate.modelindex;
            return newModelCookie == modelCookie;
        }
    }


    /// <summary>
    /// Compares two native model name strings byte for byte, without allocating a managed string.
    /// Reads at most <see cref="MaxModelNameLength"/> bytes of each, stopping at the terminating NUL.
    /// </summary>
    private static bool ModelNameEquals(NCharPtr current, NCharPtr last)
    {
        if (current.IsNull || last.IsNull)
            return current.IsNull && last.IsNull;

        return 
            current.AsByteSpan(MaxModelNameLength).SequenceEqual(last.AsByteSpan(MaxModelNameLength));
    }
    private void ReloadSkeleton()
    {
        var physics = PhysicsController!;

        if (ContentManager.IsExist(ModelKey!))
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



    /// <summary>
    /// Physics resource key of an entity, or null when this part of the framework cannot provide
    /// one. Brush models are keyed by model index; studio models are resolved by name elsewhere.
    /// </summary>
    private static unsafe string? GetPhysicsModelKey(cl_entity_t* nativeEntity)
    {
        if (nativeEntity == null || nativeEntity->model == null)
            return null;

        if (nativeEntity->model->type != modtype_t.mod_brush)
            return null;

        int modelIndex = nativeEntity->curstate.modelindex;
        return modelIndex >= 1 ? $"*{modelIndex}" : null;
    }
}
