using GoldsrcFramework.LinearMath;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.Engine;
using System.Diagnostics.CodeAnalysis;
using StrideMatrix = Stride.Core.Mathematics.Matrix;
using StrideQuaternion = Stride.Core.Mathematics.Quaternion;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Manages the physics skeleton of a GoldSrc entity: a set of child entities carrying physics bodies
/// and constraints (produced by instantiating a prefab, or by cloning for ragdolls).
/// Lives on the root entity, not in a prefab.
/// <para>
/// A studio model has many <b>studio bones</b>, but only a subset of them - the <b>simulated
/// bones</b> - carry a rigid body. The rest (<b>non-simulated bones</b> such as fingers) are placed
/// relative to their nearest simulated ancestor, and are reconstructed on every read from the
/// base pose the caller hands <see cref="SetupBones"/>, with the anchor-relative part of it folded
/// in on the spot. The controller keeps no pose of its own: both directions are pure transfers
/// between the caller's buffers and the simulation.
/// </para>
/// <para>
/// Entity level concerns such as model identity tracking and ragdoll state belong to
/// <see cref="HalfLifeBehavior"/>.
/// </para>
/// </summary>
public sealed class PhysicsController : EntityComponent
{
    /// <summary>
    /// Per-bone internal state, rebuilt by <see cref="LoadSkeleton"/>.
    /// </summary>
    private sealed class PhysicsBoneData
    {
        public Entity Bone = null!;
        public CollidableComponent Collidable = null!;
        public BodyComponent? Body;
        public int StudioBoneIndex;
        public bool IsAddon;
    }

    private readonly List<PhysicsBoneData> physicsBonesInternal = [];

    /// <summary>Parallel view of <see cref="physicsBonesInternal"/> backing the public API.</summary>
    private readonly List<Entity> boneEntities = [];

    private readonly List<ConstraintComponentBase> regularConstraints = [];
    private readonly List<ConstraintComponentBase> addonConstraints = [];

    private PhysicsBoneData? PivotPhysicsBone { get; set; }
    private StrideMatrix modelRootOffset = StrideMatrix.Identity;
    private PhysicsMotionType motionType = PhysicsMotionType.Kinematic;

    /// <summary>Parent studio bone index per studio bone; -1 for a root bone.</summary>
    private int[] studioBoneParents = [];

    /// <summary>Number of studio bones covered by <see cref="studioBoneParents"/> and <see cref="anchorBoneIndices"/>.</summary>
    private int studioBoneCount;

    /// <summary>
    /// Per studio bone, the nearest ancestor (itself included) that is a simulated bone; falls back to
    /// the pivot bone when the bone has no simulated ancestor at all. The anchor is therefore always a
    /// simulated bone, which means its world transform is always available from the simulation.
    /// </summary>
    private int[] anchorBoneIndices = [];

    /// <summary>
    /// Motion type applied to the skeleton bones. Only <see cref="PhysicsMotionType.Kinematic"/> and
    /// <see cref="PhysicsMotionType.Dynamic"/> are supported: static skeletons are expressed by the
    /// prefab itself (worldspawn carries a <c>StaticComponent</c> and has no <see cref="PhysicsController"/>).
    /// Setting this takes effect immediately when the skeleton is enabled.
    /// </summary>
    public PhysicsMotionType MotionType
    {
        get => motionType;
        set
        {
            if (motionType == value)
                return;

            motionType = value;

            if (IsEnabled)
                ApplyMotionType();
        }
    }

    /// <summary>
    /// True while the physics bone entities are attached to the root entity (and therefore in the scene
    /// and registered in the physics simulation).
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// True when no physics skeleton is loaded (e.g. the model has no .gpd). Every operation is a no-op.
    /// </summary>
    [MemberNotNullWhen(false, nameof(PivotPhysicsBone))]
    
    public bool IsNullSkeleton => physicsBonesInternal.Count == 0;

    /// <summary>The physics bone entities, in the order they were accepted by <see cref="LoadSkeleton"/>.</summary>
    public IReadOnlyList<Entity> PhysicsBones => boneEntities;

