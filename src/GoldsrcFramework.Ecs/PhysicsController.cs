using GoldsrcFramework.LinearMath;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using StrideMatrix = Stride.Core.Mathematics.Matrix;
using StrideVector3 = Stride.Core.Mathematics.Vector3;
using StrideQuaternion = Stride.Core.Mathematics.Quaternion;
using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Manages the physics bodies attached to a GoldSrc entity. Placed on the root entity
/// (not a prefab) and created at runtime by <see cref="HalfLifeBehavior"/>.
/// The physics skeleton (a set of child entities with <see cref="BodyComponent"/> and
/// constraints) is loaded via <see cref="LoadSkeleton"/> and can be detached/reattached
/// for ragdoll transitions.
/// </summary>
public sealed class PhysicsController : EntityComponent
{
    // ── 公共状态 ──────────────────────────────────────────
    public PhysicsMotionType MotionType { get; set; } = PhysicsMotionType.Kinematic;

    /// <summary>The bone used as the pivot for computing the model origin from physics transforms.</summary>
    public Entity? PivotPhysicsBone { get; private set; }

    /// <summary>Offset from the pivot bone's world transform to the model origin, computed in T-pose.</summary>
    public StrideMatrix ModelRootOffset { get; private set; } = StrideMatrix.Identity;

    /// <summary>Native model pointer (model_t*) for non-player entities. Used for model validation.</summary>
    public IntPtr ModelPointer { get; private set; }

    /// <summary>Model name for player entities. Used for model validation.</summary>
    public string? ModelName { get; private set; }

    /// <summary>Whether this controller belongs to a player entity.</summary>
    public bool IsPlayer { get; private set; }

    /// <summary>Whether the skeleton has been detached (entity is in ragdoll state).</summary>
    public bool RagdollRigged { get; private set; }

    // ── 内部数据（由 LoadSkeleton 构建）────────────────────
    private readonly List<Entity> physicsBones = [];
    private readonly List<int> boneIndices = [];
    private readonly List<bool> isAddon = [];
    private readonly List<ConstraintComponentBase> regularConstraints = [];
    private readonly List<ConstraintComponentBase> addonConstraints = [];

    // ── 备份（DetachSkeleton 时使用）──────────────────────
    private List<Entity>? detachedBones;

    public IReadOnlyList<Entity> PhysicsBones => physicsBones;

    /// <summary>True when no physics skeleton is loaded (e.g. missing .gpd). All operations are no-ops.</summary>
    public bool IsNullSkeleton => physicsBones.Count == 0;

    // ── LoadSkeleton ──────────────────────────────────────

    /// <summary>
    /// Builds the internal index from instantiated physics bone entities and computes
    /// <see cref="PivotPhysicsBone"/> / <see cref="ModelRootOffset"/>.
    /// Caller must have already added the bone entities as children of the root entity.
    /// </summary>
    public void LoadSkeleton(IEnumerable<Entity> physicsBoneEntities, IntPtr modelPointer, bool isPlayer)
    {
        IsPlayer = isPlayer;
        ModelPointer = modelPointer;
        ModelName = null;
        BuildFromEntities(physicsBoneEntities);
    }

    /// <summary>
    /// Builds the internal index from instantiated physics bone entities and computes
    /// <see cref="PivotPhysicsBone"/> / <see cref="ModelRootOffset"/>.
    /// Caller must have already added the bone entities as children of the root entity.
    /// </summary>
    public void LoadSkeleton(IEnumerable<Entity> physicsBoneEntities, string modelName, bool isPlayer)
    {
        IsPlayer = isPlayer;
        ModelName = modelName;
        ModelPointer = IntPtr.Zero;
        BuildFromEntities(physicsBoneEntities);
    }

