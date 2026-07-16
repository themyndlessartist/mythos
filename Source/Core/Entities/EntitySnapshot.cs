namespace Mythos.Framework.Entities;

/// <summary>
/// Engine-independent persistent projection of minimal entity state.
/// </summary>
public sealed record EntitySnapshot(
    EntityId Id,
    EntityCategory Category,
    EntityLifecycleState LifecycleState,
    IReadOnlyList<EntityTag> Tags,
    EntityId? ParentId,
    EntityId? OwnerId,
    EntityId? RegionId,
    IReadOnlyList<ComponentTypeId> ComponentTypes,
    long CreatedAt,
    long? RetiredAt);
