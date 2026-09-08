namespace GoldsrcFramework.Ecs;

/// <summary>
/// The motion type of a physics body, controlling how it interacts with the simulation.
/// </summary>
public enum PhysicsMotionType
{
    /// <summary>Static body, never moves.</summary>
    Static,
    /// <summary>Kinematic body, driven by GoldSrc transforms.</summary>
    Kinematic,
    /// <summary>Dynamic body, driven by the physics simulation.</summary>
    Dynamic,
}