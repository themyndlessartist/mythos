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

    public bool IsValidEntity(EntityId id) => registry.Exists(id);

    public bool IsValidRegion(EntityId id)
    {
        var result = registry.Find(id);
        return result.IsSuccess && result.Value!.Category == RegionCategory;
    }
}

public sealed class PermissiveEventReferenceValidator : IEventReferenceValidator
{
    public bool IsValidEntity(EntityId id) => id.Value != Guid.Empty;

    public bool IsValidRegion(EntityId id) => id.Value != Guid.Empty;
}
