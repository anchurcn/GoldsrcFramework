namespace GoldsrcFramework.Ecs;

/// <summary>
/// The motion type of a physics skeleton, controlling how its bodies interact with the simulation.
/// Static skeletons are not represented here: they are expressed by the prefab itself (worldspawn
/// carries a <c>StaticComponent</c> and has no <see cref="PhysicsController"/>).
/// </summary>
public enum PhysicsMotionType
{
    /// <summary>Kinematic bodies, driven by GoldSrc animation through <see cref="PhysicsController.SetPose"/>.</summary>
    Kinematic,
    /// <summary>Dynamic bodies, driven by the physics simulation (ragdolls).</summary>
    Dynamic,
}
