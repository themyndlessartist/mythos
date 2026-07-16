using Mythos.Framework.Entities;

namespace Mythos.Framework.Events;

public interface IEventReferenceValidator
{
    bool IsValidEntity(EntityId id);
    bool IsValidRegion(EntityId id);
}

public sealed class EntityRegistryEventReferenceValidator(EntityRegistry registry) : IEventReferenceValidator
{
    private static readonly EntityCategory RegionCategory = new("Region");

    public bool IsValidEntity(EntityId id)
    {
        var result = registry.Find(id);
        return result.IsSuccess && result.Value!.LifecycleState is EntityLifecycleState.Active or EntityLifecycleState.Inactive;
    }

    public bool IsValidRegion(EntityId id)
    {
        var result = registry.Find(id);
        return result.IsSuccess &&
            result.Value!.Category == RegionCategory &&
            result.Value.LifecycleState is EntityLifecycleState.Active or EntityLifecycleState.Inactive;
    }
}

public sealed class RejectingEventReferenceValidator : IEventReferenceValidator
{
    public bool IsValidEntity(EntityId id) => false;

    public bool IsValidRegion(EntityId id) => false;
}
