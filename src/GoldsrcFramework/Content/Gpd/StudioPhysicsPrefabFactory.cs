using GoldsrcFramework.Ecs;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Engine;
using GoldsrcQuaternion = GoldsrcFramework.LinearMath.Quaternion;
using GoldsrcVector3 = GoldsrcFramework.LinearMath.Vector3;
using StrideMatrix = Stride.Core.Mathematics.Matrix;
using StrideQuaternion = Stride.Core.Mathematics.Quaternion;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace GoldsrcFramework.Content.Gpd;

/// <summary>
/// Builds the physics skeleton <see cref="Prefab"/> of a studio model out of a parsed
/// <see cref="GpdAsset"/> and the model's studio header.
/// </summary>
/// <remarks>
/// <para>
/// One entity per <b>simulated</b> studio bone - the bones that carry a rigid body in the
/// <c>.gpd</c>, which is only a subset of the skeleton. Non-simulated bones (fingers, face) get no
/// entity: their pose is rebuilt by <see cref="PhysicsController.SetupBones"/> from their nearest
/// simulated ancestor.
/// </para>
/// <para>
/// <b>Frames.</b> The legacy data puts a rigid body at <c>rigidWorld = boneWorld * offset</c>
/// (gsphysics <c>BoneMotionState::getWorldTransform</c>) and states both its collision geometry and
/// its joint frames relative to that rigid body. This framework has no rigid-body offset - the body
/// <i>is</i> the bone (<see cref="PhysicsController.SetPose"/> teleports a bone body straight onto
/// the bone's animated pose), so every sub-shape pose and joint frame is re-expressed in the bone's
/// frame as <c>local = offset * frame</c>.
/// </para>
/// <para>
/// <b>Bind pose.</b> Each bone entity's local transform is its <b>T-pose</b> transform relative to
/// the model root, accumulated from the studio bone table (<c>value[0..2]</c> position and
/// <c>value[3..5]</c> radian rotation, chained through <c>parent</c>).
/// <see cref="PhysicsController.LoadSkeleton"/> derives the model-origin offset from it, which is
/// what lets a dynamic ragdoll drive the root entity again.
/// </para>
/// <para>
/// <b>Joint limits.</b> The angular limits of hinge and cone joints are parsed and kept on the
/// asset, but this revision does not apply them - the documented v1 behaviour, so a ragdoll's joints
/// are currently free. Applying them is a later change confined to this factory.
/// </para>
/// </remarks>
internal static class StudioPhysicsPrefabFactory
{
    /// <summary>
    /// Upper bound on the studio bones a model may declare. GoldSrc itself allows 128; the higher
    /// bound tolerates engines with an extended limit while keeping the arrays bounded.
    /// </summary>
    private const int MaxStudioBones = 512;

