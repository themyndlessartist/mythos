namespace Mythos.Framework.Entities;

/// <summary>
/// Engine-independent persistent projection of minimal entity state.
/// </summary>
public sealed record EntitySnapshot
{
    public EntitySnapshot(
        EntityId id,
        EntityCategory category,
        EntityLifecycleState lifecycleState,
        IReadOnlyList<EntityTag>? tags,
        EntityId? parentId,
        EntityId? ownerId,
        EntityId? regionId,
        IReadOnlyList<ComponentTypeId>? componentTypes,
        long createdAt,
        long? retiredAt)
    {
        Id = id;
        Category = category;
        LifecycleState = lifecycleState;
        Tags = tags is null ? null : Array.AsReadOnly(tags.ToArray());
        ParentId = parentId;
        OwnerId = ownerId;
        RegionId = regionId;
        ComponentTypes = componentTypes is null ? null : Array.AsReadOnly(componentTypes.ToArray());
        CreatedAt = createdAt;
        RetiredAt = retiredAt;
    }

    public EntityId Id { get; }
    public EntityCategory Category { get; }
    public EntityLifecycleState LifecycleState { get; }
    public IReadOnlyList<EntityTag>? Tags { get; }
    public EntityId? ParentId { get; }
    public EntityId? OwnerId { get; }
    public EntityId? RegionId { get; }
    public IReadOnlyList<ComponentTypeId>? ComponentTypes { get; }
    public long CreatedAt { get; }
    public long? RetiredAt { get; }
}