    /// <summary>
    /// Parent studio bone index per studio bone, as supplied to <see cref="LoadSkeleton"/>.
    /// Exposed so a ragdoll can reuse the hierarchy of the skeleton it was cloned from.
    /// </summary>
    public ReadOnlyMemory<int> StudioBoneParents => studioBoneParents;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Takes ownership of a set of physics bone entities. Any previously loaded skeleton is detached
    /// first. An empty collection produces a NullPhysicsSkeleton.
    /// </summary>
    /// <param name="physicsBoneEntities">
    /// One entity per simulated studio bone. Entities without a <see cref="BoneLink"/> or without a
    /// <see cref="CollidableComponent"/> are ignored.
    /// </param>
    /// <param name="boneParents">
    /// Parent studio bone index per studio bone, -1 for a root bone. Brush models have a single
    /// implicit root bone, so <c>[-1]</c>. An empty span degrades gracefully: every studio bone is
    /// then anchored to the pivot bone.
    /// </param>
    /// <remarks>
    /// The newly loaded skeleton is left <b>detached</b> (<see cref="IsEnabled"/> is false); call
    /// <see cref="Enable"/> afterwards to attach it to the root entity and register its bodies.
    /// </remarks>
    public void LoadSkeleton(IReadOnlyList<Entity> physicsBoneEntities, ReadOnlySpan<int> boneParents)
    {
        DetachBones();

        physicsBonesInternal.Clear();
        boneEntities.Clear();
        regularConstraints.Clear();
        addonConstraints.Clear();
        PivotPhysicsBone = null;
        modelRootOffset = StrideMatrix.Identity;
        IsEnabled = false;

        foreach (var entity in physicsBoneEntities)
        {
            if (entity.Get<BoneLink>() is not { } link)
                continue;
            if (entity.Get<CollidableComponent>() is not { } collidable)
                continue;

            physicsBonesInternal.Add(new PhysicsBoneData
            {
                Bone = entity,
                Collidable = collidable,
                Body = collidable as BodyComponent,
                StudioBoneIndex = link.StudioBoneIndex,
                IsAddon = link.IsAddon,
            });
            boneEntities.Add(entity);
        }

        if (physicsBonesInternal.Count == 0)
            return; // NullPhysicsSkeleton

        CollectConstraints();
        ApplyCollisionGroups();

        // Pivot: the simulated bone with the smallest studio bone index (index 0 preferred).
        // Studio bone 0 is not necessarily simulated and is not the model origin either, so the pivot
        // is what GetModelRootTransform() derives the model root from.
        var pivot = physicsBonesInternal.MinBy(bone => bone.StudioBoneIndex)!;

        PivotPhysicsBone = pivot;

        // ModelRootOffset is expressed in the root entity's local space, i.e. it is the inverse of the
        // pivot bone's T-pose transform relative to the model root:
        //     modelRoot = modelRootOffset * pivotWorld,  modelRootOffset = Invert(pivotLocal)
        // The inverse goes on the left because Stride matrices are row-vector
        // (World = Local * ParentWorld), the opposite of GoldSrc's column-vector Matrix3x4.
        // Deriving it from the bone's *local* matrix (instead of world matrices) keeps it valid no
        // matter where the root entity happens to be when the skeleton is loaded, and it does not
        // depend on the bones being attached yet - LoadSkeleton deliberately leaves them detached.
        // UpdateLocalMatrix() is required because the TRS fields do not push into LocalMatrix.
        pivot.Bone.Transform.UpdateLocalMatrix();
        var pivotLocal = pivot.Bone.Transform.LocalMatrix;
        StrideMatrix.Invert(ref pivotLocal, out modelRootOffset);

        BuildBoneAnchors(boneParents, pivot.StudioBoneIndex);
    }

    /// <summary>
    /// Attaches the physics bone entities to the root entity. This single operation also brings them
    /// into the scene and registers their bodies and constraints in the physics simulation.
    /// Applies the current <see cref="MotionType"/>.
    /// </summary>
    public void Enable()
    {
        if (IsNullSkeleton || IsEnabled)
            return;

        foreach (var bone in boneEntities)
            bone.Transform.Parent = Entity.Transform;

        IsEnabled = true;
        ApplyMotionType();
    }