    /// <summary>
    /// Builds the skeleton prefab, or returns null with a reason when the asset cannot be expressed
    /// as a physics skeleton for this model.
    /// </summary>
    public static unsafe Prefab? TryCreate(GpdAsset asset, studiohdr_t* header, out string error)
    {
        error = string.Empty;

        if (header == null)
        {
            error = "the model has no studio header";
            return null;
        }

        int boneCount = header->numbones;
        if (boneCount <= 0 || boneCount > MaxStudioBones)
        {
            error = $"the model declares {boneCount} studio bones";
            return null;
        }

        if (asset.RigidBodies.Length == 0)
        {
            error = "the asset declares no rigid body";
            return null;
        }

        var bindPose = BuildBindPose(header, boneCount);

        // ── Bodies ────────────────────────────────────────────────────────────
        var bodies = new BodyComponent[asset.RigidBodies.Length];
        var entities = new Entity[asset.RigidBodies.Length];

        for (int i = 0; i < asset.RigidBodies.Length; i++)
        {
            var rigidBody = asset.RigidBodies[i];
            if ((uint)rigidBody.BoneIndex >= (uint)boneCount)
            {
                error = $"rigidbody {i} refers to bone {rigidBody.BoneIndex}, outside the {boneCount} bones of the model";
                return null;
            }

            if (!TryBuildCollider(asset.Shapes[rigidBody.ShapeIndex], rigidBody.BoneToRigid, out var collider, out error))
                return null;

            var body = new BodyComponent
            {
                Collider = collider,
                // Kinematic while the entity is alive; PhysicsController switches the addon bodies and
                // the whole skeleton for a ragdoll.
                Kinematic = true,
            };

            var entity = new Entity($"PhysicsBone_{rigidBody.BoneIndex}");
            entity.Components.Add(new BoneLink
            {
                StudioBoneIndex = rigidBody.BoneIndex,
                IsAddon = rigidBody.Type == GpdRigidBodyType.Addon,
            });
            entity.Components.Add(body);
            ApplyLocalTransform(entity, bindPose[rigidBody.BoneIndex]);

            bodies[i] = body;
            entities[i] = entity;
        }

        // ── Joints ────────────────────────────────────────────────────────────
        foreach (var constraint in asset.Constraints)
        {
            var component = TryCreateConstraint(constraint, asset.RigidBodies, bodies);
            if (component is null)
            {
                error = $"constraint between rigidbody {constraint.BodyA} and {constraint.BodyB} cannot be represented";
                return null;
            }

            // The joint lives on body A's entity, like every other physics component of the skeleton.
            entities[constraint.BodyA].Components.Add(component);
        }

        var prefab = new Prefab();
        foreach (var entity in entities)
            prefab.Entities.Add(entity);

        return prefab;
    }

