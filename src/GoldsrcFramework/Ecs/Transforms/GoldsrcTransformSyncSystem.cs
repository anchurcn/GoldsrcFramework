using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;

namespace GoldsrcFramework.Ecs;

internal sealed class GoldsrcTransformSyncSystem : GameSystemBase
{
    public GoldsrcTransformSyncSystem(IServiceRegistry services)
        : base(services)
    {
        Enabled = true;
        Visible = true;
        UpdateOrder = -500;
        DrawOrder = 1_000;
    }

    /// <summary>
    /// The links to sync, as the processor that holds them sees them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A view, not a copy: the processor hands its own component table over in <c>OnSystemAdd</c>, so
    /// what is read here is exactly the set the entity manager keeps in step as links enter and leave,
    /// and there is no second list to keep in step of its own.
    /// </para>
    /// <para>
    /// The processor attaches it rather than this looking the processor up on every pass, because a
    /// lookup that finds nothing cannot be told apart from a scene that holds no links - and those are
    /// different problems: one is wiring, the other an empty scene. Empty until the first link has been
    /// in the scene, which is also the first moment there is anything to sync.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<GoldsrcTransformLinkComponent> Links { get; internal set; }
        = Array.Empty<GoldsrcTransformLinkComponent>();

    public override void Update(GameTime gameTime)
    {
        foreach (var link in Links)
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
        foreach (var link in Links)
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
