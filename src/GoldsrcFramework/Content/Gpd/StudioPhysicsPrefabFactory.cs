using GoldsrcFramework.Ecs;
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.BepuPhysics.Definitions;
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
/// frame as <c>local = offset * frame</c>. A sub-shape's pose ends there, being relative to the bone;
/// a joint frame takes one more step, because Bepu measures it from the body's center of mass rather
/// than from the body's origin - see <see cref="TryCreateConstraint"/>.
/// </para>
/// <para>
/// <b>Bind pose.</b> Each bone entity's local transform is its <b>T-pose</b> transform relative to
/// the model root, accumulated from the studio bone table (<c>value[0..2]</c> position and
/// <c>value[3..5]</c> radian rotation, chained through <c>parent</c>).
/// <see cref="PhysicsController.LoadSkeleton"/> derives the model-origin offset from it, which is
/// what lets a dynamic ragdoll drive the root entity again.
/// </para>
/// <para>
/// <b>Joint limits.</b> The angular limits of hinge and cone joints are parsed and applied: a hinge
/// carries its low/high range, a cone its swing span and its symmetric twist span. Neither maps onto
/// a single Bepu constraint, so a joint becomes a small set of them - see
/// <see cref="TryCreateConstraint"/> for the mapping and its derivation.
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
        var centersOfMass = new GoldsrcVector3[asset.RigidBodies.Length];

        for (int i = 0; i < asset.RigidBodies.Length; i++)
        {
            var rigidBody = asset.RigidBodies[i];
            if ((uint)rigidBody.BoneIndex >= (uint)boneCount)
            {
                error = $"rigidbody {i} refers to bone {rigidBody.BoneIndex}, outside the {boneCount} bones of the model";
                return null;
            }

            if (!TryBuildCollider(asset.Shapes[rigidBody.ShapeIndex], rigidBody.BoneToRigid, out var collider, out centersOfMass[i], out error))
                return null;

            var body = new BodyComponent
            {
                Collider = collider,
                // Kinematic while the entity is alive; PhysicsController switches the addon bodies and
                // the whole skeleton for a ragdoll.
                Kinematic = true,
                // Only a placeholder, so that a bare prefab - one put into a simulation without a
                // PhysicsController - still does not let a skeleton collide with itself. Which bones
                // of a skeleton may touch which, and which skeletons may touch each other, is worked
                // out and written when a skeleton is loaded, from the joint graph of the skeleton as
                // it stands then - see SkeletonCollisionGroups and PhysicsController.LoadSkeleton.
                CollisionGroup = new CollisionGroup { Id = SkeletonCollisionGroups.PrefabGroupId },
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
            var components = TryCreateConstraint(constraint, asset.RigidBodies, bodies, centersOfMass);
            if (components is null)
            {
                error = $"constraint between rigidbody {constraint.BodyA} and {constraint.BodyB} cannot be represented";
                return null;
            }

            // The joint lives on body A's entity, like every other physics component of the skeleton.
            // A joint is one to three components, always added in the same order so that a later clone
            // of the bone - which copies components in order - can pair them back up one for one.
            foreach (var component in components)
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
    /// <param name="centerOfMass">
    /// Where the compound's center of mass ends up, in bone local space. A joint frame is measured
    /// from that center rather than from the bone, so the joint code needs it - see
    /// <see cref="TryCreateConstraint"/>. Exact for the single sub-shape every collision shape block
    /// in the wild holds, because with one child its weight cancels out of Bepu's average; with
    /// several it is the plain centroid, the weights Bepu assigns not being knowable from here.
    /// </param>
    private static bool TryBuildCollider(
        GpdShape shape,
        in Matrix3x4 boneToRigid,
        out CompoundCollider collider,
        out GoldsrcVector3 centerOfMass,
        out string error)
    {
        error = string.Empty;
        collider = new CompoundCollider();
        centerOfMass = default;

        if (shape.SubShapes.Length == 0)
        {
            error = "a collision shape holds no sub-shape";
            return false;
        }

        float centerX = 0.0f, centerY = 0.0f, centerZ = 0.0f;

        for (int i = 0; i < shape.SubShapes.Length; i++)
        {
            var subShape = shape.SubShapes[i];

            // Rigid body frame -> bone frame. The collider is attached to a body whose pose is the
            // bone pose, so the legacy rigid-body offset has to be folded into the child pose.
            Matrix3x4.ConcatTransforms(boneToRigid, subShape.LocalTransform, out var local);
            var (position, rotation) = Decompose(local);

            centerX += position.X;
            centerY += position.Y;
            centerZ += position.Z;

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

        float inverseCount = 1.0f / shape.SubShapes.Length;
        centerOfMass = new GoldsrcVector3(centerX * inverseCount, centerY * inverseCount, centerZ * inverseCount);

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

    /// <summary>Degrees to radians, the conversion every legacy angular limit needs.</summary>
    private const float DegreesToRadians = MathF.PI / 180.0f;

    /// <summary>
    /// Converts one legacy joint into the Stride Bepu components that represent it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bepu has no hinge-with-limits and no cone twist, so both are built out of smaller pieces -
    /// the same composition the Bepu ragdoll demo uses for its shoulders and hips:
    /// </para>
    /// <list type="bullet">
    /// <item><c>spherical</c> (Bullet point to point) is one ball socket.</item>
    /// <item><c>hinge</c> is a <see cref="HingeConstraintComponent"/> (point + axis alignment, which
    /// leaves one rotational degree of freedom) plus a <see cref="TwistLimitConstraintComponent"/>
    /// around that same free axis to hold the legacy low/high range. The hinge axis is the frame's
    /// third basis column.</item>
    /// <item><c>cone</c> (Bullet cone twist) is a ball socket, a
    /// <see cref="SwingLimitConstraintComponent"/> around the frame's first basis column, and a
    /// <see cref="TwistLimitConstraintComponent"/> about that same column. (The two twist axes are
    /// collinear, so this can never be a swivel hinge, whose axes must be perpendicular.)</item>
    /// </list>
    /// <para>
    /// <b>Why the twist bases are reshuffled.</b> Bullet and Bepu both measure a twist with an
    /// <c>atan2</c> of two dot products, but not of the same two, in the same order. Feeding a joint
    /// frame in unchanged would put the measured zero a quarter turn away from the pose the limits
    /// were authored around, so the basis re-labels the frame's columns to line the two formulas up;
    /// <see cref="Basis"/> spells out the mapping and how it was verified. Get this wrong and a knee
    /// bends the wrong way, which is why the shipped models are checked rather than the identity
    /// assumed.
    /// </para>
    /// <para>
    /// <b>A cone swing is an ellipse, and Bepu only has a circle.</b> Bullet allows a different swing
    /// half-angle along two perpendicular directions (<c>swingSpan1</c> and <c>swingSpan2</c>), while
    /// <see cref="SwingLimitConstraintComponent"/> constrains a single angle between two axes. The
    /// larger span is used, so the swing stays permissive rather than being clipped short; every
    /// shipped model has <c>swingSpan1 &gt;= swingSpan2</c>, so that is <c>swingSpan1</c>.
    /// </para>
    /// <para>
    /// <b>What a joint frame is measured from.</b> Bepu documents <c>LocalOffsetA</c> as the "offset
    /// from the center of body A", and that center is the one its compound builder computed - the
    /// weighted average of the collider's children, which lands where the legacy rigid body is. A
    /// bone body puts the bone at its origin instead, so the legacy offset is folded into the frame
    /// <i>and</i> that center is then taken back out. Folding alone leaves each side of a joint
    /// reaching for a point one rigid-body offset away, and the solver spends the whole simulation
    /// closing a gap that was never there - a ragdoll built that way gets thrown across the room
    /// instead of held together. Axes need no such correction: Bepu recenters children by a
    /// translation and never turns the body, so a direction is the same seen from either point.
    /// </para>
    /// </remarks>
    private static ConstraintComponentBase[]? TryCreateConstraint(
        GpdConstraint constraint,
        GpdRigidBody[] rigidBodies,
        BodyComponent[] bodies,
        GoldsrcVector3[] centersOfMass)
    {
        var bodyA = rigidBodies[constraint.BodyA];
        var bodyB = rigidBodies[constraint.BodyB];
        var centerA = centersOfMass[constraint.BodyA];
        var centerB = centersOfMass[constraint.BodyB];

        // The legacy frames are relative to the rigid bodies, whose offset is baked away: they are
        // re-expressed relative to the bones first, and then relative to the center a joint is
        // actually measured from.
        Matrix3x4.ConcatTransforms(bodyA.BoneToRigid, constraint.FrameInA, out var frameA);
        Matrix3x4.ConcatTransforms(bodyB.BoneToRigid, constraint.FrameInB, out var frameB);

        var localOffsetA = RelativeTo(frameA.Origin, centerA);
        var localOffsetB = RelativeTo(frameB.Origin, centerB);

        ConstraintComponentBase[] components;
        switch (constraint.Type)
        {
            case GpdConstraintType.Spherical:
                components = [CreateBallSocket(localOffsetA, localOffsetB)];
                break;

            case GpdConstraintType.Hinge:
                components =
                [
                    new HingeConstraintComponent
                    {
                        LocalOffsetA = localOffsetA,
                        LocalHingeAxisA = Axis(frameA, 2),
                        LocalOffsetB = localOffsetB,
                        LocalHingeAxisB = Axis(frameB, 2),
                    },
                    new TwistLimitConstraintComponent
                    {
                        // (fA1, fA0, -fA2): swapping X and Y and flipping Z is what makes Bepu's
                        // measured angle identical to Bullet's, so low/high carry over unchanged.
                        LocalBasisA = Basis(frameA, columnX: 1, columnY: 0, columnZ: 2, flipZ: true),
                        LocalBasisB = Basis(frameB, columnX: 1, columnY: 0, columnZ: 2, flipZ: true),
                        MinimumAngle = constraint.Low * DegreesToRadians,
                        MaximumAngle = constraint.High * DegreesToRadians,
                    },
                ];
                break;

            case GpdConstraintType.Cone:
                components =
                [
                    CreateBallSocket(localOffsetA, localOffsetB),
                    new SwingLimitConstraintComponent
                    {
                        // The cone axis is the frame's first basis column, the same one the twist
                        // turns about. swingSpan2 (the ellipse's other half-angle) is dropped - see
                        // the remarks.
                        AxisLocalA = Axis(frameA, 0),
                        AxisLocalB = Axis(frameB, 0),
                        MaximumSwingAngle = constraint.SwingSpan1 * DegreesToRadians,
                    },
                    new TwistLimitConstraintComponent
                    {
                        // (fA1, fA2, fA0): the twist axis stays in the Z column and the other two
                        // just follow it round. The range is symmetric, so no sign can be wrong.
                        LocalBasisA = Basis(frameA, columnX: 1, columnY: 2, columnZ: 0, flipZ: false),
                        LocalBasisB = Basis(frameB, columnX: 1, columnY: 2, columnZ: 0, flipZ: false),
                        MinimumAngle = -constraint.TwistSpan * DegreesToRadians,
                        MaximumAngle = constraint.TwistSpan * DegreesToRadians,
                    },
                ];
                break;

            default:
                return null;
        }

        foreach (var component in components)
        {
            // Every mapped joint relates exactly two bodies.
            var twoBody = (ITwoBody)component;
            twoBody.A = bodies[constraint.BodyA];
            twoBody.B = bodies[constraint.BodyB];
        }

        return components;
    }

    /// <summary>A ball socket holding the two given anchor points together.</summary>
    private static BallSocketConstraintComponent CreateBallSocket(StrideVector3 offsetA, StrideVector3 offsetB)
        => new()
        {
            LocalOffsetA = offsetA,
            LocalOffsetB = offsetB,
        };

    /// <summary>
    /// A rotation whose three axes are selected from the columns of a joint frame, for the Bepu
    /// constraints that measure an angle through a basis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The selection is not free. Bullet's hinge angle is
    /// <c>atan2(dot(swing, fA0), dot(swing, fA1))</c> with <c>swing</c> body B's <c>fB1</c>
    /// (<c>btHingeConstraint.cpp:649</c>), and Bepu's twist angle is
    /// <c>atan2(dot(alignedBX, basisA.Y), dot(alignedBX, basisA.X))</c> with <c>alignedBX</c> body B's
    /// basis X axis (<c>TwistServo.cs:117</c>). The two agree only when body A's basis is
    /// <c>(fA1, fA0, -fA2)</c> and body B's is <c>(fB1, fB0, -fB2)</c>. The Z axis is the one to
    /// flip, not X or Y: Bepu rotates body B's basis Z axis onto body A's before measuring, so the
    /// two Z axes have to point the same way at rest for the measured angle to sit at zero. Swapping
    /// X and Y and negating Z is the one such re-labelling that is still a proper rotation.
    /// </para>
    /// <para>
    /// This is checked rather than assumed - <c>artifacts/_verify_joint_basis.py</c> reproduces both
    /// formulas and sweeps every shipped model through a range of poses, confirming the mapping
    /// matches Bullet to zero and that the naive one is a quarter turn out.
    /// </para>
    /// </remarks>
    private static StrideQuaternion Basis(in Matrix3x4 frame, int columnX, int columnY, int columnZ, bool flipZ)
    {
        var x = Axis(frame, columnX);
        var y = Axis(frame, columnY);
        var z = Axis(frame, columnZ);
        if (flipZ)
            z = -z;

        // In Stride's row-vector convention the rows of a rotation are the images of X, Y and Z, so
        // the selected frame columns go in as rows - the same shape AlignCapsuleAxisToX builds.
        var basis = new StrideMatrix(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            z.X, z.Y, z.Z, 0,
            0, 0, 0, 1);

        basis.Decompose(out StrideVector3 _, out StrideQuaternion rotation, out StrideVector3 _);
        return StrideQuaternion.Normalize(rotation);
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

    /// <summary>
    /// A point given in bone local space, re-expressed relative to the bone's collider center of mass.
    /// </summary>
    /// <remarks>
    /// Translation only. The center Bepu reports after building a compound is a position, not a pose:
    /// the children are shifted by it and the body keeps its orientation, so a direction is unchanged
    /// by the move and only offsets - the things a joint frame is anchored by - are affected.
    /// </remarks>
    private static StrideVector3 RelativeTo(in GoldsrcVector3 boneLocal, in GoldsrcVector3 centerOfMass)
        => ToStride(new GoldsrcVector3(
            boneLocal.X - centerOfMass.X,
            boneLocal.Y - centerOfMass.Y,
            boneLocal.Z - centerOfMass.Z));

    // ── Bind pose ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Per studio bone, its T-pose transform relative to the model root.
    /// </summary>
    /// <remarks>
    /// The studio bone table stores each bone's default pose relative to its parent, so the transform
    /// relative to the model root is the product along the parent chain. Parents are followed with a
    /// cycle guard: a malformed hierarchy resolves to identity instead of hanging.
    /// <para>
    /// Reading it does not depend on a prefab: a ragdoll created without an entity to clone uses it
    /// to seed the anchors of the bones that get no rigid body - see
    /// <c>RagdollHelper.CreateRagdoll</c>.
    /// </para>
    /// </remarks>
    internal static unsafe Matrix3x4[] BuildBindPose(studiohdr_t* header, int boneCount)
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
