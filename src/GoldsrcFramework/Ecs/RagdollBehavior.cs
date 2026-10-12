using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using Stride.Engine;
using Stride.Games;
using System.Runtime.InteropServices;
using StrideVector3 = Stride.Core.Mathematics.Vector3;
using StrideQuaternion = Stride.Core.Mathematics.Quaternion;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Script attached to a ragdoll entity: keeps the entity (and the client entity the engine draws it
/// through) where the physics skeleton is, decides when the corpse has lived long enough, and unlinks
/// it when it leaves the scene.
/// <para>
/// It is also the corpse's <see cref="IPhysicsPoseSource"/>: the ragdoll is drawn from the pose its
/// own skeleton holds, with the bones that have no rigid body kept where the pose frozen at death put
/// them.
/// </para>
/// </summary>
/// <remarks>
/// Death/respawn logic stays on the original entity's <see cref="HalfLifeBehavior"/>.
/// </remarks>
public sealed class RagdollBehavior : ScriptComponentBase, ILateUpdate, IPhysicsPoseSource, IEnterExitCallable
{
    private PhysicsController? physics;

    /// <summary>
    /// The pose the character died in, handed over at creation, or null when none was - which leaves
    /// the bones without a rigid body wherever the caller's buffer had them.
    /// </summary>
    private Matrix3x4[]? deathPose;

    /// <summary>Client time the corpse was created at, or 0 when it is not yet known.</summary>
    private float bornAt;

    /// <summary>
    /// Whether the corpse has asked to be retired, with the teardown still to come.
    /// </summary>
    /// <remarks>
    /// Set by the two things that can end a corpse's life - <see cref="Update"/> when the stay time has
    /// run out, and <see cref="RagdollHelper.RequestRelease"/> when the entity that died comes back -
    /// and acted on by <see cref="CompleteRelease"/>, which the engine's temp entity walk reaches
    /// through the same helper.
    /// </remarks>
    private bool releaseRequested;

    /// <summary>
    /// Strong handle to the corpse's own entity - the object the engine's <c>iuser4</c> slot points at,
    /// so the renderer can get from a client entity back to the skeleton that poses it. The corpse's
    /// own record of a resource it took, and managed state only: it is given back without the client
    /// entity ever being read.
    /// </summary>
    private GCHandle owner;

    public override void Start()
    {
        physics = Entity.Get<PhysicsController>();
        bornAt = RagdollHelper.ClientTime();
    }

    /// <summary>
    /// Hands the corpse the pose its bones without a rigid body are to keep - the pose the character
    /// died in.
    /// </summary>
    /// <param name="pose">
    /// Studio bone world-space matrices, the same form the read-back hands the controller as its
    /// <c>basePose</c>: the fold into anchor-relative form is the controller's job, so nothing has to
    /// be converted here.
    /// </param>
    /// <remarks>
    /// Called once, at creation, before the corpse is ever drawn. The array is copied: the caller's
    /// buffer is the live animation pose of the entity that died, and keeps changing.
    /// </remarks>
    internal void SetDeathPose(ReadOnlySpan<Matrix3x4> pose) => deathPose = pose.IsEmpty ? null : pose.ToArray();

    /// <summary>
    /// Ends the corpse's life once it has been around long enough - as a request, which the engine's
    /// temp entity walk carries out on its next pass.
    /// </summary>
    /// <remarks>
    /// The lifetime is decided here because the runtime's own clock and console variable are what define
    /// it; the corpse is retired from the walk because the walk runs early enough in the frame to keep
    /// it out of the frame it is retired on. Deliberately not <see cref="LateUpdate"/> either: what this
    /// can lead to is the entity leaving the scene, and the late-update system walks its set without
    /// deferring that.
    /// </remarks>
    public override void Update(GameTime gameTime)
    {
        if (bornAt > 0f && RagdollHelper.ClientTime() - bornAt >= RagdollHelper.CorpseStaySeconds())
            RequestRelease();
    }

    /// <summary>
    /// Asks for the corpse to be retired, which the engine's temp entity walk carries out on its next
    /// pass - see <see cref="CompleteRelease"/>.
    /// </summary>
    /// <remarks>
    /// Idempotent, and the only place a corpse is asked to go: deciding here and tearing down there
    /// keeps the lifetime in one place, so nothing else has to know the clock, the console variable or
    /// the frame order the two depend on. A corpse the walk cannot reach - one that has no client entity
    /// to be reached through - ends its own life on the spot instead of waiting for a pass that will
    /// never come.
    /// </remarks>
    internal unsafe void RequestRelease()
    {
        releaseRequested = true;

        // Nothing comes round to act on the request when the corpse has no client entity of its own -
        // the walk is only reached through one - so such a corpse ends its own life instead of waiting
        // for a pass that will never come.
        if (Entity.Get<Components.ClEntityComponent>()?.NativeEntity == null)
            CompleteRelease();
    }

    /// <summary>Whether the corpse has asked to be retired and is waiting for the walk to act on it.</summary>
    internal bool ReleaseRequested => releaseRequested;

    /// <summary>
    /// Ends the corpse's life: it leaves the scene, and leaving the scene is what unlinks it from the
    /// client entity the engine draws it through - see <see cref="IEnterExitCallable.OnExit"/>.
    /// </summary>
    /// <remarks>
    /// Normally called by the engine's temp entity walk, and that is what makes the teardown safe from
    /// there: the walk runs before the frame's systems are iterated, so removing the entity cannot pull
    /// it out from under one of them. The path that does not come through the walk reaches it from a
    /// script update instead, where the script system defers removals until it is done with its own set.
    /// Idempotent, since an entity that has already left has no scene to leave.
    /// </remarks>
    internal void CompleteRelease() => Entity.Scene?.Entities.Remove(Entity);

    /// <summary>
    /// Keeps the client entity the engine draws this corpse through where the simulation put it.
    /// </summary>
    /// <remarks>
    /// <see cref="LateUpdate"/> writes the Stride transform, and that reaches the client entity
    /// through the transform sync pass at the end of the frame - after the engine has already culled
    /// the temp entity against the position it held then, which is a frame old. Writing the origin
    /// here instead puts the position in place before that cull, so a corpse that is moving fast is
    /// culled where it is rather than where it was. Angles are left alone: the corpse is drawn from
    /// the pose the skeleton carries, not from the orientation of its client entity.
    /// </remarks>
    internal unsafe void DriveClientEntity(cl_entity_t* entity)
    {
        if (entity == null || physics is null || !physics.IsEnabled || physics.IsNullSkeleton)
            return;

        var position = physics.GetModelRootTransform().TranslationVector;
        entity->origin = new Vector3(position.X, position.Y, position.Z);
    }

    /// <summary>
    /// Whether this is a physics pose source at all: for a corpse that is the same question as whether
    /// its skeleton can produce one, since the corpse has no animation to fall back to.
    /// </summary>
    PhysicsController? IPhysicsPoseSource.Physics
        => physics is { IsEnabled: true, IsNullSkeleton: false } ? physics : null;

    /// <inheritdoc/>
    ReadOnlySpan<Matrix3x4> IPhysicsPoseSource.BasePose => deathPose;

    /// <inheritdoc/>
    Matrix3x4 IPhysicsPoseSource.ModelRoot => physics!.GetModelRootTransform().ToMatrix3x4();

    public void LateUpdate()
    {
        if (physics is null || !physics.IsEnabled || physics.IsNullSkeleton)
            return;

        // Derived from the pivot bone's physics pose, so it is up to date right after the step.
        var rootWorld = physics.GetModelRootTransform();

        // Write translation/rotation rather than the WorldMatrix field: assigning that field does not
        // update LocalMatrix, and the next TransformProcessor pass would rebuild it from stale data.
        // The ragdoll entity is a scene root (no parent), so its local transform is the world transform.
        rootWorld.Decompose(out StrideVector3 _, out StrideQuaternion rotation, out StrideVector3 position);
        Entity.Transform.Position = position;
        Entity.Transform.Rotation = rotation;
        Entity.Transform.UpdateWorldMatrix();
    }

    /// <summary>
    /// Takes hold of the entity this corpse is drawn through, and returns the raw handle the engine's
    /// <c>iuser4</c> slot is to carry for it.
    /// </summary>
    /// <remarks>
    /// Called once, while the corpse is being built and against a client entity the engine has just
    /// handed out - the only moment that slot is known to be the corpse's. The handle is kept here
    /// rather than on the temp entity it is pointed at with, because unlinking happens from an entity's
    /// exit, where the engine may already have given the slot to somebody else.
    /// </remarks>
    internal nint AttachOwner(Entity ragdoll)
    {
        owner = GCHandle.Alloc(ragdoll, GCHandleType.Normal);
        return GCHandle.ToIntPtr(owner);
    }

    /// <summary>Gives the handle back, if there is one to give.</summary>
    /// <remarks>Idempotent: the handle is cleared as it is freed, so it cannot be freed twice.</remarks>
    private void ReleaseOwner()
    {
        if (!owner.IsAllocated)
            return;

        owner.Free();
        owner = default;
    }

    /// <inheritdoc/>
    void IEnterExitCallable.OnEnter()
    {
    }

    /// <summary>
    /// Gives back the handle that holds this corpse, so the entity can be collected once it has left
    /// the scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Leaving the scene is the one point every way a corpse can end passes through: the walk retiring
    /// it, and <see cref="GoldsrcClientGame.Reset"/> clearing the root scene on a map change.
    /// </para>
    /// <para>
    /// Only managed state is touched. The client entity the corpse was drawn through is deliberately
    /// left alone: an exit is the same event a map change runs through, and the temp entity behind
    /// that pointer belongs to the engine and may already have been recycled by then, so reading or
    /// writing it is a way to crash the client rather than to unlink anything. What becomes of the
    /// engine's own temp entities is the engine's to manage. A corpse that never had a client entity
    /// owns no handle, and leaving is then just leaving.
    /// </para>
    /// </remarks>
    void IEnterExitCallable.OnExit() => ReleaseOwner();
}
