using GoldsrcFramework.Content.Gpd;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;
using System.Runtime.InteropServices;
using System.Text;
using GoldsrcVector3 = GoldsrcFramework.LinearMath.Vector3;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Creates, links and destroys ragdolls, and owns their lifetime.
/// </summary>
/// <remarks>
/// <para>
/// A ragdoll is two things that have to be kept together: a Stride scene entity that simulates the
/// skeleton, and a client entity (<c>cl_entity_t</c>) that the engine draws. The engine knows nothing
/// about the scene, so a ragdoll without a client entity of its own simulates perfectly and is never
/// seen.
/// </para>
/// <para>
/// <b>How the two are linked.</b> The client entity is a temp entity the client owns - the engine adds
/// it to the render list every frame. It carries the ragdoll in two ways, each serving one direction:
/// </para>
/// <list type="bullet">
/// <item>
/// Its <c>curstate.iuser4</c> holds a <see cref="GCHandle"/> to the Stride entity, which is how the
/// renderer gets from an entity it is asked to draw back to the skeleton supplying the pose. The
/// field is one of the <c>iuser</c> slots Half-Life leaves to the mod.
/// </item>
/// <item>
/// The Stride entity holds a <see cref="Components.ClEntityComponent"/> pointing back at the client
/// entity, which is how the ragdoll keeps the one the engine draws where the simulation put it.
/// </item>
/// </list>
/// <para>
/// A corpse is also marked with <see cref="kRenderFxRagdoll"/> in <c>curstate.renderfx</c>: it is what
/// tells the renderer this client entity is a ragdoll, and it is the guard that makes reading
/// <c>iuser4</c> safe - server entities have their own <c>iuser</c> values, set by the game.
/// </para>
/// </remarks>
public static class RagdollHelper
{
    /// <summary>FTENT_PERSIST - the entity is meant to stay, so the engine must not age it out.</summary>
    private const int TempEntityPersist = 0x0000_2000;

    /// <summary>FTENT_NOMODEL - nothing left to draw, the entity can be recycled.</summary>
    private const int TempEntityNoModel = 0x0004_0000;

    /// <summary>
    /// Render effect marking a client entity as a ragdoll. Well outside the effects GoldSrc defines,
    /// so the renderer's switch treats it as no effect, and unique enough to be used as a marker.
    /// </summary>
    private const int kRenderFxRagdoll = 3601;

    /// <summary>Console variable holding how long a corpse stays, in seconds.</summary>
    private const string CorpseStayCvar = "phys_corpsestay";

    /// <summary>How long a corpse stays when the cvar is missing or not positive.</summary>
    private const float DefaultCorpseStaySeconds = 30.0f;

    private const int MaxModelNameLength = 64; // MAX_MODEL_NAME

    /// <summary>
    /// The game whose scene holds the entities a corpse can replace. Set once while the client
    /// initialises - see <see cref="AttachGame"/>.
    /// </summary>
    private static GoldsrcClientGame? game;

    /// <summary>Whether the one-off "a corpse reached the renderer" line has been printed.</summary>
    private static bool reportedCorpseDraw;

    // ── Creation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a ragdoll for the given entity and adds it to the same root scene: the general form,
    /// cloning the skeleton the entity is already wearing.
    /// </summary>
    /// <param name="originalEntity">The entity whose physics skeleton is cloned.</param>
    /// <param name="modelName">Optional model name, only used to name the entity.</param>
    /// <param name="initialPose">
    /// Studio bone world-space matrices used to place the ragdoll's simulated bones.
    /// </param>
    /// <param name="poseSnapshot">
    /// Pose captured from <paramref name="initialPose"/>, applied to the ragdoll so that bones without
    /// a rigid body keep the pose they had at the moment of death.
    /// </param>
    /// <returns>The ragdoll entity, or null when it could not be created.</returns>
    public static unsafe Entity? CreateRagdollFor(
        Entity originalEntity,
        string? modelName,
        ReadOnlySpan<Matrix3x4> initialPose,
        PoseSnapshot poseSnapshot)
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

        // A cloned joint still refers to the bodies it was cloned from: the body on the other end of a
        // joint lives on a different entity, which cloning does not copy. Left as they are, the
        // ragdoll is a pile of bodies whose joints hold on to a skeleton that has stopped simulating.
        RemapConstraints(originPhysics.PhysicsBones, clonedBones);

