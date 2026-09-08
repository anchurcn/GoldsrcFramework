using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace GoldsrcFramework.Ecs;

/// <summary>
/// Manages the physics bodies attached to a GoldSrc entity. Placed on the root entity
/// (not a prefab) and created at runtime by HalfLifeBehavior.
/// </summary>
public sealed class PhysicsController : EntityComponent
{
    /// <summary>
    /// Child entities representing physics bones. Usually one element for brush models,
    /// multiple for studio models.
    /// </summary>
    public List<Entity> PhysicsBones { get; } = [];

    /// <summary>
    /// Current motion state of all physics bones.
    /// </summary>
    public PhysicsMotionType MotionType { get; set; } = PhysicsMotionType.Kinematic;

    /// <summary>
    /// The bone entity used as the pivot for computing the model origin from physics transforms.
    /// </summary>
    public Entity? PivotPhysicsBone { get; set; }

    /// <summary>
    /// Offset from the pivot physics bone's transform to the model origin.
    /// Computed in T-pose during initialization.
    /// </summary>
    public Matrix ModelRootOffset { get; set; } = Matrix.Identity;

    /// <summary>
    /// The model path this controller was created for. Used for validation.
    /// </summary>
    public string? ModelPath { get; private set; }

    /// <summary>
    /// Creates default physics bones with a placeholder Box collider.
    /// This will be replaced by real GPD-based creation later.
    /// </summary>
    public static PhysicsController CreateDefault(string modelPath, Entity rootEntity)
    {
        var controller = new PhysicsController { ModelPath = modelPath };

        var collider = new CompoundCollider();
        collider.Colliders.Add(new BoxCollider { Size = new Vector3(16, 16, 72) });

        var body = new BodyComponent
        {
            Collider = collider,
            Kinematic = true,
        };

        var boneEntity = new Entity("PhysicsBone_0");
        boneEntity.Transform.Position = Vector3.Zero;
        boneEntity.Components.Add(body);
        rootEntity.AddChild(boneEntity);

        controller.PhysicsBones.Add(boneEntity);
        controller.PivotPhysicsBone = boneEntity;
        controller.ModelRootOffset = Matrix.Identity;

        return controller;
    }

    /// <summary>
    /// Syncs GoldSrc bone matrices to physics bones via Teleport.
    /// <paramref name="pose"/> contains bone world-space matrices.
    /// For brush models, the single bone maps to pose[0].
    /// For studio models, each PhysicsNode maps to its corresponding bone index.
    /// <paramref name="settingMask"/> can disable specific bones (e.g. jiggle bones).
    /// </summary>
    public void SetPose(ReadOnlySpan<Matrix> pose, bool[]? settingMask = null)
    {
        for (var i = 0; i < PhysicsBones.Count && i < pose.Length; i++)
        {
            if (settingMask is not null && i < settingMask.Length && !settingMask[i])
                continue;

            var bone = PhysicsBones[i];
            var matrix = pose[i];
            matrix.Decompose(out Vector3 _, out Quaternion rotation, out Vector3 translation);

            if (bone.Get<BodyComponent>() is { } body)
            {
                body.Teleport(translation, rotation);
            }
            else if (bone.Get<StaticComponent>() is { } staticBody)
            {
                staticBody.Teleport(translation, rotation);
            }
            else
            {
                bone.Transform.Position = translation;
                bone.Transform.Rotation = rotation;
            }
        }
    }

    /// <summary>
    /// Reads physics transforms back into <paramref name="pose"/>.
    /// pose[0] is computed as the model origin from <see cref="PivotPhysicsBone"/> × <see cref="ModelRootOffset"/>.
    /// pose[1..n] are the world matrices of each physics bone.
    /// <paramref name="settingMask"/> can disable specific bones.
    /// </summary>
    public void GetPose(Span<Matrix> pose, bool[]? settingMask = null)
    {
        // pose[0] = model origin from pivot bone
        if (pose.Length > 0 && PivotPhysicsBone is { } pivot)
        {
            if (settingMask is null || settingMask.Length == 0 || settingMask[0])
            {
                var pivotWorld = pivot.Transform.WorldMatrix;
                pose[0] = pivotWorld * ModelRootOffset;
            }
        }

        // pose[1..n] = each physics bone's world transform
        for (var i = 0; i < PhysicsBones.Count && i + 1 < pose.Length; i++)
        {
            if (settingMask is not null && i + 1 < settingMask.Length && !settingMask[i + 1])
                continue;

            pose[i + 1] = PhysicsBones[i].Transform.WorldMatrix;
        }
    }

    /// <summary>
    /// Enables physics (applies current MotionType to all bones).
    /// </summary>
    public void Enable()
    {
        ApplyMotionType();
    }

    /// <summary>
    /// Disables physics on all bones. The bones remain in the scene but are set to kinematic.
    /// </summary>
    public void Disable()
    {
        foreach (var bone in PhysicsBones)
        {
            if (bone.Get<BodyComponent>() is { } body)
                body.Kinematic = true;
        }
    }

    /// <summary>
    /// Returns true if the model path matches the current controller. Returns false if
    /// the controller needs to be recreated for a different model.
    /// </summary>
    public bool ValidateModel(string modelPath)
    {
        return string.Equals(ModelPath, modelPath, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyMotionType()
    {
        foreach (var bone in PhysicsBones)
        {
            if (bone.Get<BodyComponent>() is not { } body)
                continue;

            body.Kinematic = MotionType switch
            {
                PhysicsMotionType.Kinematic => true,
                PhysicsMotionType.Dynamic => false,
                _ => true,
            };
        }
    }
}