    /// <summary>
    /// Detaches the physics bone entities from the root entity, which also removes them from the scene
    /// and unregisters their bodies and constraints from the physics simulation. Bone references are
    /// kept, so <see cref="Enable"/> can restore the skeleton without reloading the prefab.
    /// </summary>
    public void Disable()
    {
        if (!IsEnabled)
            return;

        DetachBones();
        IsEnabled = false;
    }

    // ── Pose ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Drives the kinematic simulated bones from a studio model pose. This is purely an input to the
    /// physics skeleton and caches nothing, so it stays cheap enough to call every frame.
    /// </summary>
    /// <param name="pose">
    /// Studio bone world-space matrices (Matrix3x4) indexed by studio bone index. This is the raw bone
    /// array of the model; entry 0 is the first studio bone, <b>not</b> the model origin.
    /// </param>
    /// <remarks>
    /// Addon (jiggle) bones are never written: they stay dynamic so the simulation can drag them
    /// along with their kinematic neighbours.
    /// </remarks>
    public void SetPose(ReadOnlySpan<Matrix3x4> pose)
    {
        if (!IsEnabled || IsNullSkeleton)
            return;

        foreach (var bone in physicsBonesInternal)
        {
            if (bone.IsAddon || bone.Body is null)
                continue;

            var boneIndex = bone.StudioBoneIndex;
            if ((uint)boneIndex >= (uint)pose.Length)
                continue;

            SetPhysicsBonePose(bone, pose[boneIndex].ToStrideMatrix());
        }
    }

    //public void SetPivotBonePoseByRootTransform(Stride.Core.Mathematics.Matrix rootTransformMatrix)
    //{
    //    if (!IsEnabled || IsNullSkeleton || PivotPhysicsBone is null)
    //        return;

    //    // LoadSkeleton established:  modelRoot = modelRootOffset * pivotWorld
    //    //                           modelRootOffset = Invert(pivotLocal)
    //    // Given the model root, reconstruct the pivot bone's world transform and drive it:
    //    //     pivotWorld = Invert(modelRootOffset) * modelRoot
    //    // The inverse goes on the left, the same way round as the derivation above: Stride matrices
    //    // are row-vector (World = Local * ParentWorld).
    //    StrideMatrix.Invert(ref modelRootOffset, out var inverseOffset);
    //    StrideMatrix.Multiply(ref inverseOffset, ref rootTransformMatrix, out var pivotWorld);

    //    SetPhysicsBonePose(PivotPhysicsBone, pivotWorld);
    //}

