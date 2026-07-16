namespace Mythos.Framework.Entities;

/// <summary>
/// Owns minimal persistent identity, lifecycle, and relationship metadata.
/// </summary>
public sealed class EntityRegistry
{
    private readonly Dictionary<EntityId, EntityState> entities = [];
    private readonly IEntityIdGenerator idGenerator;

    public EntityRegistry(IEntityIdGenerator? idGenerator = null)
    {
        this.idGenerator = idGenerator ?? new Version7EntityIdGenerator();
    }

    public int Count => entities.Count;

    public EntityResult<EntitySnapshot> Create(EntityCategory category, long createdAt)
    {
        if (createdAt < 0)
        {
            return EntityResult<EntitySnapshot>.Failure(
                EntityErrorCodes.InvalidTimestamp,
                "Creation timestamp cannot be negative.");
        }

        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = idGenerator.Create();
            if (id.Value == Guid.Empty || entities.ContainsKey(id))
            {
                continue;
            }

            var state = new EntityState(id, category, createdAt);
            entities.Add(id, state);
            return EntityResult<EntitySnapshot>.Success(state.ToSnapshot());
        }

        return EntityResult<EntitySnapshot>.Failure(
            EntityErrorCodes.DuplicateId,
            "The entity ID generator did not produce a unique, valid ID.");
    }

    public EntityResult<EntitySnapshot> Register(EntitySnapshot snapshot)
    {
        if (snapshot.Id.Value == Guid.Empty)
        {
            return EntityResult<EntitySnapshot>.Failure(EntityErrorCodes.InvalidId, "Entity ID cannot be empty.");
        }

        if (entities.ContainsKey(snapshot.Id))
        {
            return EntityResult<EntitySnapshot>.Failure(
                EntityErrorCodes.DuplicateId,
                $"Entity '{snapshot.Id}' is already registered.");
        }

        var validation = ValidateSnapshotReferences(snapshot);
        if (!validation.IsSuccess)
        {
            return EntityResult<EntitySnapshot>.Failure(validation.Error!.Code, validation.Error.Message);
        }

        var state = EntityState.FromSnapshot(snapshot);
        entities.Add(state.Id, state);

        if (HasCycle(state.Id, static item => item.ParentId) ||
            HasCycle(state.Id, static item => item.OwnerId))
        {
            entities.Remove(state.Id);
            return EntityResult<EntitySnapshot>.Failure(
                EntityErrorCodes.InvalidReference,
                "Registered relationships would create a cycle.");
        }

        return EntityResult<EntitySnapshot>.Success(state.ToSnapshot());
    }

    public bool Exists(EntityId id) => entities.ContainsKey(id);

    public bool IsActive(EntityId id) =>
        entities.TryGetValue(id, out var state) && state.LifecycleState == EntityLifecycleState.Active;

    public EntityResult<EntitySnapshot> Find(EntityId id) =>
        entities.TryGetValue(id, out var state)
            ? EntityResult<EntitySnapshot>.Success(state.ToSnapshot())
            : EntityResult<EntitySnapshot>.Failure(EntityErrorCodes.NotFound, $"Entity '{id}' was not found.");

    public IReadOnlyList<EntitySnapshot> QueryByCategory(EntityCategory category) =>
        SnapshotWhere(state => state.Category == category);

    public IReadOnlyList<EntitySnapshot> QueryByTag(EntityTag tag) =>
        SnapshotWhere(state => state.Tags.Contains(tag));

    public IReadOnlyList<EntitySnapshot> EnumerateChildren(EntityId parentId) =>
        SnapshotWhere(state => state.ParentId == parentId);

    public IReadOnlyList<EntitySnapshot> EnumerateOwned(EntityId ownerId) =>
        SnapshotWhere(state => state.OwnerId == ownerId);

    public IReadOnlyList<EntitySnapshot> EnumerateRegion(EntityId regionId) =>
        SnapshotWhere(state => state.RegionId == regionId);

    public EntityResult AddTag(EntityId id, EntityTag tag) =>
        WithEntity(id, state =>
        {
            state.Tags.Add(tag);
            return EntityResult.Success();
        });

    public EntityResult RemoveTag(EntityId id, EntityTag tag) =>
        WithEntity(id, state =>
        {
            state.Tags.Remove(tag);
            return EntityResult.Success();
        });

    public EntityResult RegisterComponent(EntityId id, ComponentTypeId componentType) =>
        WithEntity(id, state =>
        {
            state.ComponentTypes.Add(componentType);
            return EntityResult.Success();
        });

    public EntityResult RemoveComponent(EntityId id, ComponentTypeId componentType) =>
        WithEntity(id, state =>
        {
            state.ComponentTypes.Remove(componentType);
            return EntityResult.Success();
        });

    public EntityResult AssignParent(EntityId id, EntityId? parentId) =>
        AssignRelationship(id, parentId, true);

    public EntityResult AssignOwner(EntityId id, EntityId? ownerId) =>
        AssignRelationship(id, ownerId, false);

    public EntityResult AssignRegion(EntityId id, EntityId? regionId)
    {
        if (!entities.TryGetValue(id, out var state))
        {
            return NotFound(id);
        }

        if (regionId is { } target)
        {
            if (target == id)
            {
                return EntityResult.Failure(EntityErrorCodes.SelfReference, "An entity cannot be its own region.");
            }

            if (!entities.TryGetValue(target, out var region) || region.Category != new EntityCategory("Region"))
            {
                return EntityResult.Failure(
                    EntityErrorCodes.InvalidReference,
                    $"Region '{target}' is missing or is not categorized as a Region.");
            }
        }

        state.RegionId = regionId;
        return EntityResult.Success();
    }

    public EntityResult ChangeLifecycle(EntityId id, EntityLifecycleState nextState, long timestamp)
    {
        if (!entities.TryGetValue(id, out var state))
        {
            return NotFound(id);
        }

        if (timestamp < state.CreatedAt)
        {
            return EntityResult.Failure(
                EntityErrorCodes.InvalidTimestamp,
                "Lifecycle timestamp cannot precede entity creation.");
        }

        if (!CanTransition(state.LifecycleState, nextState))
        {
            return EntityResult.Failure(
                EntityErrorCodes.InvalidLifecycleTransition,
                $"Cannot transition from {state.LifecycleState} to {nextState}.");
        }

        state.LifecycleState = nextState;
        state.RetiredAt = nextState is EntityLifecycleState.Retired or EntityLifecycleState.Destroyed
            ? timestamp
            : null;

        return EntityResult.Success();
    }

    public EntityResult Retire(EntityId id, long timestamp) =>
        ChangeLifecycle(id, EntityLifecycleState.Retired, timestamp);

    public EntityResult Destroy(EntityId id, long timestamp) =>
        ChangeLifecycle(id, EntityLifecycleState.Destroyed, timestamp);

    public IReadOnlyList<EntitySnapshot> ExportSnapshots() =>
        entities.Values
            .OrderBy(state => state.Id.Value)
            .Select(state => state.ToSnapshot())
            .ToArray();

    private EntityResult AssignRelationship(EntityId id, EntityId? relatedId, bool hierarchy)
    {
        if (!entities.TryGetValue(id, out var state))
        {
            return NotFound(id);
        }

        if (relatedId is { } target)
        {
            if (target == id)
            {
                return EntityResult.Failure(EntityErrorCodes.SelfReference, "An entity cannot reference itself.");
            }

            if (!entities.ContainsKey(target))
            {
                return EntityResult.Failure(EntityErrorCodes.InvalidReference, $"Entity '{target}' was not found.");
            }
        }

        var previous = hierarchy ? state.ParentId : state.OwnerId;
        if (hierarchy)
        {
            state.ParentId = relatedId;
        }
        else
        {
            state.OwnerId = relatedId;
        }

        var hasCycle = HasCycle(state.Id, hierarchy ? static item => item.ParentId : static item => item.OwnerId);
        if (hasCycle)
        {
            if (hierarchy)
            {
                state.ParentId = previous;
            }
            else
            {
                state.OwnerId = previous;
            }

            return EntityResult.Failure(
                hierarchy ? EntityErrorCodes.HierarchyCycle : EntityErrorCodes.OwnershipCycle,
                hierarchy ? "Parent assignment would create a hierarchy cycle." : "Owner assignment would create an ownership cycle.");
        }

        return EntityResult.Success();
    }

    private EntityResult ValidateSnapshotReferences(EntitySnapshot snapshot)
    {
        if (snapshot.CreatedAt < 0 || snapshot.RetiredAt < snapshot.CreatedAt)
        {
            return EntityResult.Failure(EntityErrorCodes.InvalidTimestamp, "Snapshot timestamps are invalid.");
        }

        if (snapshot.LifecycleState is EntityLifecycleState.Retired or EntityLifecycleState.Destroyed &&
            snapshot.RetiredAt is null)
        {
            return EntityResult.Failure(EntityErrorCodes.InvalidTimestamp, "Terminal entities require a retirement timestamp.");
        }

        if (snapshot.LifecycleState is EntityLifecycleState.Active or EntityLifecycleState.Inactive &&
            snapshot.RetiredAt is not null)
        {
            return EntityResult.Failure(EntityErrorCodes.InvalidTimestamp, "Non-terminal entities cannot have a retirement timestamp.");
        }

        foreach (var reference in new[] { snapshot.ParentId, snapshot.OwnerId, snapshot.RegionId })
        {
            if (reference is { } referencedId && referencedId == snapshot.Id)
            {
                return EntityResult.Failure(EntityErrorCodes.SelfReference, "Entity snapshots cannot reference themselves.");
            }

            if (reference is { } missingId && !entities.ContainsKey(missingId))
            {
                return EntityResult.Failure(EntityErrorCodes.InvalidReference, $"Referenced entity '{missingId}' was not found.");
            }
        }

        if (snapshot.RegionId is { } regionId && entities[regionId].Category != new EntityCategory("Region"))
        {
            return EntityResult.Failure(EntityErrorCodes.InvalidReference, "Region reference is not categorized as a Region.");
        }

        return EntityResult.Success();
    }

    private bool HasCycle(EntityId startingId, Func<EntityState, EntityId?> selectNext)
    {
        var visited = new HashSet<EntityId>();
        var currentId = startingId;

        while (entities.TryGetValue(currentId, out var current))
        {
            if (!visited.Add(currentId))
            {
                return true;
            }

            var next = selectNext(current);
            if (next is null)
            {
                return false;
            }

            currentId = next.Value;
        }

        return false;
    }

    private IReadOnlyList<EntitySnapshot> SnapshotWhere(Func<EntityState, bool> predicate) =>
        entities.Values
            .Where(predicate)
            .OrderBy(state => state.Id.Value)
            .Select(state => state.ToSnapshot())
            .ToArray();

    private EntityResult WithEntity(EntityId id, Func<EntityState, EntityResult> operation) =>
        entities.TryGetValue(id, out var state) ? operation(state) : NotFound(id);

    private static EntityResult NotFound(EntityId id) =>
        EntityResult.Failure(EntityErrorCodes.NotFound, $"Entity '{id}' was not found.");

    private static bool CanTransition(EntityLifecycleState current, EntityLifecycleState next) =>
        current != next && (current, next) switch
        {
            (EntityLifecycleState.Active, EntityLifecycleState.Inactive) => true,
            (EntityLifecycleState.Inactive, EntityLifecycleState.Active) => true,
            (EntityLifecycleState.Active, EntityLifecycleState.Retired) => true,
            (EntityLifecycleState.Active, EntityLifecycleState.Destroyed) => true,
            (EntityLifecycleState.Inactive, EntityLifecycleState.Retired) => true,
            (EntityLifecycleState.Inactive, EntityLifecycleState.Destroyed) => true,
            _ => false,
        };

    private sealed class EntityState
    {
        public EntityState(EntityId id, EntityCategory category, long createdAt)
        {
            Id = id;
            Category = category;
            CreatedAt = createdAt;
        }

        public EntityId Id { get; }
        public EntityCategory Category { get; }
        public EntityLifecycleState LifecycleState { get; set; } = EntityLifecycleState.Active;
        public HashSet<EntityTag> Tags { get; } = [];
        public EntityId? ParentId { get; set; }
        public EntityId? OwnerId { get; set; }
        public EntityId? RegionId { get; set; }
        public HashSet<ComponentTypeId> ComponentTypes { get; } = [];
        public long CreatedAt { get; }
        public long? RetiredAt { get; set; }

        public EntitySnapshot ToSnapshot() => new(
            Id,
            Category,
            LifecycleState,
            Tags.OrderBy(tag => tag.Value, StringComparer.Ordinal).ToArray(),
            ParentId,
            OwnerId,
            RegionId,
            ComponentTypes.OrderBy(component => component.Value, StringComparer.Ordinal).ToArray(),
            CreatedAt,
            RetiredAt);

        public static EntityState FromSnapshot(EntitySnapshot snapshot)
        {
            var state = new EntityState(snapshot.Id, snapshot.Category, snapshot.CreatedAt)
            {
                LifecycleState = snapshot.LifecycleState,
                ParentId = snapshot.ParentId,
                OwnerId = snapshot.OwnerId,
                RegionId = snapshot.RegionId,
                RetiredAt = snapshot.RetiredAt,
            };

            state.Tags.UnionWith(snapshot.Tags);
            state.ComponentTypes.UnionWith(snapshot.ComponentTypes);
            return state;
        }
    }
}
