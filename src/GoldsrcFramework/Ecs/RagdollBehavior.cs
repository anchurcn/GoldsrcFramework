using System.Runtime.InteropServices;
using GoldsrcFramework.LinearMath;
using Stride.Engine;
using Stride.Games;
using StrideVector3 = Stride.Core.Mathematics.Vector3;
using StrideQuaternion = Stride.Core.Mathematics.Quaternion;
using GoldsrcFramework.Ecs.Components;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Script attached to a ragdoll entity: keeps the entity (and the client entity the engine draws it
/// through) where the physics skeleton is, and ends the corpse's life when its time is up.
/// </summary>
/// <remarks>
/// Death/respawn logic stays on the original entity's <see cref="HalfLifeBehavior"/>.
/// </remarks>
public sealed class RagdollBehavior : ScriptComponentBase, ILateUpdate
{
    private PhysicsController? physics;
    private ClEntityComponent? clEntity;

    /// <summary>
    /// Bone matrices of the pose last read from the simulation. Reused as the base pose
    /// <see cref="PhysicsController.SetupBones"/> reconstructs the bones without a rigid body on top
    /// of, and reused between frames because it carries no state of its own.
    /// </summary>
    private Matrix3x4[]? physicsPose;

    /// <summary>Client time the corpse was created at, or 0 when it is not yet known.</summary>
    private float bornAt;

    public override void Start()
    {
        physics = Entity.Get<PhysicsController>();
        clEntity = Entity.Get<ClEntityComponent>();
        bornAt = RagdollHelper.ClientTime();
    }

    /// <summary>
    /// Ends the corpse's life once it has been around long enough.
    /// </summary>
    /// <remarks>
    /// Deliberately not in <see cref="LateUpdate"/>: releasing removes the entity, which unregisters
    /// this script from the systems immediately, and the late-update system walks its set without
    /// deferring that. This system defers removals, so it is the safe place for it.
    /// </remarks>
    public override void Update(GameTime gameTime)
    {
        if (bornAt > 0f && RagdollHelper.ClientTime() - bornAt >= RagdollHelper.CorpseStaySeconds())
            RagdollHelper.Release(Entity);
    }

    /// <summary>
    /// Writes the ragdoll's simulated pose where the studio renderer expects to find the one it draws:
    /// <paramref name="modelRoot"/> becomes the model root (the renderer's rotation matrix) and
    /// <paramref name="bones"/> the full bone array, in studio bone order.
    /// </summary>
    /// <param name="modelRoot">The renderer's rotation matrix.</param>
    /// <param name="bones">The renderer's bone buffer.</param>
    /// <param name="boneCapacity">How many matrices <paramref name="bones"/> can hold.</param>
    /// <returns>
    /// False when this ragdoll cannot supply a pose, which leaves the caller to draw the entity the
    /// ordinary way. That covers a skeleton that is not loaded or no longer enabled, and a model whose
    /// bone count does not match the skeleton's - drawing a mismatched pose would be worse than
    /// drawing an animated one.
    /// </returns>
    internal unsafe bool TryFillPhysicsPose(Matrix3x4* modelRoot, Matrix3x4* bones, int boneCapacity)
    {
        if (physics is null || !physics.IsEnabled || physics.IsNullSkeleton)
            return false;

        var boneCount = physics.StudioBoneParents.Length;
        if (boneCount <= 0 || boneCount > boneCapacity)
            return false;

        // Identity rather than zero: this is the pose bones without a rigid body are rebuilt on top
        // of when no snapshot was applied, and a zeroed matrix would collapse them.
        if (physicsPose is null)
        {
            physicsPose = new Matrix3x4[StudioConstants.MAXSTUDIOBONES];
            Array.Fill(physicsPose, Matrix3x4.Identity);
        }

        physics.SetupBones(physicsPose);
        *modelRoot = physics.GetModelRootTransform().ToMatrix3x4();

        for (var i = 0; i < boneCount; i++)
            bones[i] = physicsPose[i];

        return true;
    }

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
}