    /// <summary>
    /// Rebuilds the full studio skeleton pose: simulated bones are read from the physics simulation,
    /// non-simulated bones are reconstructed on top of their anchor.
    /// </summary>
    /// <param name="pose">
    /// In/out array of studio bone world-space matrices indexed by studio bone index. Simulated bones
    /// are written from the simulation; a bone without a rigid body is placed relative to its anchor
    /// as <paramref name="basePose"/> has it, and is otherwise left as the caller filled it. The
    /// caller therefore does not have to pre-fill it.
    /// </param>
    /// <param name="basePose">
    /// The pose the caller considers current, indexed the same way - for a kinematic entity this
    /// frame's animation pose, for a ragdoll the pose frozen at death. It is read to find where a bone
    /// without a rigid body sits relative to its anchor, a question <paramref name="pose"/> itself can
    /// no longer answer once the simulation has overwritten that anchor. Empty means "leave those
    /// bones to the caller", which is what a reader with nothing of its own to say passes.
    /// <para>
    /// Two readers pass two different poses here, and the parameter is deliberately the pose rather
    /// than its description. A ragdoll passes the pose frozen at death, so a fist stays clenched and
    /// does not relax into the bind pose. A skeleton that keeps being animated passes the pose of the
    /// current frame instead, so a bone hanging off a jiggle (addon) body follows the body the
    /// simulation has moved rather than the animation it has left behind.
    /// </para>
    /// </param>
    /// <remarks>
    /// Read-back counterpart of <see cref="SetPose"/>, mirroring gsphysics
    /// <c>SkeletalPhysicsComponent::SetupBones</c> and GoldSrc <c>SV_StudioSetupBones</c>.
    /// </remarks>
    public void SetupBones(Span<Matrix3x4> pose, ReadOnlySpan<Matrix3x4> basePose = default)
    {
        if (!IsEnabled || IsNullSkeleton || pose.Length == 0)
            return;

        // 1. Simulated bones are authoritative in the simulation.
        foreach (var bone in physicsBonesInternal)
        {
            if (bone.Body is not { } body)
                continue;

            var boneIndex = bone.StudioBoneIndex;
            if ((uint)boneIndex >= (uint)pose.Length)
                continue;

            // A body pose is centred on the collider's mass; report the bone's own origin instead.
            pose[boneIndex] = ComposeWorldMatrix(ToBoneOrigin(bone, body), body.Orientation).ToMatrix3x4();
        }

        // 2. Non-simulated bones follow their anchor, which is always a simulated bone and has just
        //    been refreshed above.
        if (basePose.IsEmpty)
            return;

        for (var boneIndex = 0; boneIndex < studioBoneCount && boneIndex < pose.Length; boneIndex++)
        {
            if (boneIndex >= basePose.Length)
                break;

            var anchorIndex = anchorBoneIndices[boneIndex];
            if (anchorIndex == boneIndex)
                continue; // simulated, already written from physics
            if ((uint)anchorIndex >= (uint)pose.Length || (uint)anchorIndex >= (uint)basePose.Length)
                continue;

            // Where the bone sits relative to its anchor - bone * inverse(anchor) - replayed against
            // the anchor's *new* world matrix. Stride matrices are row-vector (World = Local *
            // ParentWorld), so the relative transform goes on the left. Both steps are one expression
            // on purpose: the relative transform is an intermediate nothing else reads, so
            // materialising it would buy nothing but a buffer.
            var anchorBase = basePose[anchorIndex].ToStrideMatrix();
            StrideMatrix.Invert(ref anchorBase, out var inverseAnchorBase);
            var boneBase = basePose[boneIndex].ToStrideMatrix();
            StrideMatrix.Multiply(ref boneBase, ref inverseAnchorBase, out var relative);
            var anchorWorld = pose[anchorIndex].ToStrideMatrix();
            StrideMatrix.Multiply(ref relative, ref anchorWorld, out var world);
            pose[boneIndex] = world.ToMatrix3x4();
        }
    }

    /// <summary>
    /// Computes the model root transform from the pivot physics bone and the T-pose offset captured by
    /// <see cref="LoadSkeleton"/>. Used by dynamic (ragdoll) entities to drive the root entity transform.
    /// </summary>
    /// <remarks>
    /// Always returns a usable transform: when no physics pose is available (null skeleton, disabled),
    /// it falls back to the root entity's current world transform, which makes writing the result back
    /// a no-op.
    /// </remarks>
    public StrideMatrix GetModelRootTransform()
    {
        if (IsNullSkeleton)
        {
            Entity.Transform.UpdateWorldMatrix();
            return Entity.Transform.WorldMatrix;
        }

        var pivotWorld = GetBoneWorldMatrix(PivotPhysicsBone);
        StrideMatrix.Multiply(ref modelRootOffset, ref pivotWorld, out var rootWorld);
        return rootWorld;
    }

    // ── Internals ─────────────────────────────────────────────────────────

    private void DetachBones()
    {
        foreach (var bone in boneEntities)
            bone.Transform.Parent = null;
    }

    /// <summary>
    /// Builds <see cref="anchorBoneIndices"/>: for every studio bone, the nearest ancestor (itself
    /// included) that is a simulated bone, or <paramref name="fallbackBoneIndex"/> when there is none.
    /// </summary>
    private void BuildBoneAnchors(ReadOnlySpan<int> boneParents, int fallbackBoneIndex)
    {
        var boneCount = boneParents.Length;
        foreach (var bone in physicsBonesInternal)
            boneCount = Math.Max(boneCount, bone.StudioBoneIndex + 1);

        studioBoneCount = boneCount;

        var parents = new int[boneCount];
        Array.Fill(parents, -1);
        boneParents[..Math.Min(boneParents.Length, boneCount)].CopyTo(parents);
        studioBoneParents = parents;

        var simulated = new bool[boneCount];
        foreach (var bone in physicsBonesInternal)
        {
            if ((uint)bone.StudioBoneIndex < (uint)boneCount)
                simulated[bone.StudioBoneIndex] = true;
        }

        anchorBoneIndices = new int[boneCount];
        for (var boneIndex = 0; boneIndex < boneCount; boneIndex++)
            anchorBoneIndices[boneIndex] = FindAnchor(boneIndex, simulated, parents, fallbackBoneIndex);
    }

