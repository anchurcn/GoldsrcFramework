using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using GoldsrcFramework.Ecs.Components;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.Graphics;
using NativeInterop;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using GoldsrcVector3 = GoldsrcFramework.LinearMath.Vector3;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace GoldsrcFramework.Ecs.Diagnostics;

/// <summary>
/// A client-side-only physics gun for poking at the framework's physics: aim at a dynamic body,
/// hold it with the left mouse button, freeze it with the right one. It is active only while the
/// crowbar is out and only when <c>gsf_physgun</c> is non-zero.
/// </summary>
/// <remarks>
/// <para>
/// The script lives on the local player's entity. That gives the gun the lifetime it wants - the
/// held body and the pinned bodies are per-player state - and it ticks in
/// <see cref="GoldsrcScriptSystem"/> (-90), after the scene has handed out collidables and before
/// <c>PhysicsGameSystem</c> (-49) integrates, so anything written here is part of the same step.
/// </para>
/// <para>
/// Holding is a constraint, not a velocity written every frame: one
/// <see cref="OneBodyLinearServoConstraintComponent"/> on the grabbed body whose
/// <c>Target</c> follows the crosshair. The body keeps its mass, keeps colliding, and drags the rest
/// of its skeleton along through the joints.
/// </para>
/// <para>
/// Nothing here writes a pose, so nothing has to convert between a bone origin and a center of mass:
/// a BEPU constraint offset is measured from the center of mass, and
/// <see cref="BodyComponent.Position"/> is the center-of-mass pose, so the two agree.
/// </para>
/// </remarks>
public sealed class PhysicsGunScript : ScriptComponentBase, IEnterExitCallable
{
    private const string CvarEnabled = "gsf_physgun";
    private const string CvarHoldDistance = "gsf_physgun_dist";
    private const string CvarMaxRange = "gsf_physgun_maxrange";
    private const string CvarStiffness = "gsf_physgun_stiffness";
    private const string CvarDamping = "gsf_physgun_damping";
    private const string CvarSpeed = "gsf_physgun_speed";
    private const string CvarForce = "gsf_physgun_force";
    private const string CvarEyeHeight = "gsf_physgun_eye";

    /// <summary>Command that prints what the gun can see and why it is not working.</summary>
    private const string StatusCommand = "gsf_physgun_status";

    private const float DefaultEnabled = 1f;

    private const float DefaultMaxRange = 128f;
    private const float DefaultStiffness = 30f;
    private const float DefaultDamping = 5f;
    private const float DefaultSpeed = 1000f;
    private const float DefaultForce = 100_000f;
    private const float DefaultEyeHeight = 28f;

    private const string AttackButton = "+attack";
    private const string Attack2Button = "+attack2";

    /// <summary>Closest a held body is ever kept to the eye, in units.</summary>
    private const float MinHoldDistance = 16f;

    /// <summary>How many pinned bodies are outlined at once, so a wall of frozen corpses stays cheap.</summary>
    private const int MaxPinnedHighlights = 32;

    private static readonly DebugDrawCommandList Overlay = new();

    /// <summary>
    /// The lines the gun authors itself: the pick ray, the hold line and their crosses.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Overlay"/> for one reason: these are drawn with depth testing forced
    /// off, so the ray is visible even when it ends inside a wall - which is precisely the case a
    /// misdirected ray produces. The collider outlines stay in <see cref="Overlay"/> and follow the
    /// <c>gsf_physdtest</c> cvar like the physics wireframe does.
    /// </remarks>
    private static readonly DebugDrawCommandList RayOverlay = new();

    private static readonly Color4 HoldColor = new(1f, 0.9f, 0.2f, 1f);

    /// <summary>The ray reached a body the gun may hold.</summary>
    private static readonly Color4 GrabRayColor = new(0.3f, 1f, 0.3f, 1f);

    /// <summary>The ray reached a body the gun froze earlier.</summary>
    private static readonly Color4 PinRayColor = new(0.4f, 0.7f, 1f, 1f);

    /// <summary>The ray reached something the gun is not allowed to hold.</summary>
    private static readonly Color4 RefuseRayColor = new(1f, 0.45f, 0.2f, 1f);

    /// <summary>The ray reached nothing at all, so it ends at its maximum range.</summary>
    private static readonly Color4 MissRayColor = new(0.7f, 0.7f, 0.7f, 1f);

    /// <summary>There is no simulation to cast into yet.</summary>
    private static readonly Color4 NoPhysicsRayColor = new(1f, 0.2f, 0.2f, 1f);

    /// <summary>Buttons already reported as unbound, so the warning is printed once each.</summary>
    private static readonly HashSet<string> reportedButtons = [];

    /// <summary>Set once the legacy client library has proven unable to answer key lookups.</summary>
    private static bool buttonsUnavailable;

