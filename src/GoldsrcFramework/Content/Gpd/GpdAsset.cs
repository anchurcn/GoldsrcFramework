using GoldsrcFramework.LinearMath;

namespace GoldsrcFramework.Content.Gpd;

/// <summary>
/// Sub-shape kinds the legacy <c>.gpd</c> (<c>sub-collision-shape type="..."</c>) can describe.
/// </summary>
public enum GpdShapeType
{
    /// <summary><c>primitive.box</c> - a box given by its half extent.</summary>
    Box,

    /// <summary>
    /// <c>primitive.capsule</c> - a capsule given by a radius and its total end-to-end height.
    /// </summary>
    Capsule,

    /// <summary>
    /// <c>primitive.other</c> - known to the format but not representable. A shape made of one of
    /// these makes the whole asset invalid.
    /// </summary>
    Other,
}

/// <summary>
/// Role of a rigid body, mirroring gsphysics <c>UserRigidbodyType</c>.
/// </summary>
public enum GpdRigidBodyType
{
    /// <summary>A regular body, driven by the animation while the entity is alive.</summary>
    None = 0,

    /// <summary>
    /// A jiggle body (ties, hair, antennae). It stays dynamic even in kinematic mode so the
    /// neighbouring animated bodies can drag it around. Maps to <c>BoneLink.IsAddon</c>.
    /// </summary>
    Addon = 1,
}

/// <summary>
/// Constraint kinds, mirroring the Bullet types gsphysics maps them to.
/// </summary>
public enum GpdConstraintType
{
    /// <summary>Bullet <c>btPoint2PointConstraint</c> - a ball and socket joint.</summary>
    Spherical,

    /// <summary>Bullet <c>btHingeConstraint</c> - one rotational degree of freedom.</summary>
    Hinge,

    /// <summary>Bullet <c>btConeTwistConstraint</c> - a swing cone plus a twist span.</summary>
    Cone,
}

/// <summary>
/// One <c>sub-collision-shape</c> of a <c>collision-shape</c>, in physics-backend-neutral form.
/// </summary>
/// <remarks>
/// The legacy format allows a collision shape to be a compound of several sub-shapes, each with its
/// own local pose inside the shape. The shipped GoldSrc models only ever use a single, unposed
/// sub-shape per shape, but both are represented here so the mapping stays faithful.
/// </remarks>
public sealed record GpdSubShape
{
    /// <summary>Kind of this sub-shape.</summary>
    public required GpdShapeType ShapeType { get; init; }

    /// <summary>
    /// Half extent of a <see cref="GpdShapeType.Box"/>, in GoldSrc units. Unused by capsules.
    /// </summary>
    public GoldsrcFramework.LinearMath.Vector3 HalfExtent { get; init; }

    /// <summary>Radius of a <see cref="GpdShapeType.Capsule"/>, in GoldSrc units.</summary>
    public float Radius { get; init; }

    /// <summary>
    /// Length of the cylinder section of a <see cref="GpdShapeType.Capsule"/>, in GoldSrc units.
    /// The legacy <c>height</c> is the total end-to-end height, so this is
    /// <c>height - 2 * radius</c> and is always positive.
    /// </summary>
    public float CylinderLength { get; init; }

    /// <summary>
    /// Pose of this sub-shape inside its collision shape, i.e. relative to the rigid body. Identity
    /// when the element carries no <c>local</c>.
    /// </summary>
    public Matrix3x4 LocalTransform { get; init; } = Matrix3x4.Identity;
}

/// <summary>
/// One <c>collision-shape</c>: the collision geometry a rigid body refers to by index.
/// </summary>
/// <param name="SubShapes">The sub-shapes that make up this shape; never empty when valid.</param>
public sealed record GpdShape(GpdSubShape[] SubShapes);

/// <summary>
/// One <c>rigidbody</c>: a simulated bone of the physics skeleton.
/// </summary>
/// <param name="BoneIndex">Index of the studio bone this body is attached to.</param>
/// <param name="ShapeIndex">Index into <see cref="GpdAsset.Shapes"/>.</param>
/// <param name="Type">Whether this is a regular or a jiggle (addon) body.</param>
/// <param name="BoneToRigid">
/// Pose of the rigid body in the bone's local space (<c>offset</c> in the legacy file), so that
/// <c>rigidWorld = boneWorld * BoneToRigid</c>. This is the same relation gsphysics implements in
/// <c>BoneMotionState::getWorldTransform</c>.
/// </param>
public sealed record GpdRigidBody(
    int BoneIndex,
    int ShapeIndex,
    GpdRigidBodyType Type,
    Matrix3x4 BoneToRigid);

/// <summary>
/// One <c>constraint</c> between two rigid bodies.
/// </summary>
/// <param name="BodyA">Index into <see cref="GpdAsset.RigidBodies"/> of the first body.</param>
/// <param name="BodyB">Index into <see cref="GpdAsset.RigidBodies"/> of the second body.</param>
/// <param name="Type">Kind of joint.</param>
/// <param name="FrameInA">
/// The joint frame relative to the <b>rigid body</b> A, not to the bone. When the rigid-body offset
/// is baked away (see <see cref="StudioPhysicsPrefabFactory"/>) this frame has to be re-expressed
/// relative to the bone first.
/// </param>
/// <param name="FrameInB">The joint frame relative to the rigid body B.</param>
/// <param name="Low">Hinge lower angular limit, in degrees.</param>
/// <param name="High">Hinge upper angular limit, in degrees.</param>
/// <param name="TwistSpan">Cone twist span, in degrees.</param>
/// <param name="SwingSpan1">Cone swing span around the first axis, in degrees.</param>
/// <param name="SwingSpan2">Cone swing span around the second axis, in degrees.</param>
/// <param name="IsAddon">
/// True when either end is an addon body. Addon joints stay enabled while the skeleton is
/// kinematic so the jiggle bodies keep being dragged along.
/// </param>
public sealed record GpdConstraint(
    int BodyA,
    int BodyB,
    GpdConstraintType Type,
    Matrix3x4 FrameInA,
    Matrix3x4 FrameInB,
    float Low,
    float High,
    float TwistSpan,
    float SwingSpan1,
    float SwingSpan2,
    bool IsAddon);

/// <summary>
/// A validated legacy <c>.gpd</c> document - the physics construction info of one studio model.
/// </summary>
/// <remarks>
/// <para>
/// Immutable and physics-backend-neutral: parsing never creates Stride entities, colliders or
/// bodies, so a malformed asset fails as an asset instead of leaving partial scene state behind.
/// </para>
/// <para>
/// Instances may be shared by any number of actors; the mutable runtime objects built from them are
/// always instance-owned.
/// </para>
/// </remarks>
public sealed class GpdAsset
{
    /// <summary>
    /// The <c>checksum</c> attribute of the document: the SHA-1 of the studio model header the file
    /// was authored against. See <see cref="GpdFile"/> for what exactly is hashed.
    /// </summary>
    public required string Checksum { get; init; }

    /// <summary>Collision shapes, indexed by <see cref="GpdRigidBody.ShapeIndex"/>.</summary>
    public required GpdShape[] Shapes { get; init; }

    /// <summary>Rigid bodies, i.e. the simulated bones of the skeleton.</summary>
    public required GpdRigidBody[] RigidBodies { get; init; }

    /// <summary>Joints between the rigid bodies.</summary>
    public required GpdConstraint[] Constraints { get; init; }
}
