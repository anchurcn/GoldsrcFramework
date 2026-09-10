using GoldsrcFramework.LinearMath;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.Engine;
using StrideMatrix = Stride.Core.Mathematics.Matrix;
using StrideQuaternion = Stride.Core.Mathematics.Quaternion;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Manages the physics skeleton of a GoldSrc entity: a set of child entities carrying physics
/// bodies and constraints (produced by instantiating a prefab, or by cloning for ragdolls).
/// Lives on the root entity, not in a prefab.
/// <para>
/// Responsibilities are deliberately narrow: enable/disable the skeleton, write studio model bone
/// transforms into it (<see cref="SetPose"/>), read them back (<see cref="GetPose"/>), and derive
/// the model root transform (<see cref="GetModelRootTransform"/>). Entity level concerns such as
/// model identity tracking and ragdoll state belong to <see cref="HalfLifeBehavior"/>.
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
    /// <summary>
    /// Skeleton level default mask applied when <see cref="SetPose"/>/<see cref="GetPose"/> are called
    /// without an explicit mask. Reserved for future use; nothing sets it yet.
    /// </summary>
    private bool[]? settingMasks = null;
    private PhysicsMotionType motionType = PhysicsMotionType.Kinematic;

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

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Takes ownership of a set of physics bone entities. Any previously loaded skeleton is detached
    /// first. An empty collection produces a NullPhysicsSkeleton.
    /// </summary>
    /// <remarks>
    /// The newly loaded skeleton is left <b>detached</b> (<see cref="IsEnabled"/> is false); call
    /// <see cref="Enable"/> afterwards to attach it to the root entity and register its bodies.
    /// </remarks>
    public void LoadSkeleton(IEnumerable<Entity> physicsBoneEntities)
    {
        DetachBones();

        physicsBonesInternal.Clear();
        boneEntities.Clear();
        regularConstraints.Clear();
        addonConstraints.Clear();
        pivotPhysicsBone = null;
        modelRootOffset = StrideMatrix.Identity;
        IsEnabled = false;

        var boneList = physicsBoneEntities as IList<Entity> ?? physicsBoneEntities.ToList();

        foreach (var entity in boneList)
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
                StudioBoneIndex = link.BoneIndex,
                IsAddon = link.IsAddon,
            });
            boneEntities.Add(entity);
        }

        if (physicsBonesInternal.Count == 0)
            return; // NullPhysicsSkeleton

        CollectConstraints();

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
    /// Writes studio model bone world transforms into the physics skeleton.
    /// </summary>
    /// <param name="pose">
    /// Bone world-space matrices (Matrix3x4) indexed by studio bone index.
    /// Brush models only use <c>pose[0]</c> (the model origin world transform).
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
    /// Reads bone world transforms out of the physics skeleton.
    /// </summary>
    /// <param name="pose">
    /// Output array (Matrix3x4) indexed by studio bone index. <c>pose[0]</c> receives the model origin
    /// world transform. Entries that map to no physics bone are left untouched.
    /// </param>
    /// <param name="settingMask">
    /// Optional per-studio-bone mask; bones whose entry is false are skipped.
    /// When null, the controller level <c>settingMasks</c> is used instead.
    /// </param>
    /// <remarks>
    /// Values come from the physics poses (not from <c>Transform.WorldMatrix</c>), so they are always
    /// up to date within the frame and include the simulated result of addon (jiggle) bones.
    /// </remarks>
    public void GetPose(Span<Matrix3x4> pose, bool[]? settingMask = null)
    {
        if (!IsEnabled || IsNullSkeleton)
            return;

        var mask = settingMask ?? settingMasks;

        if (pose.Length > 0)
            pose[0] = GetModelRootTransform().ToMatrix3x4();

        foreach (var bone in physicsBonesInternal)
        {
            if (bone.Body is not { } body)
                continue;

            var boneIndex = bone.StudioBoneIndex;
            if (boneIndex == 0 || (uint)boneIndex >= (uint)pose.Length)
                continue;
            if (mask is not null && boneIndex < mask.Length && !mask[boneIndex])
                continue;

            pose[boneIndex] = ComposeWorldMatrix(body.Position, body.Orientation).ToMatrix3x4();
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
