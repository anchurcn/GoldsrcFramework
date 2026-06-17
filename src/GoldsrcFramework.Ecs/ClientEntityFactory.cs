using GoldsrcFramework.Ecs.Components;
using GoldsrcFramework.Engine.Native;
using Stride.Engine;

namespace GoldsrcFramework.Ecs;

public static unsafe class ClientEntityFactory
{
    public static Entity CreateEntity(cl_entity_t* nativeEntity, string? name = null)
    {
        var entity = string.IsNullOrWhiteSpace(name)
            ? new Entity()
            : new Entity(name);

        BindNativeEntity(entity, nativeEntity);
        return entity;
    }

    public static void BindNativeEntity(Entity entity, cl_entity_t* nativeEntity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.Components.Add(new ClEntityComponent(nativeEntity));
        entity.Components.Add(new ClTransformComponent(nativeEntity));
        entity.Components.Add(new ClModelComponent(nativeEntity));
    }
}