    /// <summary>The instance the overlay currently belongs to; the draw hook has no registry to ask.</summary>
    private static PhysicsGunScript? overlayOwner;

    /// <summary>The instance that is currently attached and ticking, for the status command.</summary>
    private static PhysicsGunScript? active;

    private static SceneInstance? attachedScene;

    /// <summary>Bodies this gun froze. A pin is a property of a body, not a state the gun sits in.</summary>
    private readonly HashSet<BodyComponent> pinned = [];

    private readonly List<BodyComponent> pinnedScratch = [];

    private BepuConfiguration? physics;
    private PhysicsGunTarget target = PhysicsGunTarget.None;
    private BodyComponent? heldBody;
    private Entity? heldBone;
    private OneBodyLinearServoConstraintComponent? heldConstraint;
    private float holdDistance = 80f;
    private bool leftDown;
    private bool rightDown;
    private bool failed;
    private bool announced;
    private bool reportedNoPhysics;

    // ── Host wiring ───────────────────────────────────────────────────────

    /// <summary>
    /// Registers the console variables and arranges for the gun to be attached to the local player's
    /// entity when that entity appears.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attachment goes through <c>SceneInstance.EntityAdded</c>, the same event
    /// <see cref="SceneEntityLifecycleSystem"/> uses to drive <c>IEnterExitCallable.OnEnter</c>. The
    /// entity is already registered with the processors by the time that event is raised - Stride
    /// raises it at the end of <c>InternalAddEntity</c> - so adding a component here is not a mutation
    /// during a scan.
    /// </para>
    /// <para>
    /// The local player's entity is rebuilt on a map change and this needs no matching teardown: the
    /// script dies with the entity, so a fresh one starts with an empty hold and an empty pin set.
    /// </para>
    /// </remarks>
    public static void Initialize(GoldsrcClientGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        RegisterConsole();

        if (ReferenceEquals(attachedScene, game.SceneInstance))
            return;

        if (attachedScene is not null)
            attachedScene.EntityAdded -= OnEntityAdded;

        attachedScene = game.SceneInstance;
        attachedScene.EntityAdded += OnEntityAdded;
    }

    /// <summary>
    /// Flushes the overlay the gun drew this frame. Called from the engine's draw loop, which has no
    /// service registry to ask, hence the static slot.
    /// </summary>
    public static void DrawOverlay(bool depthTest)
    {
        // The gun's own lines are never hidden by world geometry; the collider outlines follow the
        // same depth-test switch as the physics wireframe.
        TriApiLineDraw.DrawLineVertices(RayOverlay.LineVertices, false);
        TriApiLineDraw.DrawLineVertices(Overlay.LineVertices, depthTest);
    }

    private static unsafe void OnEntityAdded(object? sender, Entity entity)
    {
        var engine = EngineApi.PClient;
        if (engine is null || engine->GetLocalPlayer == null)
            return;

        var local = engine->GetLocalPlayer();
        if (local == null)
            return;

        // Pointer identity rather than an index: this is the same cl_entity_t the engine hands to
        // HUD_AddEntity for the local player.
        if (entity.Get<ClEntityComponent>() is not { } cl || cl.NativeEntity != local)
            return;

        Attach(entity);
    }

    /// <summary>
    /// Attaches the gun to the local player's entity if it is not attached yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from <c>HUD_AddEntity</c> as well as from the scene's <c>EntityAdded</c> event. The event
    /// is the cleaner place, but it fires once and silently does nothing when the entity is not the one
    /// the gun is looking for; this call runs every frame the engine asks about the local player, so a
    /// gun that somehow never attached heals on the next frame instead of never working at all. The
    /// whole call is a pointer comparison and two component lookups.
    /// </para>
    /// <para>
    /// It runs from the engine's own frame, which is before the Stride tick, so adding a component
    /// here is not a mutation during a processor pass.
    /// </para>
    /// </remarks>
    public static unsafe void EnsureAttached(ClientSceneManagementSystem sceneManagement, cl_entity_t* nativeEntity)
    {
        var engine = EngineApi.PClient;
        if (engine is null || engine->GetLocalPlayer == null)
            return;

        var local = engine->GetLocalPlayer();
        if (local == null || nativeEntity != local)
            return;

        if (sceneManagement.GetEntity(local->index) is { } entity)
            Attach(entity);
    }

    private static void Attach(Entity entity)
    {
        if (entity.Get<PhysicsGunScript>() is not null)
            return;

        entity.Components.Add(new PhysicsGunScript());
    }

    // ── Script lifecycle ──────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the per-entity state.
    /// </summary>
    /// <remarks>
    /// <see cref="Start"/> is not called once per instance: an entity that leaves the scene and
    /// re-enters is registered again and <see cref="Start"/> runs a second time on the same instance
    /// (<see cref="HalfLifeBehavior"/> documents the same behaviour). Everything here is therefore
    /// safe to redo, and nothing assumes it created anything.
    /// </remarks>
    public override void Start()
    {
        active = this;
        physics = null;
        leftDown = false;
        rightDown = false;
        failed = false;
        announced = false;
        reportedNoPhysics = false;
    }

