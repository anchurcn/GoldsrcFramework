using System.Collections.Specialized;
using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

internal sealed class GoldsrcTransformSyncSystem : GameSystemBase
{
    private readonly GoldsrcSceneSystem sceneSystem;
    private readonly List<GoldsrcTransformLinkComponent> linkedComponents = [];

    public GoldsrcTransformSyncSystem(IServiceRegistry services)
        : base(services)
    {
        sceneSystem = services.GetService<GoldsrcSceneSystem>()
            ?? throw new InvalidOperationException("GoldsrcSceneSystem is not registered.");
        Enabled = true;
        UpdateOrder = -500;
        DrawOrder = 1_000;
    }

    public override void Initialize()
    {
        base.Initialize();
        foreach (var entity in sceneSystem.RootScene.Entities)
        {
            if (entity.Get<GoldsrcTransformLinkComponent>() is { } link)
                linkedComponents.Add(link);
        }

        sceneSystem.RootScene.Entities.CollectionChanged += OnEntitiesChanged;
    }

    private void OnEntitiesChanged(object? sender, TrackingCollectionChangedEventArgs e)
    {
        if (e.Item is not Entity entity)
            return;

        var component = entity.Get<GoldsrcTransformLinkComponent>();
        if (component is null)
            return;

        if (e.Action == NotifyCollectionChangedAction.Add)
            linkedComponents.Add(component);
        else if (e.Action == NotifyCollectionChangedAction.Remove)
            linkedComponents.Remove(component);
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var link in linkedComponents)
        {
            if (link.Authority != TransformAuthority.Goldsrc || !link.Binding.TryRead(out var origin, out var angles))
                continue;

            link.Entity.Transform.Position = origin;
            link.Entity.Transform.Rotation = GoldsrcTransformConverter.ToStrideRotation(angles);
            link.LastAngles = angles;
            link.HasLastAngles = true;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        foreach (var link in linkedComponents)
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