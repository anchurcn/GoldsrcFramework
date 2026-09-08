namespace GoldsrcFramework.Ecs;

/// <summary>
/// Called by <see cref="LateUpdateProcessor"/> after physics simulation and transform output
/// have completed for the frame. Use this to read final physics poses and update derived data.
/// </summary>
public interface ILateUpdate
{
    void LateUpdate();
}