    public void OnEnter()
    {
        // Everything the gun needs is resolved lazily; entering the scene is enough.
    }

    /// <summary>
    /// Drops the hold when the entity leaves the scene.
    /// </summary>
    /// <remarks>
    /// The script stops being updated the moment its entity is out of the scene, but a constraint it
    /// left behind would keep pulling the body towards a target nothing refreshes any more. This is
    /// also what makes a map change safe: the entities are torn down, and the state goes with them.
    /// </remarks>
    public void OnExit()
    {
        ReleaseHold("the local player left the scene");

        target = PhysicsGunTarget.None;

        if (ReferenceEquals(active, this))
            active = null;

        if (ReferenceEquals(overlayOwner, this))
        {
            overlayOwner = null;
            Overlay.Clear();
            RayOverlay.Clear();
        }
    }

    public override void Update(GameTime gameTime)
    {
        Overlay.Clear();
        RayOverlay.Clear();

        if (failed)
            return;

        try
        {
            UpdateGun();
        }
        catch (Exception exception)
        {
            // A debug tool must not take the frame down with it, and a failure that repeats every
            // frame would be worse than one that stops.
            failed = true;
            Print($"gsf_physgun: stopped after {exception.GetType().Name}: {exception.Message}\n");
        }
    }

    // ── Frame ─────────────────────────────────────────────────────────────

    private unsafe void UpdateGun()
    {
        var engine = EngineApi.PClient;
        if (engine is null)
            return;

        if (ReadCvar(CvarEnabled, DefaultEnabled) == 0f)
        {
            announced = false;
            ReleaseHold("the gun was switched off");
            return;
        }

        // The gate is the one thing that fails silently - a crowbar whose view model is named
        // something else leaves the gun switched on and doing nothing - so say what arms it.
        if (!announced)
        {
            announced = true;
            Print("gsf_physgun: on; hold the crowbar and aim at a dynamic body, or run gsf_physgun_status\n");
        }

        // Resolved late: the physics configuration only exists once something collidable has been
        // attached, which is not necessarily the frame this script starts on. Not having one is not a
        // reason to draw nothing - a red ray still shows where the gun is aiming.
        physics ??= Entity.EntityManager?.Services?.GetService<BepuConfiguration>();
        var physicsReady = physics is not null;

        if (!physicsReady && !reportedNoPhysics)
        {
            reportedNoPhysics = true;
            Print("gsf_physgun: no physics configuration yet, so there is nothing to cast into\n");
        }

        if (!TryGetAim(engine, out var eye, out var aim, out _))
            return;

        var maxRange = ReadCvar(CvarMaxRange, DefaultMaxRange);
        target = physicsReady ? Pick(eye, aim, maxRange) : PhysicsGunTarget.None;

        if (!physicsReady)
        {
            overlayOwner = this;
            BuildOverlay(eye, aim, maxRange, false);
            return;
        }

        var armed = IsCrowbarOut(engine);
        var left = IsButtonHeld(AttackButton);
        var right = IsButtonHeld(Attack2Button);
        var leftPressed = left && !leftDown;
        var rightPressed = right && !rightDown;
        leftDown = left;
        rightDown = right;

        if (heldBody is not null)
            UpdateHold(eye, aim, armed, left, rightPressed);
        else if (armed)
            UpdateIdle(eye, aim, leftPressed, rightPressed);

        overlayOwner = this;
        BuildOverlay(eye, aim, maxRange, true);
    }

    private void UpdateHold(StrideVector3 eye, StrideVector3 aim, bool armed, bool left, bool rightPressed)
    {
        // The body can vanish under us: a corpse is released when its owner respawns, and a map
        // change takes every entity with it.
        if (heldBone is null || heldBone.Scene is null)
        {
            ForgetHold();
            return;
        }

        if (!armed)
        {
            ReleaseHold("the crowbar is no longer out");
            return;
        }

        if (!left)
        {
            ReleaseHold("left button released", notify: true);
            return;
        }

        if (rightPressed)
        {
            PinHeld();
            return;
        }

        if (heldConstraint is not null)
            heldConstraint.Target = eye + aim * holdDistance;
    }

    private void UpdateIdle(StrideVector3 eye, StrideVector3 aim, bool leftPressed, bool rightPressed)
    {
        if (leftPressed)
        {
            if (target.CanHold)
                Grab(eye, aim);
            else if (target.HasHit)
                Notify($"gsf_physgun: not grabbing {Describe(target)}: {Refusal(target.Verdict)}");

            return;
        }

        if (rightPressed && target.Verdict == PhysicsGunVerdict.Pinned && target.Body is { } body)
            Unpin(body);
    }