        // 2. Create the entity.
        var entindex = originalEntity.Get<Components.ClEntityComponent>()?.Index ?? -1;
        var ragdollEntity = modelName is null
            ? new Entity($"Ragdoll@{entindex}")
            : new Entity($"Ragdoll@{entindex}(\"{modelName}\")");

        var ragdollPhysics = new PhysicsController { MotionType = PhysicsMotionType.Dynamic };
        ragdollEntity.Components.Add(ragdollPhysics);

        //    Attach the transform link while the entity is still out of the scene: the sync system
        //    only looks an entity's link component up when the entity is added, so one attached
        //    afterwards would never be registered. Its authority is Stride - the simulation owns the
        //    transform and the Draw pass writes it back to the client entity the corpse is drawn
        //    through. The binding starts empty because that client entity does not exist yet;
        //    Associate points it at one once the corpse has it.
        var transformLink = new ClEntityTransformBinding(null);
        ragdollEntity.Components.Add(
            new GoldsrcTransformLinkComponent(transformLink, TransformAuthority.Stride));

        // 3. Bring it into the scene first so the bodies land in the same simulation.
        rootScene.Entities.Add(ragdollEntity);

        // 4. Load and attach the cloned skeleton, reusing the original's bone hierarchy, then
        //    initialize its pose. Enable() must come before SetPose so the bodies exist in the
        //    simulation and Teleport actually writes into them.
        ragdollPhysics.LoadSkeleton(clonedBones, originPhysics.StudioBoneParents.Span);
        ragdollPhysics.Enable();
        ragdollPhysics.SetPose(initialPose);
        ragdollPhysics.ApplyPoseSnapshot(poseSnapshot);

        // 5. The behavior that keeps the entity transform in sync and counts the lifetime down.
        ragdollEntity.Components.Add(new RagdollBehavior());

        // 6. Give it a client entity to be drawn through, and link the two together.
        var source = originalEntity.Get<Components.ClEntityComponent>()?.NativeEntity;
        if (source != null && source->model != null)
        {
            // Loading the model by name pins it for the corpse's lifetime.
            LoadStudioModel(EngineApi.PClient, source->model, out _);

            var clEntity = CreateRagdollTempEntFromTemplate(source, &source->curstate);
            if (clEntity != null)
                Associate(ragdollEntity, clEntity);
        }

