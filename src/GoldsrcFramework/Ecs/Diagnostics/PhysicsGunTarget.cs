using Stride.BepuPhysics;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace GoldsrcFramework.Ecs.Diagnostics;

/// <summary>
/// How the physics gun has to treat what the crosshair is pointing at.
/// </summary>
public enum PhysicsGunVerdict
{
    /// <summary>Nothing is under the crosshair.</summary>
    Nothing = 0,

    /// <summary>A dynamic body, which the gun may hold and freeze.</summary>
    Grabbable = 1,

    /// <summary>A body this gun froze earlier. The right button releases it again.</summary>
    Pinned = 2,

    /// <summary>World geometry - worldspawn's brush mesh. There is no body to drive.</summary>
    StaticWorld = 3,

    /// <summary>A kinematic body: the simulation does not own its pose, something else does.</summary>
    KinematicBody = 4,

    /// <summary>A dynamic body that belongs to a skeleton the animation drives.</summary>
    AnimatedSkeleton = 5,

    /// <summary>A dynamic body that no <see cref="PhysicsController"/> claims.</summary>
    UnownedBody = 6,
}

/// <summary>
/// What a pick ray found: the collidable it hit, the body behind it, the entity that owns the
/// physics, and whether the gun is allowed to hold it.
/// </summary>
/// <remarks>
/// <para>
/// The verdict is expressed in terms of what drives the body rather than in terms of model names or
/// prefab tags, because that is what decides whether an external force is welcome. A body the
/// simulation owns (<see cref="PhysicsMotionType.Dynamic"/>) can be pulled around; a body that
/// belongs to an animation-driven skeleton cannot, and neither can world geometry.
/// </para>
/// <para>
/// The second dynamic-body test is not redundancy. A jiggle (addon) bone is
/// <c>Kinematic == false</c> even inside a skeleton the animation drives, and a constraint attached
/// to one would be cloned along with the bones when that character dies, dragging the corpse
/// towards whatever the gun was aimed at. Requiring a dynamic skeleton keeps the gun on objects the
/// simulation actually owns.
/// </para>
/// </remarks>
public readonly struct PhysicsGunTarget
{
    private PhysicsGunTarget(
        PhysicsGunVerdict verdict,
        CollidableComponent? collidable,
        BodyComponent? body,
        Entity? owner,
        Vector3 point,
        float distance)
    {
        Verdict = verdict;
        Collidable = collidable;
        Body = body;
        Owner = owner;
        Point = point;
        Distance = distance;
    }

    /// <summary>No hit at all; every other member is meaningless.</summary>
    public static PhysicsGunTarget None => default;

    public PhysicsGunVerdict Verdict { get; }

    /// <summary>The collidable the ray hit, or null when nothing was hit.</summary>
    public CollidableComponent? Collidable { get; }

    /// <summary>The body behind the hit, or null when the hit was not a body.</summary>
    public BodyComponent? Body { get; }

    /// <summary>The entity carrying the <see cref="PhysicsController"/>, or null when there is none.</summary>
    public Entity? Owner { get; }

    /// <summary>Where the ray met the surface, in world space.</summary>
    public Vector3 Point { get; }

    /// <summary>Distance along the ray, from the eye to <see cref="Point"/>.</summary>
    public float Distance { get; }

    /// <summary>True when something was hit at all.</summary>
    public bool HasHit => Verdict != PhysicsGunVerdict.Nothing;

    /// <summary>True when the gun may start holding this.</summary>
    public bool CanHold => Verdict == PhysicsGunVerdict.Grabbable;

    /// <summary>
    /// Reads a ray hit and decides what the gun may do with it.
    /// </summary>
    /// <param name="hit">The hit as the simulation reported it.</param>
    /// <param name="isPinned">Whether this gun already froze the collidable that was hit.</param>
    public static PhysicsGunTarget Classify(in HitInfo hit, bool isPinned)
        => Classify(hit.Collidable, hit.Point, hit.Distance, isPinned);

    /// <summary>
    /// Reads a hit and decides what the gun may do with it.
    /// </summary>
    /// <param name="collidable">The collidable that was hit.</param>
    /// <param name="point">Where the ray met the surface.</param>
    /// <param name="distance">Distance along the ray, from the eye to <paramref name="point"/>.</param>
    /// <param name="isPinned">Whether this gun already froze <paramref name="collidable"/>.</param>
    public static PhysicsGunTarget Classify(CollidableComponent collidable, Vector3 point, float distance, bool isPinned)
    {
        // A pinned body is kinematic, so it has to be recognised before the dynamic test below.
        if (isPinned)
            return new PhysicsGunTarget(PhysicsGunVerdict.Pinned, collidable, collidable as BodyComponent, FindOwner(collidable.Entity), point, distance);

        if (collidable is not BodyComponent body)
            return new PhysicsGunTarget(PhysicsGunVerdict.StaticWorld, collidable, null, null, point, distance);

        var owner = FindOwner(body.Entity);

        if (body.Kinematic)
            return new PhysicsGunTarget(PhysicsGunVerdict.KinematicBody, collidable, body, owner, point, distance);

        if (owner?.Get<PhysicsController>() is not { } controller)
            return new PhysicsGunTarget(PhysicsGunVerdict.UnownedBody, collidable, body, owner, point, distance);

        if (controller.MotionType != PhysicsMotionType.Dynamic)
            return new PhysicsGunTarget(PhysicsGunVerdict.AnimatedSkeleton, collidable, body, owner, point, distance);

        return new PhysicsGunTarget(PhysicsGunVerdict.Grabbable, collidable, body, owner, point, distance);
    }

    /// <summary>
    /// The nearest entity at or above <paramref name="from"/> that carries a
    /// <see cref="PhysicsController"/>, which is the entity that owns the body's physics.
    /// </summary>
    /// <remarks>
    /// Physics bones are children of the entity the controller sits on, so the walk is one step for
    /// everything the framework builds today. It is written as a bounded walk rather than a single
    /// parent hop so that a skeleton nested under an intermediate entity still resolves, and so that
    /// a malformed or cyclic hierarchy cannot hang the caller.
    /// </remarks>
    private static Entity? FindOwner(Entity? from)
    {
        for (var steps = 0; from is not null && steps < 8; steps++)
        {
            if (from.Get<PhysicsController>() is not null)
                return from;

            from = from.Transform.Parent?.Entity;
        }

        return null;
    }
}