    // ── Picking ───────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the closest thing in front of the eye and classifies it.
    /// </summary>
    /// <remarks>
    /// The ray goes to the simulations rather than through <c>EV_PlayerTrace</c>, which only walks the
    /// client's predicted physents: a ragdoll corpse is drawn through a client-created tent entity
    /// that is not in that list, so the engine's own trace would pass straight through the body the
    /// gun is aimed at.
    /// </remarks>
    private PhysicsGunTarget Pick(StrideVector3 eye, StrideVector3 aim, float maxRange)
    {
        if (physics is null)
            return PhysicsGunTarget.None;

        var best = PhysicsGunTarget.None;
        var bestDistance = float.MaxValue;

        foreach (var simulation in physics.BepuSimulations)
        {
            if (!simulation.RayCast(eye, aim, maxRange, out var hit))
                continue;

            if (hit.Distance >= bestDistance)
                continue;

            bestDistance = hit.Distance;
            best = PhysicsGunTarget.Classify(hit, hit.Collidable is BodyComponent hitBody && pinned.Contains(hitBody));
        }

        return best;
    }

    // ── Grab, pin, release ────────────────────────────────────────────────

    private void Grab(StrideVector3 eye, StrideVector3 aim)
    {
        if (target.Body is not { } body || body.Entity is not { } bone)
            return;

        var configuredDistance = ReadCvar(CvarHoldDistance, 0f);
        holdDistance = configuredDistance > 0f
            ? configuredDistance
            : Math.Clamp(target.Distance, MinHoldDistance, ReadCvar(CvarMaxRange, DefaultMaxRange));

        // The grab point is where the ray met the surface, and a constraint offset is measured in the
        // body's own frame from its center of mass - which is exactly what this difference is.
        var fromCenterOfMass = target.Point - body.Position;
        var localOffset = StrideVector3.Transform(fromCenterOfMass, Quaternion.Invert(body.Orientation));

        var servo = new OneBodyLinearServoConstraintComponent
        {
            A = body,
            LocalOffset = localOffset,
            Target = eye + aim * holdDistance,
            SpringFrequency = ReadCvar(CvarStiffness, DefaultStiffness),
            SpringDampingRatio = ReadCvar(CvarDamping, DefaultDamping),
            ServoMaximumSpeed = ReadCvar(CvarSpeed, DefaultSpeed),
            ServoMaximumForce = ReadCvar(CvarForce, DefaultForce),
        };

        bone.Components.Add(servo);

        heldBody = body;
        heldBone = bone;
        heldConstraint = servo;

        Notify($"gsf_physgun: holding {Describe(target)}, {holdDistance:0} units from the eye");
    }

    /// <summary>
    /// Freezes the held body where it is and lets go of it.
    /// </summary>
    /// <remarks>
    /// Only the held body is frozen, not its skeleton. That is what turns it into an anchor of
    /// infinite mass with the rest of the rig still hanging off it through live joints, like a corpse
    /// on a hook. Freezing the whole skeleton (<c>PhysicsController.MotionType = Kinematic</c>) would
    /// also clear <c>constraint.Enabled</c> and leave the bones held together by nothing - a frozen
    /// rig that comes apart as soon as it is moved.
    /// </remarks>
    private void PinHeld()
    {
        if (heldBody is not { } body)
            return;

        var force = heldConstraint is null ? 0f : heldConstraint.GetAccumulatedForceMagnitude();
        var name = body.Entity?.Name ?? "body";

        RemoveConstraint();
        heldBody = null;

        body.LinearVelocity = StrideVector3.Zero;
        body.AngularVelocity = StrideVector3.Zero;
        body.Kinematic = true;

        pinned.Add(body);

        Notify($"gsf_physgun: pinned {name} (the servo was applying {force:0} units of force)");
    }

    private void Unpin(BodyComponent body)
    {
        if (!pinned.Remove(body))
            return;

        var name = body.Entity?.Name ?? "body";

        body.LinearVelocity = StrideVector3.Zero;
        body.AngularVelocity = StrideVector3.Zero;
        body.Kinematic = false;

        Notify($"gsf_physgun: unpinned {name}");
    }

    /// <summary>Removes the servo constraint and forgets the held body.</summary>
    /// <param name="reason">Why the hold ended, for the console line.</param>
    /// <param name="notify">
    /// True for reasons the player caused, which are also shown at the centre of the screen. The
    /// automatic ones (the gun was switched off, the crowbar went away) only go to the console.
    /// </param>
    private void ReleaseHold(string reason, bool notify = false)
    {
        if (heldBody is null && heldConstraint is null)
            return;

        RemoveConstraint();
        heldBody = null;

        var message = $"gsf_physgun: let go ({reason})";

        if (notify)
            Notify(message);
        else
            Print(message + "\n");
    }