    private static int FindAnchor(int boneIndex, ReadOnlySpan<bool> simulated, ReadOnlySpan<int> parents, int fallbackBoneIndex)
    {
        var current = boneIndex;

        // Bounded by the bone count so a malformed or cyclic hierarchy cannot hang the load.
        for (var guard = 0; guard <= parents.Length; guard++)
        {
            if ((uint)current >= (uint)simulated.Length)
                return fallbackBoneIndex;
            if (simulated[current])
                return current;

            current = parents[current];
        }

        return fallbackBoneIndex;
    }

    /// <summary>
    /// Splits the skeleton constraints into regular ones and ones touching an addon (jiggle) bone.
    /// Constraints themselves are registered/unregistered by the <c>ConstraintProcessor</c> as the bone
    /// entities enter and leave the entity manager; only their <c>Enabled</c> flag is managed here.
    /// </summary>
    private void CollectConstraints()
    {
        var addonBones = new HashSet<Entity>();
        foreach (var bone in physicsBonesInternal)
        {
            if (bone.IsAddon)
                addonBones.Add(bone.Bone);
        }

        foreach (var bone in boneEntities)
        {
            foreach (var component in bone.Components)
            {
                if (component is not ConstraintComponentBase constraint)
                    continue;

                var connectsAddon = false;
                foreach (var body in constraint.Bodies)
                {
                    if (body is { Entity: { } bodyEntity } && addonBones.Contains(bodyEntity))
                    {
                        connectsAddon = true;
                        break;
                    }
                }

                if (connectsAddon)
                    addonConstraints.Add(constraint);
                else
                    regularConstraints.Add(constraint);
            }
        }
    }

    /// <summary>
    /// Writes the collision group of every collidable of this skeleton: the subgroup indices that
    /// stop only the pairs a joint links, and an id of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stride's narrow phase drops a contact between two collidables that share a non-zero
    /// <c>CollisionGroup.Id</c> and sit within one subgroup index of each other on every axis, which
    /// makes the three indices a cell of a grid and that test a Chebyshev distance - see
    /// <see cref="SkeletonCollisionGroups"/> for what a skeleton needs and why the id is per
    /// instance. Both halves are worked out here, from the joints the skeleton actually carries,
    /// because that graph is what the filter has to reproduce: a ragdoll's clones have their joints
    /// re-pointed at the clones, so the joints a prefab was built with are only the same list by
    /// construction.
    /// </para>
    /// <para>
    /// A skeleton of a single collidable keeps the id that filters nothing: it holds no pair to stop,
    /// and two of them - two doors, two props - have to go on colliding with each other.
    /// </para>
    /// </remarks>
    private void ApplyCollisionGroups()
    {
        var slots = new Dictionary<BodyComponent, int>();
        for (var bone = 0; bone < physicsBonesInternal.Count; bone++)
        {
            if (physicsBonesInternal[bone].Body is { } body)
                slots[body] = bone;
        }

        var joints = new (int BodyA, int BodyB)[regularConstraints.Count + addonConstraints.Count];
        var jointCount = 0;

        foreach (var constraint in AllConstraints())
        {
            if (constraint is ITwoBody { A: { } bodyA, B: { } bodyB }
                && slots.TryGetValue(bodyA, out var slotA)
                && slots.TryGetValue(bodyB, out var slotB))
            {
                joints[jointCount++] = (slotA, slotB);
            }
        }

        var groups = SkeletonCollisionGroups.Layout(physicsBonesInternal.Count, joints.AsSpan(0, jointCount));
        var id = physicsBonesInternal.Count > 1 ? SkeletonCollisionGroups.NextId() : (ushort)0;

        for (var bone = 0; bone < physicsBonesInternal.Count; bone++)
        {
            groups[bone].Id = id;
            physicsBonesInternal[bone].Collidable.CollisionGroup = groups[bone];
        }
    }

