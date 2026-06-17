using GoldsrcFramework.Ecs.Components;
using GoldsrcFramework.Ecs.Scripting;
using Stride.Games;

namespace GoldsrcFramework.Demo;

internal sealed class SpinTempEntityScript : ClScript
{
    private ClTransformComponent? transform;
    private float degreesPerSecond;

    public SpinTempEntityScript()
    {
    }

    public SpinTempEntityScript(ClTransformComponent transform, float degreesPerSecond)
    {
        this.transform = transform;
        this.degreesPerSecond = degreesPerSecond;
    }

    public override void Update(GameTime time)
    {
        if (transform == null)
            return;

        var angles = transform.Angles;
        angles.Y = (angles.Y + degreesPerSecond * (float)time.Elapsed.TotalSeconds) % 360.0f;
        transform.Angles = angles;
    }
}