    /// <summary>Drops the hold without touching the body, for a body that is already gone.</summary>
    private void ForgetHold()
    {
        heldConstraint = null;
        heldBone = null;
        heldBody = null;

        Print("gsf_physgun: let go (the body left the scene)\n");
    }

    /// <summary>
    /// Detaches the servo from the bone entity.
    /// </summary>
    /// <remarks>
    /// Removal goes through the entity's component collection, which is what the
    /// <c>ConstraintProcessor</c> watches: it drops the constraint from the solver on its next pass
    /// (-900). Until then the body is still being pulled, so a release lags one frame.
    /// </remarks>
    private void RemoveConstraint()
    {
        if (heldConstraint is { } constraint && heldBone is { } bone && bone.Components.Contains(constraint))
            bone.Components.Remove(constraint);

        heldConstraint = null;
        heldBone = null;
    }

    // ── Visual feedback ───────────────────────────────────────────────────

    private void BuildOverlay(StrideVector3 eye, StrideVector3 aim, float maxRange, bool physicsReady)
    {
        // The ray itself, always drawn: where the gun is aiming, how far it gets, and what stopped it.
        // This is the first thing to look at when the gun seems to do nothing, because it tells "aimed
        // the wrong way" apart from "aimed at something that cannot be held".
        var end = target.HasHit ? target.Point : eye + aim * maxRange;
        var rayColor = !physicsReady ? NoPhysicsRayColor : target.Verdict switch
        {
            PhysicsGunVerdict.Grabbable => GrabRayColor,
            PhysicsGunVerdict.Pinned => PinRayColor,
            PhysicsGunVerdict.Nothing => MissRayColor,
            _ => RefuseRayColor,
        };

        RayOverlay.AddLine(eye, end, rayColor);
        AddCross(end, rayColor, Math.Clamp((end - eye).Length() * 0.05f, 2f, 8f));

        // The wireframe builder picks its own colours (green awake, dark green asleep, grey static),
        // so the outline only has to say *which* collidable the gun is talking about.
        if (target.Collidable is { } picked && target.Verdict is PhysicsGunVerdict.Grabbable or PhysicsGunVerdict.Pinned)
            ColliderWireframeBuilder.Emit(Overlay, picked);

        if (heldBody is { } held)
        {
            var point = eye + aim * holdDistance;

            RayOverlay.AddLine(eye, point, HoldColor);
            AddCross(point, HoldColor, 4f);
            ColliderWireframeBuilder.Emit(Overlay, held);
        }

        foreach (var body in CollectLivePinned())
            ColliderWireframeBuilder.Emit(Overlay, body);
    }

    /// <summary>
    /// Drops pinned bodies whose entity is gone and returns the rest, capped for drawing.
    /// </summary>
    /// <remarks>
    /// A pinned body outlives the gun's interest in it: the corpse it belongs to is released when its
    /// owner respawns. Without pruning, the set would grow for the whole map and the overlay would
    /// ask a detached body for a pose.
    /// </remarks>
    private List<BodyComponent> CollectLivePinned()
    {
        pinnedScratch.Clear();

        if (pinned.Count == 0)
            return pinnedScratch;

        foreach (var body in pinned)
        {
            if (body.Entity is { Scene: not null })
                pinnedScratch.Add(body);
        }

        if (pinnedScratch.Count != pinned.Count)
        {
            pinned.Clear();

            foreach (var body in pinnedScratch)
                pinned.Add(body);
        }

        if (pinnedScratch.Count > MaxPinnedHighlights)
            pinnedScratch.RemoveRange(MaxPinnedHighlights, pinnedScratch.Count - MaxPinnedHighlights);

        return pinnedScratch;
    }

    private static void AddCross(StrideVector3 point, Color4 color, float size)
    {
        var axis = StrideVector3.UnitX * size;
        RayOverlay.AddLine(point - axis, point + axis, color);

        axis = StrideVector3.UnitY * size;
        RayOverlay.AddLine(point - axis, point + axis, color);

        axis = StrideVector3.UnitZ * size;
        RayOverlay.AddLine(point - axis, point + axis, color);
    }

    // ── Engine reads ──────────────────────────────────────────────────────

    private static StrideVector3 ToStride(in GoldsrcVector3 value) => new(value.X, value.Y, value.Z);