    /// <summary>Every joint of the skeleton, whether or not it touches an addon (jiggle) bone.</summary>
    private IEnumerable<ConstraintComponentBase> AllConstraints()
    {
        foreach (var constraint in regularConstraints)
            yield return constraint;

        foreach (var constraint in addonConstraints)
            yield return constraint;
    }

    /// <summary>
    /// Applies <see cref="MotionType"/> to the skeleton.
    /// </summary>
    /// <remarks>
    /// In kinematic mode non-addon bones are kinematic and the constraints between them are disabled
    /// (two kinematic bodies never produce motion). Addon bones stay dynamic in both modes and keep
    /// their constraints enabled so the neighbouring kinematic bones can drag them around.
    /// </remarks>
    private void ApplyMotionType()
    {
        if (IsNullSkeleton)
            return;

        var kinematic = motionType == PhysicsMotionType.Kinematic;

        foreach (var bone in physicsBonesInternal)
        {
            if (bone.Body is { } body)
                body.Kinematic = !bone.IsAddon && kinematic;
        }

        foreach (var constraint in regularConstraints)
            constraint.Enabled = !kinematic;
    }

    /// <summary>
    /// Sets a single physics bone's pose from a Stride world-space matrix.
    /// </summary>
    private static void SetPhysicsBonePose(PhysicsBoneData bone, StrideMatrix pose)
    {
        pose.Decompose(out StrideVector3 _, out StrideQuaternion rotation, out StrideVector3 translation);

        // The caller hands us the bone's own origin, while a body pose is expressed at the
        // collider's center of mass; the two differ by the rotated offset.
        bone.Body?.Teleport(translation + BoneCenterOfMassOffset(bone, rotation), rotation);
    }

    private static StrideMatrix GetBoneWorldMatrix(PhysicsBoneData bone)
    {
        if (bone.Body is { } body)
            return ComposeWorldMatrix(ToBoneOrigin(bone, body), body.Orientation);

        // Static bones (worldspawn) have no physics pose to read; fall back to the entity transform.
        bone.Bone.Transform.UpdateWorldMatrix();
        return bone.Bone.Transform.WorldMatrix;
    }

    /// <summary>
    /// Position of a bone's own origin, given the pose of its body.
    /// </summary>
    /// <remarks>
    /// A Stride body pose is expressed at the collider's center of mass rather than at the entity
    /// transform - <c>CollidableComponent</c> converts between the two when it attaches and when it
    /// writes back - so the pose <see cref="SetPose"/> sends to the simulation is centred on that
    /// mass. Subtracting the offset again is what makes the round trip return the bone origin the
    /// caller works in, and what puts each shape where the bind pose puts it.
    /// </remarks>
    private static StrideVector3 ToBoneOrigin(PhysicsBoneData bone, BodyComponent body)
        => body.Position - BoneCenterOfMassOffset(bone, body.Orientation);

    /// <summary>
    /// The collider's center of mass, rotated from the bone's frame into world space.
    /// </summary>
    /// <remarks>
    /// Read on every call instead of being cached: a collider only learns its center of mass once it
    /// is attached to a simulation, and detaching resets it to zero. It stays zero for every collider
    /// that is not a compound - a brush model's mesh in particular - so those bones convert to and
    /// from nothing.
    /// </remarks>
    private static StrideVector3 BoneCenterOfMassOffset(PhysicsBoneData bone, StrideQuaternion rotation)
        => StrideVector3.Transform(bone.Collidable.CenterOfMass, rotation);

    private static StrideMatrix ComposeWorldMatrix(StrideVector3 translation, StrideQuaternion rotation)
    {
        var scale = StrideVector3.One;
        StrideMatrix.Transformation(ref scale, ref rotation, ref translation, out var world);
        return world;
    }
}