        return ragdollEntity;
    }

    /// <summary>
    /// Creates a standalone ragdoll of <paramref name="modelName"/> at <paramref name="origin"/>: the
    /// simple form, with no entity to clone - the skeleton starts in the model's bind pose.
    /// </summary>
    /// <remarks>
    /// This is what the console command uses. The services are where the content manager and the root
    /// scene come from, so no game instance has to be threaded through.
    /// </remarks>
    /// <param name="services">Service registry to resolve the content manager and the root scene from.</param>
    /// <param name="modelName">Engine path of the model, e.g. <c>models/scientist.mdl</c>.</param>
    /// <param name="origin">Where to put the body, in world coordinates.</param>
    /// <returns>The ragdoll entity, or null when the model has no usable physics data.</returns>
    public static unsafe Entity? CreateRagdoll(IServiceRegistry services, string modelName, GoldsrcVector3 origin)
    {
        if (services is null || string.IsNullOrEmpty(modelName))
            return null;

        var content = services.GetService<IContentManager>();
        var scene = services.GetService<GoldsrcSceneSystem>()?.RootScene;
        var engine = EngineApi.PClient;
        if (content is null || scene is null || engine is null)
            return null;

        var key = StudioPhysicsKey(modelName);
        if (key is null || !content.IsExist(key))
            return null;

        var model = LoadModelByName(engine, modelName, out _);
        if (model == null || model->type != modtype_t.mod_studio)
            return null;

        var prefab = content.Load<Prefab>(key);
        if (prefab is null)
            return null;

        var entity = new Entity($"Ragdoll(\"{modelName}\")");
        var physics = new PhysicsController { MotionType = PhysicsMotionType.Dynamic };
        entity.Components.Add(physics);

        // Attach the transform link while the entity is still out of the scene: the sync system only
        // looks an entity's link component up when the entity is added, so one attached afterwards
        // would never be registered. Its authority is Stride - the simulation owns the transform and
        // the Draw pass writes it back to the client entity the corpse is drawn through. The binding
        // starts empty because that client entity does not exist yet; Associate points it at one.
        var transformLink = new ClEntityTransformBinding(null);
        entity.Components.Add(new GoldsrcTransformLinkComponent(transformLink, TransformAuthority.Stride));

        // The bones carry the bind pose relative to the model root, so placing the root places the
        // whole skeleton - no explicit pose is needed.
        entity.Transform.Position = new StrideVector3(origin.X, origin.Y, origin.Z);

        scene.Entities.Add(entity);

        physics.LoadSkeleton(prefab.Instantiate(), BoneParents(content, key));
        physics.Enable();

        // Bones without a rigid body (fingers, face) have no entity of their own: SetupBones rebuilds
        // them from a snapshot taken relative to their nearest simulated ancestor, and without one
        // they would be left at identity - pinned to the world origin instead of riding along with
        // the skeleton. A standalone ragdoll has no animation pose to hand over, so the pose to keep
        // is the model's bind pose.
        ApplyBindPoseSnapshot(content, key, physics);

        entity.Components.Add(new RagdollBehavior());

        var spawn = origin;
        var clEntity = CreateRagdollTempEnt(&spawn, model);
        if (clEntity != null)
            Associate(entity, clEntity);

        return entity;
    }

    /// <summary>
    /// Destroys a ragdoll: the client entity it is drawn through is handed back to the engine, the
    /// link is torn down, and the entity leaves the scene.
    /// </summary>
    /// <remarks>
    /// The GCHandle is freed by <see cref="RagdollBehavior.OnExit"/>, which the scene removal runs.
    /// </remarks>
    public static unsafe void Release(Entity? ragdoll)
    {
        if (ragdoll is null)
            return;

        // TODO*:ensure ragdoll behavior exist.

        var clEntity = ragdoll.Get<Components.ClEntityComponent>()?.NativeEntity;
        if (clEntity != null)
        {
            GCHandle.FromIntPtr(clEntity->curstate.iuser4).Free();
            clEntity->curstate.iuser4 = 0;
            ReleaseClientEntity(clEntity);
        }

        ragdoll.Scene?.Entities.Remove(ragdoll);
    }

    // ── Linking ───────────────────────────────────────────────────────────────

    /// <summary>Links a ragdoll to the client entity it is drawn through.</summary>
    private static unsafe void Associate(Entity ragdoll, cl_entity_t* clEntity)
    {
        var handle = GCHandle.Alloc(ragdoll, GCHandleType.Normal);
        var raw = unchecked((int)GCHandle.ToIntPtr(handle));

        clEntity->curstate.iuser4 = raw;
        ragdoll.Components.Add(new Components.ClEntityComponent(clEntity));

        // The transform link went on before the entity joined the scene, while the client entity it
        // writes back to did not exist yet. Point it at the one that does now.
        if (ragdoll.Get<GoldsrcTransformLinkComponent>()?.Binding is ClEntityTransformBinding binding)
            binding.SetEntity(clEntity);
    }

    /// <summary>
    /// The behavior that can produce a pose for <paramref name="entity"/>, if it is a ragdoll.
    /// </summary>
    /// <remarks>
    /// The <c>renderfx</c> marker is checked first and is what makes reading <c>iuser4</c> safe: an
    /// entity that is not a ragdoll may well have an <c>iuser</c> value of its own.
    /// </remarks>
    internal static unsafe bool TryGetBehavior(cl_entity_t* entity, out RagdollBehavior? behavior)
    {
        behavior = null;

        if (entity == null || entity->curstate.renderfx != kRenderFxRagdoll)
            return false;

        var raw = entity->curstate.iuser4;
        if (raw == 0)
            return false;

        try
        {
            if (GCHandle.FromIntPtr((nint)raw).Target is Entity ragdoll)
                behavior = ragdoll.Get<RagdollBehavior>();
        }
        catch
        {
            return false;
        }

        return behavior is not null;
    }

    /// <summary>
    /// Hands this helper the game it looks entity state up in.
    /// </summary>
    /// <remarks>
    /// Called once, at the composition root. Everything else here works from the entity it is given;
    /// only <see cref="IsReplacedByCorpse"/> has to reach into the scene, and only because the caller
    /// that needs it - the renderer, from the engine's draw loop - has no registry to ask.
    /// </remarks>
    internal static void AttachGame(GoldsrcClientGame clientGame) => game = clientGame;

    /// <summary>
    /// Fills <paramref name="modelRoot"/> and <paramref name="bones"/> with the pose of the physics
    /// skeleton a corpse is drawn from, when <paramref name="entity"/> is one of ours.
    /// </summary>
    /// <param name="entity">The client entity the engine is asking to draw.</param>
    /// <param name="modelRoot">The renderer's rotation matrix, to receive the model root pose.</param>
    /// <param name="bones">The renderer's bone buffer, to receive the pose, in studio bone order.</param>
    /// <param name="boneCapacity">How many matrices <paramref name="bones"/> can hold.</param>
    /// <returns>
    /// True when <paramref name="entity"/> is a ragdoll corpse and it wrote the pose. False for every
    /// other entity, and for a corpse whose skeleton is gone or does not match the model, which is
    /// then left to be drawn the ordinary way rather than blanked out.
    /// </returns>
    /// <remarks>
    /// Writing straight into the renderer's own buffers is the point: returning a pose instead would
    /// be a per-entity, per-frame allocation of several kilobytes. The ragdoll's <c>renderfx</c>
    /// marker is what tells the caller which entity to ask about, and <see cref="TryGetBehavior"/>
    /// re-checks it before reading <c>iuser4</c>.
    /// </remarks>
    internal static unsafe bool TryFillPhysicsPose(
        cl_entity_t* entity, Matrix3x4* modelRoot, Matrix3x4* bones, int boneCapacity)
    {
        if (entity == null || modelRoot == null || bones == null || boneCapacity <= 0)
            return false;

        if (!TryGetBehavior(entity, out var behavior))
            return false;

        var posed = behavior!.TryFillPhysicsPose(modelRoot, bones, boneCapacity);

        // Printed once: whether a corpse reaches the renderer at all is the question this whole
        // chain turns on, and there is no other way to see it from outside the engine's draw loop.
        // The root position comes with it because "reached the renderer" and "visible" are not the
        // same thing - a body flung away from where it was spawned is drawn exactly where physics
        // put it, which is nowhere near the camera.
        if (!reportedCorpseDraw)
        {
            reportedCorpseDraw = true;

            if (posed)
            {
                var root = modelRoot->Origin;
                Log($"ragdoll: corpse drawn from physics, model root at {root.X:F0} {root.Y:F0} {root.Z:F0}");
            }
            else
            {
                Log("ragdoll: the corpse reached the renderer but had no pose to draw");
            }
        }

        return posed;
    }

    /// <summary>
    /// Whether <paramref name="entity"/> is a client entity that a corpse has replaced, and so must
    /// not be drawn any more.
    /// </summary>
    /// <remarks>
    /// While an entity is <see cref="HalfLifeBehavior.RagdollRigged"/> its skeleton belongs to the
    /// ragdoll, and the engine would otherwise draw its death animation standing next to the body.
    /// The state is read live from the scene rather than remembered here, so nothing has to be
    /// cleared when an entity respawns or a map goes away.
    /// </remarks>
    internal static unsafe bool IsReplacedByCorpse(cl_entity_t* entity)
    {
        // Only a server entity has an ECS counterpart to ask. A corpse - or anything else the engine
        // keeps outside its entity list - has no index to look up.
        if (entity == null || entity->index <= 0)
            return false;

        return game?.SceneManagement.GetEntity(entity->index)?.Get<HalfLifeBehavior>() is { RagdollRigged: true };
    }

    /// <summary>How long a corpse stays, read live from <c>phys_corpsestay</c> so it can be tuned.</summary>
    internal static unsafe float CorpseStaySeconds()
    {
        var engine = EngineApi.PClient;
        if (engine == null || engine->GetCvarPointer == null)
            return DefaultCorpseStaySeconds;

        Span<byte> name = stackalloc byte[32];
        WriteAscii(CorpseStayCvar, name);

        fixed (byte* namePointer = name)
        {
            var cvar = engine->GetCvarPointer(NCharPtr.From(namePointer));
            return cvar != null && cvar->value > 0f ? cvar->value : DefaultCorpseStaySeconds;
        }
    }

    /// <summary>Seconds the client has been running, or 0 when the engine will not say.</summary>
    internal static unsafe float ClientTime()
    {
        var engine = EngineApi.PClient;
        return engine != null && engine->GetClientTime != null ? engine->GetClientTime() : 0.0f;
    }

    /// <summary>
    /// Writes one diagnostic line to the engine console.
    /// </summary>
    /// <remarks>
    /// Drawing a corpse spans the engine's temp entity list, the client's studio interface and the
    /// ECS: when a body does not appear there is nothing to look at, so the chain says what it did
    /// here. One line per corpse, never per frame.
    /// </remarks>
    internal static unsafe void Log(string message)
    {
        var engine = EngineApi.PClient;
        if (engine == null || engine->Con_Printf == null)
            return;

        Span<byte> buffer = stackalloc byte[192];
        buffer.Clear();

        var written = Encoding.ASCII.GetBytes(message, buffer);
        if (written <= 0 || written >= buffer.Length)
            return;

        buffer[written] = 0;

        fixed (byte* line = buffer)
            engine->Con_Printf((NChar*)line);
    }

    // ── Temp entity ───────────────────────────────────────────────────────────

    private static unsafe cl_entity_t* CreateRagdollTempEnt(Vector3* origin, model_t* model)
    {
        if (origin == null || model == null)
            return null;

        var engine = EngineApi.PClient;
        if (engine == null || engine->pEfxAPI == null || engine->pEfxAPI->CL_TentEntAllocCustom == null)
            return null;

        var tempEntity = engine->pEfxAPI->CL_TentEntAllocCustom(origin, model, 0, null);
        if (tempEntity == null)
            return null;

        tempEntity->flags |= TempEntityPersist;
        tempEntity->die = float.MaxValue;

        var ragdollEnt = &tempEntity->entity;
        ragdollEnt->curstate.renderfx = kRenderFxRagdoll;
        return ragdollEnt;
    }

    private static unsafe cl_entity_t* CreateRagdollTempEntFromTemplate(
        cl_entity_t* tmplEnt, entity_state_t* tmplState)
    {
        if (tmplEnt == null)
            return null;

        var drawModel = tmplEnt->model;
        if (drawModel == null || drawModel->type != modtype_t.mod_studio)
            return null;

        // The death position: prefer the state that triggered the ragdoll, fall back to the entity.
        var origin = tmplState != null ? tmplState->origin : tmplEnt->origin;

        var ragdollEnt = CreateRagdollTempEnt(&origin, drawModel);
        if (ragdollEnt == null)
            return null;

        // Copy the death render state so the corpse shows the death pose until physics takes over.
        CopyDeathState(tmplEnt, tmplState, ragdollEnt);

        return ragdollEnt;
    }

    private static unsafe void CopyDeathState(
        cl_entity_t* tmplEnt, entity_state_t* tmplState, cl_entity_t* ragdollEnt)
    {
        var srcState = tmplState != null ? tmplState : &tmplEnt->curstate;
        var ragdollState = &ragdollEnt->curstate;

        ragdollEnt->origin = tmplEnt->origin;
        ragdollEnt->angles = tmplEnt->angles;

        ragdollState->modelindex = srcState->modelindex;
        ragdollState->sequence = srcState->sequence;
        ragdollState->frame = srcState->frame;
        ragdollState->skin = srcState->skin;
        ragdollState->body = srcState->body;
        ragdollState->colormap = srcState->colormap;
        ragdollState->angles = srcState->angles;
        ragdollState->effects = srcState->effects;
        ragdollState->scale = srcState->scale;
        ragdollState->rendermode = srcState->rendermode;
        ragdollState->rendercolor = srcState->rendercolor;
        ragdollState->renderamt = srcState->renderamt;
        ragdollState->gaitsequence = srcState->gaitsequence;
        ragdollState->controller = srcState->controller;
        ragdollState->blending = srcState->blending;
    }

    /// <summary>Hands a client entity back to the engine, which recycles it once the time is up.</summary>
    private static unsafe void ReleaseClientEntity(cl_entity_t* clEntity)
    {
        var tempEntity = ContainerOf(clEntity);
        tempEntity->die = ClientTime();
        tempEntity->flags = TempEntityNoModel;
    }

    /// <summary>
    /// The temp entity a client entity belongs to.
    /// </summary>
    /// <remarks>
    /// <c>entity</c> is the last field of <c>TEMPENTITY</c>, and neither it nor <c>cl_entity_t</c> has
    /// trailing padding, so the offset is simply the difference of the two sizes.
    /// </remarks>
    private static unsafe TEMPENTITY* ContainerOf(cl_entity_t* entity)
        => (TEMPENTITY*)((byte*)entity - (sizeof(TEMPENTITY) - sizeof(cl_entity_t)));

    // ── Skeleton ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Studio bone hierarchy of the model behind <paramref name="key"/>: for every studio bone its
    /// parent bone index, -1 for a root bone.
    /// </summary>
    /// <remarks>
    /// A brush model - or any model without a studio header - has a single implicit root bone, so
    /// <c>[-1]</c>.
    /// </remarks>
    internal static unsafe int[] BoneParents(IContentManager content, string key)
    {
        var studioModel = content.Load<StudioModel>(key);
        if (studioModel is null || studioModel.Header->numbones <= 0)
            return [-1];

        var bones = studioModel.Header->GetBones();
        var parents = new int[studioModel.Header->numbones];
        for (var i = 0; i < parents.Length; i++)
            parents[i] = bones[i].parent;

        return parents;
    }

    /// <summary>
    /// Seeds <paramref name="physics"/> with the model's bind pose, taken relative to each bone's
    /// anchor, so that the bones without a rigid body follow the simulated ancestor they hang off.
    /// </summary>
    /// <remarks>
    /// The bind pose is stored relative to the model root, and that root cancels out of an
    /// anchor-relative transform, so this works without knowing where the ragdoll will be placed.
    /// Both the bone hierarchy and the anchor mapping come from
    /// <see cref="PhysicsController.LoadSkeleton"/>, so this has to run after it.
    /// </remarks>
    private static unsafe void ApplyBindPoseSnapshot(IContentManager content, string key, PhysicsController physics)
    {
        var studioModel = content.Load<StudioModel>(key);
        if (studioModel is null || studioModel.Header->numbones <= 0)
            return;

        var bindPose = StudioPhysicsPrefabFactory.BuildBindPose(studioModel.Header, studioModel.Header->numbones);
        physics.ApplyPoseSnapshot(physics.CapturePoseSnapshot(bindPose));
    }

    /// <summary>
    /// Loads a studio model by its engine name, returning the precached <c>model_t*</c> and its index.
    /// Mirrors gsphysics' <c>CL_LoadModel(modname, &amp;index)</c> used to pin a ragdoll's model.
    /// </summary>
    private static unsafe model_t* LoadStudioModel(ClientEngineFuncs* engine, model_t* model, out int modelIndex)
    {
        modelIndex = 0;

        if (engine == null || engine->CL_LoadModel == null)
            return null;

        Span<byte> name = stackalloc byte[MaxModelNameLength];
        name.Clear();

        var length = 0;
        while (length < MaxModelNameLength - 1 && (byte)model->name[length] != 0)
        {
            name[length] = (byte)model->name[length];
            length++;
        }

        if (length == 0)
            return null;
        name[length] = 0;

        fixed (byte* namePointer = name)
        {
            var index = 0;
            var loaded = engine->CL_LoadModel((NChar*)namePointer, &index);
            modelIndex = index;
            return loaded;
        }
    }

    /// <summary>Loads a model by engine path, which also yields the index the engine refers to it by.</summary>
    private static unsafe model_t* LoadModelByName(ClientEngineFuncs* engine, string path, out int modelIndex)
    {
        modelIndex = 0;

        if (engine == null || engine->CL_LoadModel == null || path.Length >= MaxModelNameLength)
            return null;

        Span<byte> name = stackalloc byte[MaxModelNameLength];
        name.Clear();

        var length = Encoding.ASCII.GetBytes(path, name);
        if (length >= name.Length)
            return null;
        name[length] = 0;

        fixed (byte* namePointer = name)
        {
            var index = 0;
            var loaded = engine->CL_LoadModel((NChar*)namePointer, &index);
            modelIndex = index;
            return loaded;
        }
    }

    /// <summary>
    /// Physics resource key of a studio model path: its name with the extension replaced by
    /// <c>.gpd</c>, e.g. <c>models/barney.mdl</c> to <c>models/barney.gpd</c>.
    /// </summary>
    private static string? StudioPhysicsKey(string modelName)
    {
        if (modelName.Length < 5 || !modelName.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
            return null;

        return string.Concat(modelName.AsSpan(0, modelName.Length - 4), ".gpd");
    }

    /// <summary>Writes <paramref name="value"/> as null-terminated ASCII into <paramref name="buffer"/>.</summary>
    private static void WriteAscii(string value, Span<byte> buffer)
    {
        buffer.Clear();
        var written = Encoding.ASCII.GetBytes(value, buffer);
        if (written < buffer.Length)
            buffer[written] = 0;
    }

    // ── Constraints ───────────────────────────────────────────────────────────

    /// <summary>
    /// Re-points every cloned joint at the cloned bodies it should hold together, and copies the
    /// description the clone drops.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A joint is a component on body A's entity and refers to both ends by instance. Cloning copies
    /// the components of the entities in the clone, and the body on the other end lives on a different
    /// entity, so the copied joint keeps pointing at the original skeleton - whose bodies are about to
    /// be disabled and, being kinematic, were never going to produce motion anyway. That is why a
    /// ragdoll built without this step falls apart into separate bodies with no joints at all.
    /// </para>
    /// <para>
    /// The two skeletons are in the same order (the bones are cloned in the order they were loaded),
    /// and cloning preserves the order of an entity's components, so the n-th joint of a bone stays
    /// its n-th joint.
    /// </para>
    /// </remarks>
    private static void RemapConstraints(IReadOnlyList<Entity> skeletons, IReadOnlyList<Entity> clones)
    {
        var bodies = new Dictionary<BodyComponent, BodyComponent>();

        for (var i = 0; i < skeletons.Count && i < clones.Count; i++)
        {
            if (skeletons[i].Get<BodyComponent>() is { } source && clones[i].Get<BodyComponent>() is { } clone)
                bodies[source] = clone;
        }

        if (bodies.Count == 0)
            return;

        for (var i = 0; i < skeletons.Count && i < clones.Count; i++)
        {
            var source = ConstraintsOf(skeletons[i]);
            var clone = ConstraintsOf(clones[i]);

            for (var j = 0; j < source.Count && j < clone.Count; j++)
            {
                var from = (ITwoBody)source[j];
                var to = (ITwoBody)clone[j];

                if (from.A is { } a && bodies.TryGetValue(a, out var mappedA))
                    to.A = mappedA;

                if (from.B is { } b && bodies.TryGetValue(b, out var mappedB))
                    to.B = mappedB;

                // Cloning goes through the Stride binary serializer, which only carries [DataMember]
                // members. A joint's description (LocalOffset, axes, spring settings) lives in the
                // internal BepuConstraint field, which is not a data member, so the clone comes out
                // with every anchor at zero. Copy the real values over from the original joint.
                CopyConstraintDescription(source[j], clone[j]);
            }
        }
    }

    /// <summary>
    /// Copies a joint's description from an original joint to its clone.
    /// </summary>
    /// <remarks>
    /// Only the bodies are re-pointed by <see cref="RemapConstraints"/>; the description (the anchor
    /// offsets above all) is dropped by the clone because it lives in the joint's internal Bepu
    /// constraint, which the serializer skips. A joint cloned without this step keeps both anchors at
    /// the collider's center of mass - the middle of a limb - and the solver drags every bone toward
    /// those midpoints, which reads as the whole ragdoll being hurled sideways.
    /// </remarks>
    private static void CopyConstraintDescription(ConstraintComponentBase source, ConstraintComponentBase clone)
    {
        // v1: every joint is a ball socket. When hinge/cone get their own angle-limit components back,
        // those descriptions are copied here too.
        if (source is BallSocketConstraintComponent sourceBall && clone is BallSocketConstraintComponent cloneBall)
        {
            cloneBall.LocalOffsetA = sourceBall.LocalOffsetA;
            cloneBall.LocalOffsetB = sourceBall.LocalOffsetB;
            cloneBall.SpringFrequency = sourceBall.SpringFrequency;
            cloneBall.SpringDampingRatio = sourceBall.SpringDampingRatio;
        }
    }

    /// <summary>The joint components of an entity, in component order.</summary>
    private static List<ConstraintComponentBase> ConstraintsOf(Entity entity)
    {
        var constraints = new List<ConstraintComponentBase>();

        foreach (var component in entity.Components)
        {
            if (component is ConstraintComponentBase constraint)
                constraints.Add(constraint);
        }

        return constraints;
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
