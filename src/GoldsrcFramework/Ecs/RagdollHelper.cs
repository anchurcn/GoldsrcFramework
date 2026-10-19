using GoldsrcFramework.Content.Gpd;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using NativeInterop;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.Core;
using Stride.Engine;
using Stride.Engine.Design;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// The activities a Half-Life model dies on, as values of the HLSDK's <c>Activity</c> enum: the
    /// plain death run ACT_DIESIMPLE..ACT_DIEVIOLENT and the wound run
    /// ACT_DIE_HEADSHOT..ACT_DIE_BACKSHOT. Both are in <c>external/hlsdk/dlls/activity.h</c> (36-39
    /// and 66-69), and both are what the reference implementation treats as dying - see
    /// <see cref="IsPlayingDeathSequence"/>.
    /// </summary>
    private const int ActDieSimple = 36;
    private const int ActDieViolent = 39;
    private const int ActDieHeadshot = 66;
    private const int ActDieBackshot = 69;

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

    // ── Creation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a ragdoll for the given entity and adds it to the same root scene: the general form,
    /// cloning the skeleton the entity is already wearing.
    /// </summary>
    /// <param name="originalEntity">The entity whose physics skeleton is cloned.</param>
    /// <param name="modelName">Optional model name, only used to name the entity.</param>
    /// <param name="initialPose">
    /// Studio bone world-space matrices used to place the ragdoll's simulated bones, and kept as the
    /// pose its bones without a rigid body are drawn at, so they stay where they were at the moment of
    /// death.
    /// </param>
    /// <returns>The ragdoll entity, or null when it could not be created.</returns>
    public static unsafe Entity? CreateRagdollFor(
        Entity originalEntity,
        string? modelName,
        ReadOnlySpan<Matrix3x4> initialPose)
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

        // The behavior that keeps the entity transform in sync, counts the lifetime down, and is the
        // corpse's pose source. It is built before the client entity, which is pointed back at the
        // handle the behavior holds - see AttachClientEntity. The death pose goes to it rather than to
        // the controller, which keeps no pose of its own: the controller is handed it again on every
        // read.
        var behavior = new RagdollBehavior();
        behavior.SetDeathPose(initialPose);
        ragdollEntity.Components.Add(behavior);

        // 3. Give it the client entity the corpse is drawn through, before anything points at it. The
        //    temp entity depends on the model, not on the skeleton, so it can be allocated this early,
        //    and every component that carries it is then built with it already in place.
        var source = originalEntity.Get<Components.ClEntityComponent>()?.NativeEntity;
        if (source != null && source->model != null)
        {
            // Loading the model by name pins it for the corpse's lifetime.
            LoadStudioModel(EngineApi.PClient, source->model, out _);

            var clEntity = CreateRagdollTempEntFromTemplate(source, &source->curstate);
            if (clEntity != null)
                AttachClientEntity(ragdollEntity, behavior, clEntity);
        }

        // 4. Bring it into the scene first so the bodies land in the same simulation.
        rootScene.Entities.Add(ragdollEntity);

        // 5. Load and attach the cloned skeleton, reusing the original's bone hierarchy, then
        //    initialize its pose. Enable() must come before SetPose so the bodies exist in the
        //    simulation and Teleport actually writes into them.
        ragdollPhysics.LoadSkeleton(clonedBones, originPhysics.StudioBoneParents.Span);
        ragdollPhysics.Enable();
        ragdollPhysics.SetPose(initialPose);

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

        // The behavior that keeps the entity transform in sync, counts the lifetime down, and is the
        // corpse's pose source. It is built before the client entity, which is pointed back at the
        // handle the behavior holds - see AttachClientEntity. Bones without a rigid body (fingers,
        // face) have no entity of their own: the read-back places them relative to their nearest
        // simulated ancestor, and without a pose to place them by they would be left wherever the
        // buffer had them. A standalone ragdoll has no animation pose to hand over, so the pose to keep
        // is the model's bind pose.
        var behavior = new RagdollBehavior();
        behavior.SetDeathPose(BindPose(content, key));
        entity.Components.Add(behavior);

        // Give it the client entity the corpse is drawn through before anything points at it: the temp
        // entity depends on the model, not on the skeleton, so it can be allocated this early, and every
        // component that carries it is then built with it already in place.
        var spawn = origin;
        var clEntity = CreateRagdollTempEnt(&spawn, model);
        if (clEntity != null)
            AttachClientEntity(entity, behavior, clEntity);

        // The bones carry the bind pose relative to the model root, so placing the root places the
        // whole skeleton - no explicit pose is needed.
        entity.Transform.Position = new StrideVector3(origin.X, origin.Y, origin.Z);

        scene.Entities.Add(entity);

        physics.LoadSkeleton(prefab.Instantiate(), BoneParents(content, key));
        physics.Enable();

        return entity;
    }

    /// <summary>
    /// Asks a ragdoll to end its life, which the engine's temp entity walk then carries out on its
    /// next pass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The request is only a request: the corpse is still drawn, from its skeleton, until the walk
    /// comes round - see <see cref="RagdollTempEntUpdate"/> for why the teardown has to happen there
    /// rather than here, and <see cref="RagdollBehavior.CompleteRelease"/> for what it does. A caller
    /// that tears a corpse down itself runs after the walk has already put it into the frame, and can
    /// only watch the renderer draw it once more - from the animation pipeline, the pose source having
    /// just been taken away.
    /// </para>
    /// <para>
    /// Asking instead of doing is also what makes a corpse's two endings the same one: the stay timer
    /// running out and the entity that died coming back are both just a request, and the two corpses
    /// go on to look alike.
    /// </para>
    /// </remarks>
    public static void RequestRelease(Entity? ragdoll)
    {
        if (ragdoll is null)
            return;

        // The behaviour is what the walk looks a corpse up by, so asking it to retire is the whole of
        // the request. One without a behaviour - which creation never makes - cannot be reached by the
        // walk at all, so it leaves the scene here rather than waiting for a pass that will never come.
        if (ragdoll.Get<RagdollBehavior>() is { } behavior)
            behavior.RequestRelease();
        else
            ragdoll.Scene?.Entities.Remove(ragdoll);
    }

    // ── Linking ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Links a ragdoll to the client entity it is drawn through, in both directions at once.
    /// </summary>
    /// <remarks>
    /// The corpse is assembled around the client entity instead of being patched afterwards to point at
    /// one: the binding and the reference both exist the first time they are seen, so no component is
    /// ever handed an entity it was not built with.
    /// </remarks>
    private static unsafe void AttachClientEntity(Entity ragdoll, RagdollBehavior behavior, cl_entity_t* clEntity)
    {
        // The engine's slot is only a pointer back at the corpse, and the corpse - the behavior - is
        // what holds the handle it names: unlinking happens from an entity's exit, where the engine may
        // already have given the slot to somebody else, so the handle has to live on this side.
        clEntity->curstate.iuser4 = unchecked((int)behavior.AttachOwner(ragdoll));

        ragdoll.Components.Add(new Components.ClEntityComponent(clEntity));

        // Authority is Stride: the simulation owns the transform, and the Draw pass writes it back to
        // the client entity the corpse is drawn through.
        ragdoll.Components.Add(new GoldsrcTransformLinkComponent(
            new ClEntityTransformBinding(clEntity), TransformAuthority.Stride));
    }
    public unsafe static bool IsRagdoll(cl_entity_t* entity) => entity->curstate.renderfx == kRenderFxRagdoll;

    /// <summary>
    /// Whether <paramref name="entity"/> is playing a death sequence: the activity of the sequence the
    /// engine has its model on is one the model dies on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The client is never told that an entity died. What it can see is the sequence the server put it
    /// on, and the model itself says what activity each of its sequences is - so that is the question
    /// to ask before building a corpse for it, the same one the reference implementation asks
    /// (<c>CorpseManager::IsPlayingDeathSequence</c>, <c>phy_corpse.cpp</c>). Asked of the live state
    /// rather than remembered: a sequence that changes away from death is a respawn, and nothing of
    /// ours has to be kept in step with it.
    /// </para>
    /// <para>
    /// False for an entity that could not be rigged anyway - one that is not a studio model, or whose
    /// sequence index is out of range, or whose model carries no activity on its sequences at all. Such
    /// an entity is left to play its animation, which is what it would do without any physics.
    /// </para>
    /// </remarks>
    internal static unsafe bool IsPlayingDeathSequence(cl_entity_t* entity)
    {
        if (entity == null)
            return false;

        var model = entity->model;
        if (model == null || model->type != modtype_t.mod_studio)
            return false;

        var studio = EngineApi.PStudio;
        if (studio == null || studio->Mod_Extradata == null)
            return false;

        var header = (studiohdr_t*)studio->Mod_Extradata(model);
        if (header == null)
            return false;

        var sequence = entity->curstate.sequence;
        if (sequence < 0 || sequence >= header->numseq)
            return false;

        return IsDeathActivity((header->GetSequences() + sequence)->activity);
    }

    /// <summary>
    /// Whether a sequence activity is one of the ways a Half-Life model dies - see
    /// <see cref="ActDieSimple"/>.
    /// </summary>
    private static bool IsDeathActivity(int activity)
        => activity is >= ActDieSimple and <= ActDieViolent
            or >= ActDieHeadshot and <= ActDieBackshot;

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

    /// <summary>
    /// Writes the numbers that say where a corpse stood when it was retired.
    /// </summary>
    /// <remarks>
    /// This prints the temp entity's own origin, the state the engine copied out of it, the Stride root
    /// transform the sync pass writes from, the physics model root the pose is taken at, and how many
    /// transform links the sync processor owns - one line per corpse, never per frame. The origin is
    /// written from the model root just before this, so the two agreeing is what tells a corpse whose
    /// client entity is being driven apart from one that was only ever left at its spawn point, and the
    /// state is a frame older than both, having been copied when the walk last added it to a frame.
    /// </remarks>
    private static unsafe void LogReleaseState(Entity ragdoll, cl_entity_t* clEntity)
    {
        var origin = clEntity->origin;
        var state = &clEntity->curstate;

        var stridePosition = ragdoll.Transform.WorldMatrix.TranslationVector;

        var controller = ragdoll.Get<PhysicsController>();
        var rootPosition = controller is not null
            ? controller.GetModelRootTransform().TranslationVector
            : default(StrideVector3);

        var links = game?.TransformSync.Links.Count;

        Log("[gsf] release origin=" + Point(origin.X, origin.Y, origin.Z)
            + " curstate=" + Point(state->origin.X, state->origin.Y, state->origin.Z)
            + " stride=" + Point(stridePosition.X, stridePosition.Y, stridePosition.Z)
            + " root=" + (controller is not null ? Point(rootPosition.X, rootPosition.Y, rootPosition.Z) : "(none)")
            + " links=" + (links?.ToString() ?? "(none)"));
    }

    /// <summary>Formats a position for the diagnostic line above.</summary>
    private static string Point(float x, float y, float z) => $"({x:F1},{y:F1},{z:F1})";

    // ── Temp entity ───────────────────────────────────────────────────────────

    private static unsafe cl_entity_t* CreateRagdollTempEnt(Vector3* origin, model_t* model)
    {
        if (origin == null || model == null)
            return null;

        var engine = EngineApi.PClient;
        if (engine == null || engine->pEfxAPI == null || engine->pEfxAPI->CL_TentEntAllocCustom == null)
            return null;

        // The callback is the entity's lifetime, and where it is called from is what it is for: the
        // engine runs it from inside its temp entity walk, before the same walk decides whether the
        // entity may be drawn. Retiring an expired corpse there is the only place from which the frame
        // it expires on can be kept from drawing it - code that tears the corpse down runs after that
        // decision has already been made, and can only watch it be drawn once more.
        var tempEntity = engine->pEfxAPI->CL_TentEntAllocCustom(origin, model, 0, &RagdollTempEntUpdate);
        if (tempEntity == null)
            return null;

        tempEntity->flags |= TempEntityPersist;

        // Allocating with a callback leaves die at the current client time, which would have the walk
        // recycle the entity on the very next frame; the callback owns the lifetime instead.
        tempEntity->die = float.MaxValue;

        var ragdollEnt = &tempEntity->entity;
        ragdollEnt->curstate.renderfx = kRenderFxRagdoll;

        Log("[gsf] tent born origin=" + Point(origin->X, origin->Y, origin->Z));
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

    /// <summary>
    /// The corpse's side of the engine's temp entity walk: keeps the client entity where the
    /// simulation put it, and retires it once the corpse has asked to go.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Installed through <c>CL_TentEntAllocCustom</c>, so the engine calls it once per frame, from
    /// inside the walk that also decides whether the entity is drawn. That order is the whole point:
    /// retiring the entity here keeps it out of the frame it is retired on, whereas tearing the corpse
    /// down from the runtime's own update runs <em>after</em> the decision, by which time the entity is
    /// already part of the frame and is drawn once more - from the animation pipeline, since the pose
    /// source is what the teardown just removed.
    /// </para>
    /// <para>
    /// So this is where every corpse's life ends, and it only ever carries out what the corpse has
    /// already decided - see <see cref="RagdollBehavior.ReleaseRequested"/>. Nothing is decided here,
    /// which is what keeps the lifetime in one place: no clock and no cvar is read from inside the
    /// engine's walk.
    /// </para>
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void RagdollTempEntUpdate(TEMPENTITY* tempEntity, float frametime, float clientTime)
    {
        if (tempEntity == null)
            return;

        try
        {
            // A corpse that can no longer be resolved has been taken out of the scene by a path that did
            // not go through the walk - a map change. Nothing is left to drive it or to retire it, so it
            // is retired here rather than left holding its slot for ever.
            if (!TryGetBehavior(&tempEntity->entity, out var behavior) || behavior is null)
            {
                RetireTempEntity(tempEntity, clientTime);
                return;
            }
            if (!behavior.ReleaseRequested)
                return;

            LogReleaseState(behavior.Entity, &tempEntity->entity);

            // Retire first, then take the corpse down: the walk reads FTENT_NOMODEL as soon as this
            // returns, so the corpse is out of the frame it goes away on, and the walk drops it at its
            // head on the next pass, where the die written above has come to pass.
            RetireTempEntity(tempEntity, clientTime);
            behavior.CompleteRelease();
        }
        catch
        {
            // No managed exception may cross back into the engine's walk, and a corpse that cannot be
            // driven must not be left in front of the player: retire it and let the engine have it back.
            RetireTempEntity(tempEntity, clientTime);
        }
    }

    /// <summary>
    /// Takes a corpse's client entity out of the walking order: the rest of the walk skips it, and the
    /// next one finds <c>die</c> in the past and moves it back to the free list.
    /// </summary>
    /// <remarks>
    /// <c>FTENT_NOMODEL</c> is what the walk reads before it adds an entity to the frame, and leaving
    /// the other flags alone keeps the entity recognisable as a corpse for the remainder of its life.
    /// </remarks>
    private static unsafe void RetireTempEntity(TEMPENTITY* tempEntity, float clientTime)
    {
        tempEntity->die = clientTime;
        tempEntity->flags |= TempEntityNoModel;
    }

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
    /// The model's bind pose, in the form the corpse is handed its death pose: studio bone world-space
    /// matrices indexed by studio bone.
    /// </summary>
    /// <remarks>
    /// The pose is stored relative to the model root, and that root cancels out of the anchor-relative
    /// transform the controller folds out of it, so this works without knowing where the ragdoll will
    /// be placed. A model with no studio header yields an empty pose, which leaves the bones without a
    /// rigid body wherever the buffer has them.
    /// </remarks>
    private static unsafe Matrix3x4[] BindPose(IContentManager content, string key)
    {
        var studioModel = content.Load<StudioModel>(key);
        if (studioModel is null || studioModel.Header->numbones <= 0)
            return [];

        return StudioPhysicsPrefabFactory.BuildBindPose(studioModel.Header, studioModel.Header->numbones);
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
    /// and cloning preserves the order of an entity's components, so the n-th joint component of a
    /// bone stays its n-th. A joint is one to three components (a ball socket; a hinge and its twist
    /// limit; a cone's ball socket, swing limit and twist limit), and they are added to body A's
    /// entity in a fixed order by the factory, so pairing by position still lines every one of them
    /// up with its clone.
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
    /// offsets, the axes and the limits above all) is dropped by the clone because it lives in the
    /// joint's internal Bepu constraint, which the serializer skips. A joint cloned without this step
    /// keeps both anchors at the collider's center of mass - the middle of a limb - and the solver
    /// drags every bone toward those midpoints, which reads as the whole ragdoll being hurled
    /// sideways.
    /// <para>
    /// Every kind a joint can be built from is covered: the ball sockets, the hinge and its twist
    /// limit, and the cone's swing and twist limits - see
    /// <c>StudioPhysicsPrefabFactory.TryCreateConstraint</c> for what a joint is made of. A kind
    /// that is missed here comes out with its axes and angles at zero, which for a twist limit means
    /// the joint is welded at its rest pose rather than merely missing its range.
    /// </para>
    /// </remarks>
    private static void CopyConstraintDescription(ConstraintComponentBase source, ConstraintComponentBase clone)
    {
        switch (source, clone)
        {
            case (BallSocketConstraintComponent from, BallSocketConstraintComponent to):
                to.LocalOffsetA = from.LocalOffsetA;
                to.LocalOffsetB = from.LocalOffsetB;
                to.SpringFrequency = from.SpringFrequency;
                to.SpringDampingRatio = from.SpringDampingRatio;
                break;

            case (HingeConstraintComponent from, HingeConstraintComponent to):
                to.LocalOffsetA = from.LocalOffsetA;
                to.LocalHingeAxisA = from.LocalHingeAxisA;
                to.LocalOffsetB = from.LocalOffsetB;
                to.LocalHingeAxisB = from.LocalHingeAxisB;
                to.SpringFrequency = from.SpringFrequency;
                to.SpringDampingRatio = from.SpringDampingRatio;
                break;

            case (SwingLimitConstraintComponent from, SwingLimitConstraintComponent to):
                to.AxisLocalA = from.AxisLocalA;
                to.AxisLocalB = from.AxisLocalB;
                to.MinimumDot = from.MinimumDot;
                to.SpringFrequency = from.SpringFrequency;
                to.SpringDampingRatio = from.SpringDampingRatio;
                break;

            case (TwistLimitConstraintComponent from, TwistLimitConstraintComponent to):
                to.LocalBasisA = from.LocalBasisA;
                to.LocalBasisB = from.LocalBasisB;
                to.MinimumAngle = from.MinimumAngle;
                to.MaximumAngle = from.MaximumAngle;
                to.SpringFrequency = from.SpringFrequency;
                to.SpringDampingRatio = from.SpringDampingRatio;
                break;
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