    /// <summary>
    /// Where the pick ray starts and which way it points, or false when the engine cannot say.
    /// </summary>
    /// <remarks>
    /// Shared by the frame and by the status command, so that what the status command reports is
    /// exactly what the ray is cast with rather than a second calculation that could drift.
    /// </remarks>
    private static unsafe bool TryGetAim(ClientEngineFuncs* engine, out StrideVector3 eye, out StrideVector3 aim, out GoldsrcVector3 angles)
    {
        eye = default;
        aim = default;
        angles = default;

        var local = engine->GetLocalPlayer != null ? engine->GetLocalPlayer() : null;
        if (local == null || engine->GetViewAngles == null || engine->AngleVectors == null)
            return false;

        // Named locals rather than the out parameters: only a local's address can be taken here, since
        // an out parameter may live somewhere the GC is free to move.
        var viewAngles = default(GoldsrcVector3);
        engine->GetViewAngles((float*)&viewAngles);

        var forward = default(GoldsrcVector3);
        engine->AngleVectors((float*)&viewAngles, (float*)&forward, null, null);

        angles = viewAngles;

        aim = new StrideVector3(forward.X, forward.Y, forward.Z);
        if (aim.LengthSquared() < 0.0001f)
            return false;

        aim.Normalize();

        eye = ToStride(local->origin);
        eye.Z += ReadEyeHeight(engine);

        return true;
    }

    /// <summary>
    /// Height of the eye above the local player's origin.
    /// </summary>
    /// <remarks>
    /// <c>EV_LocalPlayerViewheight</c> is the engine's own answer, but its contract is not stated
    /// anywhere in this repository: it may fill a three-element offset or a single height. The buffer
    /// is zeroed first so that an implausible z is recognisable, and the console variable is the
    /// fallback.
    /// </remarks>
    private static unsafe float ReadEyeHeight(ClientEngineFuncs* engine)
    {
        var fallback = ReadCvar(CvarEyeHeight, DefaultEyeHeight);

        var eventApi = engine->pEventAPI;
        if (eventApi == null || eventApi->EV_LocalPlayerViewheight == null)
            return fallback;

        var offset = StrideVector3.Zero;
        eventApi->EV_LocalPlayerViewheight((float*)&offset);

        return offset.Z is > 0.5f and < 200f ? offset.Z : fallback;
    }

    /// <summary>True while the local player's view model is a crowbar.</summary>
    /// <remarks>
    /// Matched on the model name rather than on a weapon id: the id lives in <c>weapon_data_t</c>,
    /// which only reaches the client through <c>HUD_TxferPredictionData</c>, and hooking that for a
    /// debug tool is not worth it yet. A substring rather than a full path so a mod that renames the
    /// view model still works - a view model is only ever a crowbar's when it says crowbar.
    /// </remarks>
    private static unsafe bool IsCrowbarOut(ClientEngineFuncs* engine)
    {
        if (engine->GetViewModel == null)
            return false;

        var viewModel = engine->GetViewModel();
        if (viewModel == null || viewModel->model == null)
            return false;

        return ModelNameContains(viewModel->model, "crowbar");
    }

    private static unsafe bool ModelNameContains(model_t* model, string needle)
    {
        Span<byte> name = stackalloc byte[64];
        var text = name[..ReadModelName(model, name)];

        for (var start = 0; start + needle.Length <= text.Length; start++)
        {
            var match = true;

            for (var i = 0; i < needle.Length && match; i++)
                match = ToLower(text[start + i]) == ToLower((byte)needle[i]);

            if (match)
                return true;
        }

        return false;
    }

    /// <summary>Copies a model's name into <paramref name="buffer"/> and returns how much of it is used.</summary>
    private static unsafe int ReadModelName(model_t* model, Span<byte> buffer)
    {
        var length = 0;

        while (length < buffer.Length - 1 && (byte)model->name[length] != 0)
        {
            buffer[length] = (byte)model->name[length];
            length++;
        }

        return length;
    }

