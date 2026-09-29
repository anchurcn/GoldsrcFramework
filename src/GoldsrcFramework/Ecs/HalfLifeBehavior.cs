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

    /// <summary>
    /// Studio bone matrices of the model, as written by <c>PreStudioModelRenderer</c>. Allocated on
    /// first use - a brush model never needs it - and reused afterwards, because it is a plain output
    /// buffer that carries no per-frame state of its own.
    /// </summary>
    private Matrix3x4[]? studioPose;

    public HalfLifeBehavior()
    {
        // Init on start.
        ContentManager = null!;
        clEntity = null!;
        PhysicsController = null!;
    }
    /// <summary>
    /// Binds the entity level dependencies and makes sure exactly one <see cref="PhysicsController"/>
    /// is attached to this entity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="Start"/> is not called once per instance.</b> The script system forgets a script
    /// whenever its entity leaves the scene - Stride reports that as a component removal, because
    /// removing an entity from the <c>EntityManager</c> notifies the processors with <c>forceRemove</c> -
    /// so an entity that leaves and later re-enters the PVS is registered again and
    /// <see cref="Start"/> runs a second time on the <b>same</b> instance. Upstream behaves the same
    /// way: <c>ScriptProcessor</c> maps those same notifications onto <c>ScriptSystem.Add</c> /
    /// <c>Remove</c>, and <c>Add</c> queues the script for <c>Start</c> again.
    /// </para>
    /// <para>
    /// Everything here is therefore written to be re-entrant, and the controller is taken from the
    /// entity instead of being created unconditionally.
    /// </para>
    /// </remarks>
    public override void Start()
    {
        ContentManager = this.Entity.EntityManager.Services.GetService<IContentManager>()
            ?? throw new NullReferenceException("IContentManager is not registered.");
        clEntity = Entity.Get<Components.ClEntityComponent>();

        // Reuse what a previous Start left on the entity: the controller outlives the entity leaving
        // the scene, and a second component of a type without [AllowMultipleComponents] cannot be added
        // to the same entity at all. Nothing else needs to be redone for the re-entering case either -
        // Disable() only detaches the bone entities and keeps them, so Enable() (which OnEnter leads to)
        // restores that very same skeleton. The skeleton and model state below is deliberately left
        // untouched on a re-entering entity, so it keeps its pose instead of reloading the prefab.
        PhysicsController = GetOrAddPhysicsController();
    }

    /// <summary>
    /// The controller attached to this entity, adding one when there is none yet.
    /// </summary>
    /// <remarks>
    /// The lookup goes through <see cref="Entity.Get{T}"/> - this entity's own components, children
    /// excluded - so an entity that re-enters the scene hands back the controller its first
    /// <see cref="Start"/> created instead of stacking a second one.
    /// </remarks>
    private PhysicsController GetOrAddPhysicsController()
    {
        if (Entity.Get<PhysicsController>() is { } existing)
            return existing;

        var controller = new PhysicsController();
        Entity.Components.Add(controller);
        return controller;
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

        if (IsPlayingDeathSequence && !physics.IsNullSkeleton)
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
    /// Change detection is <see cref="CheckModelChanged"/>, which reads the live model cookie and
    /// compares it with the one the current skeleton was built for.
    /// </para>
    /// </summary>
    private unsafe void EnsureSkeletonLoaded()
    {
        var modelChanged = CheckModelChanged(out var currentCookie);
        if (modelChanged)
        {
            modelCookie = currentCookie;
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
    /// <param name="currentCookie">
    /// The freshly read cookie, for the caller to record when this returns <c>true</c>.
    /// </param>
    /// <returns><c>true</c> when the entity is no longer on the model the skeleton was built for.</returns>
    private unsafe bool CheckModelChanged(out IntPtr currentCookie)
    {
        if (IsPlayer)
        {
            // A player model is identified by its name. Comparing names rather than the pointers is
            // deliberate: the engine hands a model slot back to the free list and reuses it for the
            // next model, so one address can describe two different models over time.
            currentCookie = clEntity.PlayerInfo->model.GetNCharPtr();
            return !ModelNameEquals(NCharPtr.From(currentCookie), NCharPtr.From(modelCookie));
        }

        // Every other model is identified by its index in the engine's model table.
        currentCookie = clEntity.NativeEntity->curstate.modelindex;
        return currentCookie != modelCookie;
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
                physics.LoadSkeleton(prefab.Instantiate(), GetStudioBoneParents(ModelKey!));
                physics.Enable();
                return;
            }
        }

        // No physics data for this model: NullPhysicsSkeleton, rendering still works.
        physics.LoadSkeleton([], []);
    }

    /// <summary>
    /// Studio bone hierarchy of the model behind <paramref name="key"/>: for every studio bone its
    /// parent bone index, -1 for a root bone.
    /// </summary>
    /// <remarks>
    /// Read here rather than requested from the content manager: the content manager only hands out
    /// the studio header as a <see cref="StudioModel"/>, and the hierarchy is a plain read of it.
    /// A brush model - or any model without a studio header - has a single implicit root bone, so
    /// <c>[-1]</c>.
    /// </remarks>
    private unsafe int[] GetStudioBoneParents(string key)
    {
        var studioModel = ContentManager.Load<StudioModel>(key);
        if (studioModel is null || studioModel.Header->numbones <= 0)
            return [-1];

        var bones = studioModel.Header->GetBones();
        var parents = new int[studioModel.Header->numbones];
        for (var i = 0; i < parents.Length; i++)
            parents[i] = bones[i].parent;

        return parents;
    }

    /// <summary>
    /// Returns the current animation pose used to drive the physics skeleton, or null when no pose
    /// source is available.
    /// </summary>
    /// <remarks>
    /// Brush models have a single implicit root bone that sits at the model origin, so a one element
    /// pose is the whole skeleton. A studio model instead needs the whole studio bone array of the
    /// animation the engine has applied to the entity this frame, which <c>PreStudioModelRenderer</c>
    /// produces - the same array the renderer draws from.
    /// </remarks>
    private unsafe Matrix3x4[]? GetCurrentPose()
    {
        if (PhysicsController!.PhysicsBones.Count == 1)
        {
            // World matrices are only refreshed during the Draw phase, so refresh explicitly before
            // reading one inside the Update phase.
            Entity.Transform.UpdateWorldMatrix();
            return [Entity.Transform.WorldMatrix.ToMatrix3x4()];
        }

        // Anything else is a studio model. Sampling its animation here, in the script stage, is what
        // keeps the skeleton in step with the model: a kinematic skeleton is driven from a pose, and
        // the render pass runs too late to hand one over for the step that follows.
        if (clEntity.NativeEntity == null)
            return null;

        studioPose ??= new Matrix3x4[StudioConstants.MAXSTUDIOBONES];
        return Rendering.PreStudioModelRenderer.SetupBones(clEntity.NativeEntity, studioPose) > 0
            ? studioPose
            : null;
    }



    /// <summary>
    /// Physics resource key of an entity, or null when the entity has no physics data.
    /// </summary>
    /// <remarks>
    /// A brush model is keyed by model index (<c>*&lt;index&gt;</c>); a studio model is keyed by the
    /// path of its physics data (<c>models/barney.gpd</c>). Both are what
    /// <see cref="IContentManager.Load{T}"/> expects.
    /// </remarks>
    private static unsafe string? GetPhysicsModelKey(cl_entity_t* nativeEntity)
    {
        if (nativeEntity == null || nativeEntity->model == null)
            return null;

        if (nativeEntity->model->type == modtype_t.mod_studio)
            return GetStudioPhysicsKey(nativeEntity->model);

        if (nativeEntity->model->type != modtype_t.mod_brush)
            return null;

        int modelIndex = nativeEntity->curstate.modelindex;
        return modelIndex >= 1 ? $"*{modelIndex}" : null;
    }

    /// <summary>
    /// Physics resource key of a studio model: its name with the extension replaced by <c>.gpd</c>,
    /// e.g. <c>models/barney.mdl</c> to <c>models/barney.gpd</c>.
    /// </summary>
    /// <remarks>
    /// The content manager accepts either spelling, so the key stays a plain model path and no
    /// separate key space is needed. Player models keep the legacy layout
    /// (<c>models/player.mdl</c>, <c>models/player/gordon/gordon.mdl</c>) because only the
    /// extension is swapped.
    /// </remarks>
    private static unsafe string? GetStudioPhysicsKey(model_t* model)
    {
        // The engine keeps a model name in a fixed size char buffer, so a longer name cannot name a
        // model and needs no handling.
        Span<char> name = stackalloc char[MaxModelNameLength];
        int length = 0;

        while (length < MaxModelNameLength && (byte)model->name[length] != 0)
        {
            name[length] = (char)(byte)model->name[length];
            length++;
        }

        if (length < 5 || !name[..length].EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
            return null;

        return new string(name[..(length - 4)]) + ".gpd";
    }
}
