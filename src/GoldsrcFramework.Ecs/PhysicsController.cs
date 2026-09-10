using GoldsrcFramework.LinearMath;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.Engine;
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
/// relative to their nearest simulated ancestor, and are only reconstructed when a
/// <see cref="PoseSnapshot"/> has been applied to the controller.
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

    private PhysicsBoneData? pivotPhysicsBone;
    private StrideMatrix modelRootOffset = StrideMatrix.Identity;
    private bool[]? settingMasks = null;
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
    /// Anchor-relative pose captured by <see cref="ApplyPoseSnapshot"/>. Null when no snapshot is
    /// applied, in which case non-simulated bones are left to the caller of <see cref="SetupBones"/>.
    /// Nothing else ever writes this field: <see cref="SetPose"/> has no side effects.
    /// </summary>
    private StrideMatrix[]? localPose;

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
        pivotPhysicsBone = null;
        modelRootOffset = StrideMatrix.Identity;
        localPose = null;
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

        // Pivot: the simulated bone with the smallest studio bone index (index 0 preferred).
        // Studio bone 0 is not necessarily simulated and is not the model origin either, so the pivot
        // is what GetModelRootTransform() derives the model root from.
        var pivot = physicsBonesInternal.MinBy(bone => bone.StudioBoneIndex)!;

        pivotPhysicsBone = pivot;

        // ModelRootOffset is expressed in the root entity's local space, i.e. it is the inverse of the
        // pivot bone's T-pose transform relative to the model root:
        //     modelRoot = pivotWorld * modelRootOffset,  modelRootOffset = Invert(pivotLocal)
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
    /// <param name="settingMask">
    /// Optional per-studio-bone mask; bones whose entry is false are skipped.
    /// When null, the controller level <c>settingMasks</c> is used instead.
    /// </param>
    /// <remarks>
    /// Addon (jiggle) bones are never written: they stay dynamic so the simulation can drag them
    /// along with their kinematic neighbours.
    /// </remarks>
    public void SetPose(ReadOnlySpan<Matrix3x4> pose, bool[]? settingMask = null)
    {
        if (!IsEnabled || IsNullSkeleton)
            return;

        var mask = settingMask ?? settingMasks;

        foreach (var bone in physicsBonesInternal)
        {
            if (bone.IsAddon || bone.Body is null)
                continue;

            var boneIndex = bone.StudioBoneIndex;
            if ((uint)boneIndex >= (uint)pose.Length)
                continue;
            if (mask is not null && boneIndex < mask.Length && !mask[boneIndex])
                continue;

            SetPhysicsBonePose(bone, pose[boneIndex].ToStrideMatrix());
        }
    }

    /// <summary>
    /// Rebuilds the full studio skeleton pose: simulated bones are read from the physics simulation,
    /// non-simulated bones are reconstructed on top of their anchor.
    /// </summary>
    /// <param name="pose">
    /// In/out array of studio bone world-space matrices indexed by studio bone index. The caller is
    /// expected to fill it with the current base pose (for a kinematic entity, the current animation
    /// pose). Simulated bones are overwritten with the physics pose; non-simulated bones are
    /// overwritten only when a snapshot has been applied (see <see cref="ApplyPoseSnapshot"/>),
    /// otherwise the caller's values are kept.
    /// </param>
    /// <param name="settingMask">
    /// Optional per-studio-bone mask; bones whose entry is false are skipped.
    /// When null, the controller level <c>settingMasks</c> is used instead.
    /// </param>
    /// <remarks>
    /// Read-back counterpart of <see cref="SetPose"/>, mirroring gsphysics
    /// <c>SkeletalPhysicsComponent::SetupBones</c> and GoldSrc <c>SV_StudioSetupBones</c>.
    /// </remarks>
    public void SetupBones(Span<Matrix3x4> pose, bool[]? settingMask = null)
    {
        if (!IsEnabled || IsNullSkeleton || pose.Length == 0)
            return;

        var mask = settingMask ?? settingMasks;

        // 1. Simulated bones are authoritative in the simulation.
        foreach (var bone in physicsBonesInternal)
        {
            if (bone.Body is not { } body)
                continue;

            var boneIndex = bone.StudioBoneIndex;
            if ((uint)boneIndex >= (uint)pose.Length)
                continue;
            if (mask is not null && boneIndex < mask.Length && !mask[boneIndex])
                continue;

            pose[boneIndex] = ComposeWorldMatrix(body.Position, body.Orientation).ToMatrix3x4();
        }

        // 2. Non-simulated bones follow their anchor, which is always a simulated bone and has just been
        //    refreshed above. Only possible when a pose snapshot was applied.
        if (localPose is null)
            return;

        for (var boneIndex = 0; boneIndex < studioBoneCount && boneIndex < pose.Length; boneIndex++)
        {
            var anchorIndex = anchorBoneIndices[boneIndex];
            if (anchorIndex == boneIndex)
                continue; // simulated, already written from physics
            if ((uint)anchorIndex >= (uint)pose.Length)
                continue;
            if (mask is not null && boneIndex < mask.Length && !mask[boneIndex])
                continue;

            var anchorWorld = pose[anchorIndex].ToStrideMatrix();
            var local = localPose[boneIndex];
            StrideMatrix.Multiply(ref anchorWorld, ref local, out var world);
            pose[boneIndex] = world.ToMatrix3x4();
        }
    }

    /// <summary>
    /// Captures the pose described by <paramref name="pose"/> into an anchor-relative snapshot that can
    /// be replayed on another skeleton instance.
    /// </summary>
    /// <param name="pose">Studio bone world-space matrices indexed by studio bone index.</param>
    /// <remarks>
    /// Typically called right before a character is turned into a ragdoll, so that the ragdoll keeps
    /// the pose of bones that have no rigid body - a fist stays clenched instead of relaxing into the
    /// bind pose. Capture is a deliberate, one-off operation: it is the only way to produce a
    /// <see cref="PoseSnapshot"/>, and nothing caches poses implicitly.
    /// </remarks>
    public PoseSnapshot CapturePoseSnapshot(ReadOnlySpan<Matrix3x4> pose)
    {
        if (IsNullSkeleton || studioBoneCount == 0)
            return new PoseSnapshot([]);

        var locals = new Matrix3x4[studioBoneCount];
        Array.Fill(locals, Matrix3x4.Identity);

        for (var boneIndex = 0; boneIndex < studioBoneCount; boneIndex++)
        {
            var anchorIndex = anchorBoneIndices[boneIndex];
            if (anchorIndex == boneIndex)
                continue; // simulated bones are never read back from a snapshot
            if ((uint)anchorIndex >= (uint)pose.Length || (uint)boneIndex >= (uint)pose.Length)
                continue;

            var anchorWorld = pose[anchorIndex].ToStrideMatrix();
            var boneWorld = pose[boneIndex].ToStrideMatrix();
            StrideMatrix.Invert(ref anchorWorld, out var inverseAnchorWorld);
            StrideMatrix.Multiply(ref inverseAnchorWorld, ref boneWorld, out var local);
            locals[boneIndex] = local.ToMatrix3x4();
        }

        return new PoseSnapshot(locals);
    }

    /// <summary>
    /// Applies or clears a pose snapshot captured by <see cref="CapturePoseSnapshot"/>. While a
    /// snapshot is applied, <see cref="SetupBones"/> reconstructs non-simulated bones from it.
    /// </summary>
    /// <param name="snapshot">The snapshot to apply, or null to clear the current one.</param>
    /// <remarks>
    /// A snapshot is only accepted when its studio bone count matches this skeleton's; otherwise it is
    /// ignored and the current one is cleared.
    /// </remarks>
    public void ApplyPoseSnapshot(PoseSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.BoneCount != studioBoneCount)
        {
            localPose = null;
            return;
        }

        // Convert once here so SetupBones stays free of conversions on the render path.
        var source = snapshot.LocalTransformsArray;
        var converted = new StrideMatrix[source.Length];
        for (var i = 0; i < source.Length; i++)
            converted[i] = source[i].ToStrideMatrix();

        localPose = converted;
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
        if (!IsEnabled || pivotPhysicsBone is not { } pivot)
        {
            Entity.Transform.UpdateWorldMatrix();
            return Entity.Transform.WorldMatrix;
        }

        var pivotWorld = GetBoneWorldMatrix(pivot);
        StrideMatrix.Multiply(ref pivotWorld, ref modelRootOffset, out var rootWorld);
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
        bone.Body?.Teleport(translation, rotation);
    }

    private static StrideMatrix GetBoneWorldMatrix(PhysicsBoneData bone)
    {
        if (bone.Body is { } body)
            return ComposeWorldMatrix(body.Position, body.Orientation);

        // Static bones (worldspawn) have no physics pose to read; fall back to the entity transform.
        bone.Bone.Transform.UpdateWorldMatrix();
        return bone.Bone.Transform.WorldMatrix;
    }

    private static StrideMatrix ComposeWorldMatrix(StrideVector3 translation, StrideQuaternion rotation)
    {
        var scale = StrideVector3.One;
        StrideMatrix.Transformation(ref scale, ref rotation, ref translation, out var world);
        return world;
    }
}