    private static byte ToLower(byte value)
        => value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value + 32) : value;

    /// <summary>
    /// Whether a <c>+command</c> key button is currently down.
    /// </summary>
    /// <remarks>
    /// Read from the key buttons the engine itself drives: the legacy client library owns them and
    /// the engine resolves them through our <c>KB_Find</c> export, so this is the same object the
    /// engine writes when the binding goes down. The alternative, <c>usercmd_t.buttons</c> inside
    /// <c>CL_CreateMove</c>, runs several times per frame as prediction re-runs commands, and a
    /// side-effecting action must not fire more than once.
    /// </remarks>
    private static unsafe bool IsButtonHeld(string button)
    {
        if (buttonsUnavailable)
            return false;

        Span<byte> name = stackalloc byte[32];
        if (!TryWriteAscii(button, name))
            return false;

        fixed (byte* pointer = name)
        {
            try
            {
                var key = LegacyClientInterop.KB_Find((NChar*)pointer);

                if (key == null)
                {
                    if (reportedButtons.Add(button))
                        Print($"gsf_physgun: '{button}' is not bound, so the gun cannot see its buttons\n");

                    return false;
                }

                // Low bit is the down state; the second bit is a one-shot pulse the legacy client
                // consumes for its own weapon code and is deliberately not used here.
                return (key->state & 1) != 0;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
            {
                buttonsUnavailable = true;
                Print($"gsf_physgun: key buttons are unreadable ({exception.GetType().Name})\n");
                return false;
            }
        }
    }

    // ── Console ───────────────────────────────────────────────────────────

    private static unsafe void RegisterConsole()
    {
        var engine = EngineApi.PClient;
        if (engine is null || engine->RegisterVariable == null || engine->GetCvarPointer == null)
            return;

        RegisterCvar(engine, CvarEnabled, DefaultEnabled.ToString("0"));
        RegisterCvar(engine, CvarHoldDistance, "0");
        RegisterCvar(engine, CvarMaxRange, DefaultMaxRange.ToString("0"));
        RegisterCvar(engine, CvarStiffness, DefaultStiffness.ToString("0"));
        RegisterCvar(engine, CvarDamping, DefaultDamping.ToString("0"));
        RegisterCvar(engine, CvarSpeed, DefaultSpeed.ToString("0"));
        RegisterCvar(engine, CvarForce, DefaultForce.ToString("0"));
        RegisterCvar(engine, CvarEyeHeight, DefaultEyeHeight.ToString("0"));

        RegisterCommand(engine, StatusCommand, &PrintStatusCommand);

        Print("gsf_physgun: ready - the crowbar arms it; 'gsf_physgun 0' disables it, "
            + "'gsf_physgun_status' explains what it sees\n");
    }

    private static unsafe void RegisterCommand(ClientEngineFuncs* engine, string name, delegate* unmanaged[Cdecl]<void> handler)
    {
        if (engine->AddCommand == null)
            return;

        Span<byte> buffer = stackalloc byte[64];
        if (!TryWriteAscii(name, buffer))
            return;

        fixed (byte* pointer = buffer)
            engine->AddCommand(NCharPtr.From(pointer), handler);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void PrintStatusCommand()
    {
        try
        {
            PrintStatus();
        }
        catch (Exception exception)
        {
            Print($"gsf_physgun: status failed with {exception.GetType().Name}\n");
        }
    }

    /// <summary>
    /// Prints every link in the chain between "the crowbar is out" and "a body is moving", so that a
    /// gun that does nothing can be diagnosed without guessing which link broke.
    /// </summary>
    private static unsafe void PrintStatus()
    {
        var engine = EngineApi.PClient;
        if (engine is null)
        {
            Print("gsf_physgun: there is no client engine yet\n");
            return;
        }

        Print($"gsf_physgun: cvar                  {(ReadCvar(CvarEnabled, DefaultEnabled) != 0f ? "on" : "off")}\n");

        var local = engine->GetLocalPlayer != null ? engine->GetLocalPlayer() : null;
        Print($"gsf_physgun: local player          {(local == null ? "not available" : $"entity {local->index}")}\n");

        if (active is not { } instance)
        {
            Print("gsf_physgun: script                not attached - nothing can run\n");
            Print("gsf_physgun: (it attaches when the engine first reports the local player entity)\n");
            return;
        }

        Print($"gsf_physgun: script                attached to {instance.Entity?.Name ?? "the player entity"}\n");
        Print($"gsf_physgun: physics configuration {(instance.physics is null ? "not resolved yet" : "ready")}\n");
        Print($"gsf_physgun: view model            {ViewModelName(engine)}"
            + $"{(IsCrowbarOut(engine) ? " - gate open" : " - gate closed, the gun is not armed")}\n");
        Print($"gsf_physgun: '{AttackButton}' bound        {(ButtonIsBound(AttackButton) ? "yes" : "no")}\n");
        Print($"gsf_physgun: '{Attack2Button}' bound       {(ButtonIsBound(Attack2Button) ? "yes" : "no")}\n");
        Print($"gsf_physgun: what the crosshair sees {CrosshairSummary(instance)}\n");
        Print($"gsf_physgun: hold                  {(instance.heldBody is null ? "nothing" : Describe(instance.target))}\n");
        Print($"gsf_physgun: pinned bodies         {instance.pinned.Count}\n");

        // The numbers behind the ray, for when the picture and the expectation disagree.
        if (TryGetAim(engine, out var eye, out var aim, out var angles))
        {
            Print($"gsf_physgun: eye                   {eye.X:0.#} {eye.Y:0.#} {eye.Z:0.#}"
                + $"  (player origin z {PlayerOriginZ(engine):0.#} + eye {ReadEyeHeight(engine):0.#})\n");
            Print($"gsf_physgun: view angles (p y r)   {angles.X:0.#} {angles.Y:0.#} {angles.Z:0.#}\n");
            Print($"gsf_physgun: aim direction         {aim.X:0.###} {aim.Y:0.###} {aim.Z:0.###}\n");
        }
        else
        {
            Print("gsf_physgun: eye / aim             the engine would not say\n");
        }
    }

    private static unsafe float PlayerOriginZ(ClientEngineFuncs* engine)
    {
        var local = engine->GetLocalPlayer != null ? engine->GetLocalPlayer() : null;
        return local == null ? 0f : local->origin.Z;
    }

    private static string CrosshairSummary(PhysicsGunScript instance)
    {
        var pick = instance.target;

        if (!pick.HasHit)
            return "nothing within range";

        return pick.CanHold
            ? $"{Describe(pick)} - holdable"
            : $"{Describe(pick)} - {Refusal(pick.Verdict)}";
    }

    /// <summary>Whether a key button exists, without printing the warning <see cref="IsButtonHeld"/> would.</summary>
    private static unsafe bool ButtonIsBound(string button)
    {
        if (buttonsUnavailable)
            return false;

        Span<byte> name = stackalloc byte[32];
        if (!TryWriteAscii(button, name))
            return false;

        fixed (byte* pointer = name)
        {
            try
            {
                return LegacyClientInterop.KB_Find((NChar*)pointer) != null;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
            {
                return false;
            }
        }
    }

    private static unsafe string ViewModelName(ClientEngineFuncs* engine)
    {
        if (engine->GetViewModel == null)
            return "unavailable";

        var viewModel = engine->GetViewModel();
        if (viewModel == null || viewModel->model == null)
            return "none";

        Span<byte> name = stackalloc byte[64];
        var length = ReadModelName(viewModel->model, name);

        return length == 0 ? "none" : Encoding.ASCII.GetString(name[..length]);
    }

    /// <summary>
    /// Reports an outcome to both the console and the centre of the screen.
    /// </summary>
    /// <remarks>
    /// The console alone is not enough: a gun that does nothing while the console is closed is
    /// indistinguishable from a gun that was never installed.
    /// </remarks>
    private static unsafe void Notify(string message)
    {
        Print(message + "\n");

        if (EngineApi.PClient != null)
            EngineApi.DrawStringCenter(message);
    }

    private static unsafe void RegisterCvar(ClientEngineFuncs* engine, string name, string value)
    {
        Span<byte> nameBuffer = stackalloc byte[64];
        Span<byte> valueBuffer = stackalloc byte[16];

        if (!TryWriteAscii(name, nameBuffer) || !TryWriteAscii(value, valueBuffer))
            return;

        fixed (byte* namePointer = nameBuffer)
        {
            if (engine->GetCvarPointer(NCharPtr.From(namePointer)) != null)
                return; // Already registered, e.g. on a re-init.

            fixed (byte* valuePointer = valueBuffer)
                engine->RegisterVariable(NCharPtr.From(namePointer), NCharPtr.From(valuePointer), 0);
        }
    }

    /// <summary>Reads a console variable, looking it up by name every call so no pointer is cached.</summary>
    private static unsafe float ReadCvar(string name, float fallback)
    {
        var engine = EngineApi.PClient;
        if (engine is null || engine->GetCvarPointer == null)
            return fallback;

        Span<byte> buffer = stackalloc byte[64];
        if (!TryWriteAscii(name, buffer))
            return fallback;

        fixed (byte* pointer = buffer)
        {
            var cvar = engine->GetCvarPointer(NCharPtr.From(pointer));
            return cvar != null ? cvar->value : fallback;
        }
    }

    /// <summary>Names the object a pick landed on, for the console.</summary>
    private static string Describe(PhysicsGunTarget value)
    {
        var name = value.Owner?.Name ?? value.Body?.Entity?.Name ?? "unknown";

        return value.Body?.Entity?.Get<BoneLink>() is { } link
            ? $"{name} bone {link.StudioBoneIndex}"
            : name;
    }

    /// <summary>Why a pick cannot be held.</summary>
    private static string Refusal(PhysicsGunVerdict verdict) => verdict switch
    {
        PhysicsGunVerdict.StaticWorld => "it is world geometry",
        PhysicsGunVerdict.KinematicBody => "it is a kinematic body",
        PhysicsGunVerdict.AnimatedSkeleton => "its skeleton is driven by an animation",
        PhysicsGunVerdict.UnownedBody => "no physics controller owns it",
        PhysicsGunVerdict.Pinned => "it is already pinned",
        _ => "nothing is there",
    };

    /// <summary>Writes a line to the client console. Format specifiers are neutralised.</summary>
    private static unsafe void Print(string message)
    {
        var engine = EngineApi.PClient;
        if (engine is null || engine->Con_Printf == null)
            return;

        // Con_Printf is a printf: a stray percent sign would be read as a format specifier.
        if (message.Contains('%'))
            message = message.Replace('%', '?');

        Span<byte> buffer = stackalloc byte[256];
        if (!TryWriteAscii(message, buffer))
            return;

        fixed (byte* pointer = buffer)
            engine->Con_Printf(NCharPtr.From(pointer));
    }

    private static bool TryWriteAscii(string value, Span<byte> buffer)
    {
        buffer.Clear();

        var written = Encoding.ASCII.GetBytes(value, buffer);
        if (written >= buffer.Length)
            return false;

        buffer[written] = 0;
        return true;
    }
}
