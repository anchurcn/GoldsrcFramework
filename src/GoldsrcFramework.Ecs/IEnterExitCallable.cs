namespace GoldsrcFramework.Ecs;

/// <summary>
/// Notified by <see cref="ClientSceneManagementSystem"/> when an entity enters or leaves the
/// client's visible scene. Use this to enable/disable physics or other per-entity subsystems.
/// </summary>
public interface IEnterExitCallable
{
    void OnEnter();
    void OnExit();
}