    private void BuildFromEntities(IEnumerable<Entity> physicsBoneEntities)
    {
        // Clear previous state
        physicsBones.Clear();
        boneIndices.Clear();
        isAddon.Clear();
        regularConstraints.Clear();
        addonConstraints.Clear();
        PivotPhysicsBone = null;
        ModelRootOffset = StrideMatrix.Identity;

        var boneList = physicsBoneEntities.ToList();
        if (boneList.Count == 0)
        {
            // NullPhysicsSkeleton
            ApplyMotionType();
            return;
        }

        // Collect bones and their BoneLink data
        var addonBoneSet = new HashSet<Entity>();
        foreach (var bone in boneList)
        {
            var link = bone.Get<BoneLink>();
            if (link is null)
                continue;

            physicsBones.Add(bone);
            boneIndices.Add(link.BoneIndex);
            isAddon.Add(link.IsAddon);
            if (link.IsAddon)
                addonBoneSet.Add(bone);
        }

        // Collect constraints, split into regular / addon
        foreach (var bone in boneList)
        {
            foreach (var component in bone.Components)
            {
                if (component is not ConstraintComponentBase constraint)
                    continue;

                bool connectsAddon = false;
                foreach (var body in constraint.Bodies)
                {
                    if (body is { Entity: { } bodyEntity } && addonBoneSet.Contains(bodyEntity))
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

        // Compute PivotPhysicsBone: bone with smallest bone index (prefer index 0)
        int pivotIndex = 0;
        int minBoneIndex = int.MaxValue;
        for (int i = 0; i < physicsBones.Count; i++)
        {
            if (boneIndices[i] < minBoneIndex)
            {
                minBoneIndex = boneIndices[i];
                pivotIndex = i;
            }
        }
        PivotPhysicsBone = physicsBones[pivotIndex];

        // ModelRootOffset = Invert(Pivot.WorldMatrix) at T-pose
        // The root entity should be at its initial transform; bone local transforms
        // were set to T-pose values by the ContentManager.
        Entity.Transform.UpdateWorldMatrix();
        var pivotWorld = PivotPhysicsBone.Transform.WorldMatrix;
        StrideMatrix.Invert(ref pivotWorld, out var offset);
        ModelRootOffset = offset;

        ApplyMotionType();
    }

    // ── SetPose / GetPose ─────────────────────────────────

    /// <summary>
    /// Syncs GoldSrc bone world transforms to physics bones via Teleport.
    /// <paramref name="pose"/> contains bone world-space matrices indexed by animation bone index.
    /// For brush models, only pose[0] (model origin) is used.
    /// <paramref name="settingMask"/> can disable specific bones (e.g. jiggle bones).
    /// </summary>
    public void SetPose(ReadOnlySpan<Matrix3x4> pose, bool[]? settingMask = null)
    {
        if (IsNullSkeleton)
            return;

        for (int i = 0; i < physicsBones.Count; i++)
        {
            int boneIdx = boneIndices[i];
            if (boneIdx >= pose.Length)
                continue;
            if (settingMask is not null && boneIdx < settingMask.Length && !settingMask[boneIdx])
                continue;

            var strideMatrix = pose[boneIdx].ToStrideMatrix();
            SetPhysicsBonePose(physicsBones[i], strideMatrix);
        }
    }

    /// <summary>
    /// Reads physics transforms back into <paramref name="pose"/>.
    /// pose[0] is computed as the model origin from <see cref="PivotPhysicsBone"/> × <see cref="ModelRootOffset"/>.
    /// Other entries are the world transforms of each physics bone, indexed by animation bone index.
    /// </summary>
    public void GetPose(Span<Matrix3x4> pose, bool[]? settingMask = null)
    {
        if (IsNullSkeleton)
            return;

        // pose[0] = model origin from pivot bone
        if (pose.Length > 0 && PivotPhysicsBone is { } pivot)
        {
            if (settingMask is null || settingMask.Length == 0 || settingMask[0])
            {
                var pivotWorld = pivot.Transform.WorldMatrix;
                var offset = ModelRootOffset;
                StrideMatrix.Multiply(ref pivotWorld, ref offset, out var origin);
                pose[0] = origin.ToMatrix3x4();
            }
        }

        // Other bones
        for (int i = 0; i < physicsBones.Count; i++)
        {
            int boneIdx = boneIndices[i];
            if (boneIdx == 0 || boneIdx >= pose.Length)
                continue;
            if (settingMask is not null && boneIdx < settingMask.Length && !settingMask[boneIdx])
                continue;

            pose[boneIdx] = physicsBones[i].Transform.WorldMatrix.ToMatrix3x4();
        }
    }

    /// <summary>
    /// Sets a single physics bone's pose from a Stride world-space matrix.
    /// Accounts for the body's <see cref="CollidableComponent.CenterOfMass"/> so the
    /// entity ends up exactly at the requested position.
    /// </summary>
    public void SetPhysicsBonePose(Entity physicsBone, StrideMatrix pose)
    {
        pose.Decompose(out _, out StrideQuaternion rotation, out StrideVector3 translation);

        if (physicsBone.Get<BodyComponent>() is { } body)
        {
            // BodyComponent.Teleport expects the center-of-mass world position.
            // entityWorldPos = teleportPos - CenterOfMass * worldRot
            // => teleportPos = entityWorldPos + CenterOfMass * worldRot
            var com = body.CenterOfMass;
            if (com != StrideVector3.Zero)
            {
                var comWorld = StrideVector3.Transform(com, rotation);
                translation += comWorld;
            }
            body.Teleport(translation, rotation);
        }
        else if (physicsBone.Get<StaticComponent>() is { } staticBody)
        {
            staticBody.Teleport(translation, rotation);
        }
        else
        {
            // No physics component: set transform directly
            physicsBone.Transform.Position = translation;
            physicsBone.Transform.Rotation = rotation;
        }
    }

    // ── Enable / Disable ──────────────────────────────────

    /// <summary>Enables physics by applying the current <see cref="MotionType"/> to all bones and constraints.</summary>
    public void Enable()
    {
        ApplyMotionType();
    }

    /// <summary>
    /// Disables physics: all bodies are set to kinematic (sleeping) and all constraints
    /// are disabled. The bone entities remain in the scene for later reuse.
    /// </summary>
    public void Disable()
    {
        foreach (var bone in physicsBones)
        {
            if (bone.Get<BodyComponent>() is { } body)
                body.Kinematic = true;
        }

        foreach (var c in regularConstraints)
            c.Enabled = false;
        foreach (var c in addonConstraints)
            c.Enabled = false;
    }

    private void ApplyMotionType()
    {
        for (int i = 0; i < physicsBones.Count; i++)
        {
            if (physicsBones[i].Get<BodyComponent>() is not { } body)
                continue;

            // addon bones are always dynamic; non-addon bones follow MotionType
            if (isAddon[i])
            {
                body.Kinematic = false;
            }
            else
            {
                body.Kinematic = MotionType switch
                {
                    PhysicsMotionType.Kinematic => true,
                    PhysicsMotionType.Dynamic => false,
                    _ => true,
                };
            }
        }

        // Constraints
        bool kinematicMode = MotionType == PhysicsMotionType.Kinematic;
        foreach (var c in regularConstraints)
            c.Enabled = !kinematicMode; // regular constraints only active in dynamic mode
        foreach (var c in addonConstraints)
            c.Enabled = true;            // addon constraints always active
    }

    // ── Detach / Reattach (ragdoll) ───────────────────────

    /// <summary>
    /// Detaches the physics skeleton from the root entity and switches to NullPhysicsSkeleton.
    /// Used when the entity dies and a ragdoll temp entity takes over physics.
    /// The bone entities are kept in memory for later reattachment.
    /// </summary>
    public void DetachSkeleton()
    {
        if (IsNullSkeleton || detachedBones is not null)
            return;

        detachedBones = [..physicsBones];

        // Remove bones from parent (also removes from scene → physics stops)
        foreach (var bone in physicsBones)
            bone.Transform.Parent = null;

        // Switch to NullPhysicsSkeleton state
        physicsBones.Clear();
        boneIndices.Clear();
        isAddon.Clear();
        regularConstraints.Clear();
        addonConstraints.Clear();
        PivotPhysicsBone = null;

        RagdollRigged = true;
    }

    /// <summary>
    /// Reattaches the previously detached physics skeleton and rebuilds the internal index.
    /// Used when the entity respawns.
    /// </summary>
    public void ReattachSkeleton()
    {
        if (detachedBones is null || detachedBones.Count == 0)
            return;

        foreach (var bone in detachedBones)
            bone.Transform.Parent = Entity.Transform;

        // Rebuild internal index
        if (IsPlayer)
            LoadSkeleton(detachedBones, ModelName!, true);
        else
            LoadSkeleton(detachedBones, ModelPointer, false);

        detachedBones = null;
        RagdollRigged = false;
    }

    // ── Root transform from pivot (dynamic) ───────────────

    /// <summary>
    /// Computes the root entity's world transform from the pivot physics bone and
    /// <see cref="ModelRootOffset"/>, then re-teleports all bones to fix up local
    /// coordinates. Call this in LateUpdate for dynamic (ragdoll) entities.
    /// </summary>
    public void ApplyRootTransformFromPivot(Entity rootEntity)
    {
        if (PivotPhysicsBone is null || IsNullSkeleton)
            return;

        // rootWorld = pivotWorld * ModelRootOffset
        var pivotWorld = PivotPhysicsBone.Transform.WorldMatrix;
        var offset = ModelRootOffset;
        StrideMatrix.Multiply(ref pivotWorld, ref offset, out var rootWorld);

        rootEntity.Transform.WorldMatrix = rootWorld;

        // Re-teleport all bones so their local transforms are correct relative to the new root.
        // Teleport on the body is a no-op (same pose); only the entity local transform is updated.
        foreach (var bone in physicsBones)
        {
            SetPhysicsBonePose(bone, bone.Transform.WorldMatrix);
        }
    }

    // ── Model validation ──────────────────────────────────

    /// <summary>Returns true if the current model pointer matches the controller's model.</summary>
    public bool ValidateModel(IntPtr currentPointer)
    {
        if (IsPlayer)
            return false;
        return ModelPointer == currentPointer;
    }

    /// <summary>Returns true if the current model name matches the controller's model.</summary>
    public bool ValidateModel(string currentName)
    {
        if (!IsPlayer)
            return false;
        return string.Equals(ModelName, currentName, StringComparison.OrdinalIgnoreCase);
    }
}