    // ── Collision ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts one collision shape into a compound collider whose children are posed relative to
    /// the bone instead of the legacy rigid body.
    /// </summary>
    private static bool TryBuildCollider(
        GpdShape shape,
        in Matrix3x4 boneToRigid,
        out CompoundCollider collider,
        out string error)
    {
        error = string.Empty;
        collider = new CompoundCollider();

        if (shape.SubShapes.Length == 0)
        {
            error = "a collision shape holds no sub-shape";
            return false;
        }

        for (int i = 0; i < shape.SubShapes.Length; i++)
        {
            var subShape = shape.SubShapes[i];

            // Rigid body frame -> bone frame. The collider is attached to a body whose pose is the
            // bone pose, so the legacy rigid-body offset has to be folded into the child pose.
            Matrix3x4.ConcatTransforms(boneToRigid, subShape.LocalTransform, out var local);
            var (position, rotation) = Decompose(local);

            switch (subShape.ShapeType)
            {
                case GpdShapeType.Box:
                    collider.Colliders.Add(new BoxCollider
                    {
                        // The legacy half extent is a half size; Stride takes the full size.
                        Size = new StrideVector3(
                            subShape.HalfExtent.X * 2.0f,
                            subShape.HalfExtent.Y * 2.0f,
                            subShape.HalfExtent.Z * 2.0f),
                        PositionLocal = position,
                        RotationLocal = rotation,
                    });
                    break;

                case GpdShapeType.Capsule:
                    collider.Colliders.Add(new CapsuleCollider
                    {
                        Radius = subShape.Radius,
                        Length = subShape.CylinderLength,
                        PositionLocal = position,
                        RotationLocal = AlignCapsuleAxisToX(local),
                    });
                    break;

                default:
                    error = $"sub-shape {i} uses an unsupported shape kind";
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Rotation of a capsule whose axis has to follow the local X axis of <paramref name="local"/>.
    /// </summary>
    /// <remarks>
    /// The legacy data comes from Bullet, whose capsules run along their local X
    /// (<c>btCapsuleShapeX</c>); a Bepu capsule runs along its local Y. The rotation is built by
    /// re-mapping the frame's axes instead of multiplying two rotations, which keeps the axis
    /// convention unambiguous: the image of Y becomes the frame's X axis, and the other two are
    /// placed so the result stays a proper rotation. Which of the two remaining axes is which cannot
    /// matter - a capsule is rotationally symmetric around its own axis.
    /// </remarks>
    private static StrideQuaternion AlignCapsuleAxisToX(in Matrix3x4 local)
    {
        // In Stride's row-vector convention the rows of a rotation are the images of X, Y and Z.
        // ToStrideMatrix() puts GoldSrc's column 0 (the frame's X axis) into Stride row 0, and so on.
        var frame = local.ToStrideMatrix();

        var capsule = new StrideMatrix(
            frame.M31, frame.M32, frame.M33, 0, // image of X = frame Z
            frame.M11, frame.M12, frame.M13, 0, // image of Y = frame X  (the capsule axis)
            frame.M21, frame.M22, frame.M23, 0, // image of Z = frame Y
            0, 0, 0, 1);

        capsule.Decompose(out StrideVector3 _, out StrideQuaternion rotation, out StrideVector3 _);
        return StrideQuaternion.Normalize(rotation);
    }

    // ── Joints ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts one legacy joint into the Stride Bepu component that represents it.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>spherical</c> (Bullet point to point) becomes a ball socket.</item>
    /// <item><c>hinge</c> becomes a hinge. Bullet's hinge axis is the frame's third basis column, so
    /// that is what the hinge axis is read from.</item>
    /// <item><c>cone</c> (Bullet cone twist) becomes a swivel hinge: its twist axis is the frame's
    /// first basis column, which becomes the swivel axis of A and the hinge axis of B.</item>
    /// </list>
    /// No angular limits are applied yet, see the type remarks.
    /// </remarks>
    private static ConstraintComponentBase? TryCreateConstraint(
        GpdConstraint constraint,
        GpdRigidBody[] rigidBodies,
        BodyComponent[] bodies)
    {
        var bodyA = rigidBodies[constraint.BodyA];
        var bodyB = rigidBodies[constraint.BodyB];

        // The legacy frames are relative to the rigid bodies; the offset is baked away, so they are
        // re-expressed relative to the bones first.
        Matrix3x4.ConcatTransforms(bodyA.BoneToRigid, constraint.FrameInA, out var frameA);
        Matrix3x4.ConcatTransforms(bodyB.BoneToRigid, constraint.FrameInB, out var frameB);

        ConstraintComponentBase component;
        switch (constraint.Type)
        {
            case GpdConstraintType.Spherical:
                component = new BallSocketConstraintComponent
                {
                    LocalOffsetA = ToStride(frameA.Origin),
                    LocalOffsetB = ToStride(frameB.Origin),
                };
                break;

            case GpdConstraintType.Hinge:
                component = new HingeConstraintComponent
                {
                    LocalOffsetA = ToStride(frameA.Origin),
                    LocalHingeAxisA = Axis(frameA, 2),
                    LocalOffsetB = ToStride(frameB.Origin),
                    LocalHingeAxisB = Axis(frameB, 2),
                };
                break;

            case GpdConstraintType.Cone:
                component = new SwivelHingeConstraintComponent
                {
                    LocalOffsetA = ToStride(frameA.Origin),
                    LocalSwivelAxisA = Axis(frameA, 0),
                    LocalOffsetB = ToStride(frameB.Origin),
                    LocalHingeAxisB = Axis(frameB, 0),
                };
                break;

            default:
                return null;
        }

        // Every mapped joint relates exactly two bodies.
        var twoBody = (ITwoBody)component;
        twoBody.A = bodies[constraint.BodyA];
        twoBody.B = bodies[constraint.BodyB];
        return component;
    }

    /// <summary>
    /// Basis column <paramref name="column"/> of a joint frame, as a unit vector in the body's local
    /// space.
    /// </summary>
    /// <remarks>
    /// The GoldSrc transform is column-vector, so its basis columns are the frame's own axes. Both
    /// GoldSrc and Stride label the axes the same way, so a direction maps component for component.
    /// </remarks>
    private static StrideVector3 Axis(in Matrix3x4 frame, int column)
    {
        var axis = column switch
        {
            0 => new StrideVector3(frame.M11, frame.M21, frame.M31),
            1 => new StrideVector3(frame.M12, frame.M22, frame.M32),
            _ => new StrideVector3(frame.M13, frame.M23, frame.M33),
        };

        // Rounding in the stored matrices leaves the axis very slightly off unit length, and Bepu
        // expects a unit axis.
        return Vector3Normalize(axis);
    }

    // ── Bind pose ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Per studio bone, its T-pose transform relative to the model root.
    /// </summary>
    /// <remarks>
    /// The studio bone table stores each bone's default pose relative to its parent, so the transform
    /// relative to the model root is the product along the parent chain. Parents are followed with a
    /// cycle guard: a malformed hierarchy resolves to identity instead of hanging.
    /// </remarks>
    private static unsafe Matrix3x4[] BuildBindPose(studiohdr_t* header, int boneCount)
    {
        var bones = header->GetBones();
        var result = new Matrix3x4[boneCount];
        var state = new byte[boneCount]; // 0 = unresolved, 1 = resolving, 2 = resolved

        for (int bone = 0; bone < boneCount; bone++)
            result[bone] = Resolve(bone, bones, result, state);

        return result;
    }

    private static unsafe Matrix3x4 Resolve(int bone, mstudiobone_t* bones, Matrix3x4[] result, byte[] state)
    {
        const byte Resolving = 1;
        const byte Resolved = 2;

        if (state[bone] == Resolved)
            return result[bone];
        if (state[bone] == Resolving)
            return Matrix3x4.Identity; // malformed cyclic hierarchy

        state[bone] = Resolving;

        var local = BindPoseLocal(bones + bone);
        int parent = bones[bone].parent;

        Matrix3x4 toRoot;
        if ((uint)parent < (uint)result.Length && parent != bone)
            Matrix3x4.ConcatTransforms(Resolve(parent, bones, result, state), local, out toRoot);
        else
            toRoot = local;

        result[bone] = toRoot;
        state[bone] = Resolved;
        return toRoot;
    }

    /// <summary>
    /// Default (bind) transform of one bone relative to its parent.
    /// </summary>
    /// <remarks>
    /// <c>value[0..2]</c> is the bone position, <c>value[3..5]</c> its rotation in radians - the same
    /// convention GoldSrc uses when it turns a bone into a matrix, so the rotation goes through
    /// <see cref="GoldsrcQuaternion.AngleQuaternion(float*, out GoldsrcQuaternion)"/> and then
    /// <see cref="Matrix3x4.QuaternionMatrix(GoldsrcQuaternion, out Matrix3x4)"/>.
    /// </remarks>
    private static unsafe Matrix3x4 BindPoseLocal(mstudiobone_t* bone)
    {
        float* angles = stackalloc float[3];
        angles[0] = bone->value[3];
        angles[1] = bone->value[4];
        angles[2] = bone->value[5];

        GoldsrcQuaternion.AngleQuaternion(angles, out var rotation);

        Matrix3x4.QuaternionMatrix(rotation, out var local);
        local.Origin = new GoldsrcVector3(bone->value[0], bone->value[1], bone->value[2]);
        return local;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a bone's bind transform into its entity. <see cref="PhysicsController.Enable"/>
    /// reparents the bones directly onto the root entity, so this is the transform relative to the
    /// model root, not relative to the parent bone.
    /// </summary>
    private static void ApplyLocalTransform(Entity entity, in Matrix3x4 toRoot)
    {
        var (position, rotation) = Decompose(toRoot);
        entity.Transform.Position = position;
        entity.Transform.Rotation = rotation;
    }

    /// <summary>
    /// Splits a GoldSrc transform into a Stride position and a normalized rotation.
    /// </summary>
    private static (StrideVector3 Position, StrideQuaternion Rotation) Decompose(in Matrix3x4 goldsrc)
    {
        var stride = goldsrc.ToStrideMatrix();
        stride.Decompose(out StrideVector3 _, out StrideQuaternion rotation, out StrideVector3 position);
        return (position, StrideQuaternion.Normalize(rotation));
    }

    private static StrideVector3 ToStride(in GoldsrcVector3 value) => new(value.X, value.Y, value.Z);

    private static StrideVector3 Vector3Normalize(StrideVector3 value)
    {
        float lengthSquared = value.LengthSquared();
        return lengthSquared > float.Epsilon ? value / MathF.Sqrt(lengthSquared) : StrideVector3.UnitX;
    }
}
