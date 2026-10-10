using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

internal sealed class GoldsrcTransformSyncSystem : GameSystemBase
{
    private readonly GoldsrcSceneSystem sceneSystem;

    public GoldsrcTransformSyncSystem(IServiceRegistry services)
        : base(services)
    {
        sceneSystem = services.GetService<GoldsrcSceneSystem>()
            ?? throw new InvalidOperationException("GoldsrcSceneSystem is not registered.");
        Enabled = true;
        UpdateOrder = -500;
        DrawOrder = 1_000;
    }

    public override void Update(GameTime gameTime)
    {
        // The set of links belongs to the scene: the processor owns it and the entity manager keeps it in
        // step as links are attached and detached. Until a link has been in the scene there is no
        // processor at all - and nothing to read either.
        if (sceneSystem.SceneInstance.GetProcessor<GoldsrcTransformSyncProcessor>() is not { } processor)
            return;

        foreach (var link in processor.Links)
        {
            if (link.Authority != TransformAuthority.Goldsrc || !link.Binding.TryRead(out var origin, out var angles))
                continue;

            link.Entity.Transform.Position = origin;
            link.Entity.Transform.Rotation = GoldsrcTransformConverter.ToStrideRotation(angles);
            link.Entity.Transform.UpdateWorldMatrix(); // It will first call UpdateLocalMatrix on self, and because this is root entity (no parent), the local matrix is equals to world matrix.        
            link.LastAngles = angles;
            link.HasLastAngles = true;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (sceneSystem.SceneInstance.GetProcessor<GoldsrcTransformSyncProcessor>() is not { } processor)
            return;

        foreach (var link in processor.Links)
        {
            if (link.Authority != TransformAuthority.Stride)
                continue;

            var transform = link.Entity.Transform;
            if (!transform.WorldMatrix.Decompose(
                    out Vector3 _,
                    out Quaternion rotation,
                    out Vector3 position))
                continue;
                    
            Vector3? previousAngles = link.HasLastAngles ? link.LastAngles : null;
            var angles = GoldsrcTransformConverter.ToGoldsrcAngles(rotation, previousAngles);
            if (!link.Binding.TryWrite(position, angles))
                continue;

            link.LastAngles = angles;
            link.HasLastAngles = true;
        }
    }
}
