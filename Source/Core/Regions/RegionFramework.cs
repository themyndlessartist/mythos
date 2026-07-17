using Mythos.Framework.Entities;

namespace Mythos.Framework.Regions;

/// <summary>Owns engine-independent Region metadata and validates Entity Framework integration.</summary>
public sealed class RegionFramework
{
    public static readonly EntityCategory EntityRegionCategory = new("Region");
    private readonly EntityRegistry entities;
    private Dictionary<EntityId, RegionState> regions = [];
    private Dictionary<(EntityId First, EntityId Second), IReadOnlyDictionary<string, string>> adjacency = [];

    public RegionFramework(EntityRegistry entities)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
    }

    public EntityId? RootRegionId { get; private set; }
    public int Count => regions.Count;

    public RegionResult<RegionRecordSnapshot> CreateRoot(RegionCategory category, long createdAt,
        string? boundaryReference = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (RootRegionId is not null)
        {
            return RegionResult<RegionRecordSnapshot>.Failure(RegionErrorCodes.RootConflict, "A root world region already exists.");
        }

        return CreateInternal(category, null, createdAt, boundaryReference, metadata);
    }

    public RegionResult<RegionRecordSnapshot> CreateRegion(RegionCategory category, EntityId parentId, long createdAt,
        string? boundaryReference = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        CreateInternal(category, parentId, createdAt, boundaryReference, metadata);

    public RegionResult<RegionRecordSnapshot> Find(EntityId id) => regions.TryGetValue(id, out var state)
        ? RegionResult<RegionRecordSnapshot>.Success(state.ToSnapshot())
        : RegionResult<RegionRecordSnapshot>.Failure(RegionErrorCodes.InvalidReference, $"Region '{id}' was not found.");

    public IReadOnlyList<RegionRecordSnapshot> QueryChildren(EntityId parentId) =>
        Ordered(regions.Values.Where(region => region.ParentId == parentId));

    public IReadOnlyList<RegionRecordSnapshot> QueryAncestors(EntityId id)
    {
        var result = new List<RegionState>();
        var current = regions.TryGetValue(id, out var initial) ? initial.ParentId : null;
        while (current is { } parentId && regions.TryGetValue(parentId, out var parent))
        {
            result.Add(parent);
            current = parent.ParentId;
        }

        return result.Select(state => state.ToSnapshot()).ToArray();
    }

    public IReadOnlyList<RegionRecordSnapshot> QueryDescendants(EntityId id)
    {
        if (!regions.ContainsKey(id)) return [];
        var found = new List<RegionState>();
        var pending = new Queue<EntityId>();
        pending.Enqueue(id);
        while (pending.Count > 0)
        {
            var parent = pending.Dequeue();
            foreach (var child in regions.Values.Where(r => r.ParentId == parent).OrderBy(r => r.Id.Value))
            {
                found.Add(child);
                pending.Enqueue(child.Id);
            }
        }

        return Ordered(found);
    }

    public bool Contains(EntityId containingRegionId, EntityId regionId) =>
        containingRegionId == regionId || QueryAncestors(regionId).Any(region => region.Id == containingRegionId);

    public RegionResult AssignParent(EntityId id, EntityId? parentId)
    {
        if (!regions.TryGetValue(id, out var state)) return Missing(id);
        if (id == RootRegionId && parentId is not null)
            return RegionResult.Failure(RegionErrorCodes.InvalidHierarchy, "The root world region cannot have a parent.");
        if (parentId is null && id != RootRegionId)
            return RegionResult.Failure(RegionErrorCodes.InvalidHierarchy, "A non-root region cannot be orphaned.");
        if (parentId is { } parent && !IsUsableRegion(parent, out var error)) return error;
        if (parentId == id || (parentId is { } candidate && Contains(id, candidate)))
            return RegionResult.Failure(RegionErrorCodes.HierarchyCycle, "Parent assignment would create a region hierarchy cycle.");

        var previousParent = state.ParentId;
        state.ParentId = parentId;
        var ownershipValid = regions.Values.All(region => Contains(region.SimulationOwnerId, region.Id));
        state.ParentId = previousParent;
        if (!ownershipValid)
            return RegionResult.Failure(RegionErrorCodes.InvalidState, "Parent assignment would invalidate simulation ownership.");

        var assigned = entities.AssignParent(id, parentId);
        if (!assigned.IsSuccess) return FromEntity(assigned);
        state.ParentId = parentId;
        return RegionResult.Success();
    }

    public RegionResult AddAdjacency(EntityId first, EntityId second, IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (first == second) return RegionResult.Failure(RegionErrorCodes.InvalidReference, "A region cannot be adjacent to itself.");
        if (!IsUsableRegion(first, out var firstError)) return firstError;
        if (!IsUsableRegion(second, out var secondError)) return secondError;
        var metadataResult = CopyMetadata(metadata);
        if (!metadataResult.IsSuccess) return RegionResult.Failure(metadataResult.Error!.Code, metadataResult.Error.Message);
        adjacency[Pair(first, second)] = metadataResult.Value!;
        return RegionResult.Success();
    }

    public RegionResult RemoveAdjacency(EntityId first, EntityId second)
    {
        adjacency.Remove(Pair(first, second));
        return RegionResult.Success();
    }

    public IReadOnlyList<RegionRecordSnapshot> QueryAdjacent(EntityId id) => Ordered(
        adjacency.Keys.Where(pair => pair.First == id || pair.Second == id)
            .Select(pair => regions[pair.First == id ? pair.Second : pair.First]));

    public RegionResult AssignEntity(EntityId entityId, EntityId regionId)
    {
        if (!IsUsableEntity(entityId, out var entityError)) return entityError;
        if (!IsUsableRegion(regionId, out var regionError)) return regionError;
        return FromEntity(entities.AssignRegion(entityId, regionId));
    }

    public RegionResult TransferEntity(EntityId entityId, EntityId fromRegionId, EntityId toRegionId)
    {
        if (!IsUsableEntity(entityId, out var entityError)) return entityError;
        var entity = entities.Find(entityId).Value!;
        if (entity.RegionId != fromRegionId)
            return RegionResult.Failure(RegionErrorCodes.InvalidState, $"Entity '{entityId}' is not assigned to source region '{fromRegionId}'.");
        if (!IsUsableRegion(toRegionId, out var regionError)) return regionError;
        return FromEntity(entities.AssignRegion(entityId, toRegionId));
    }

    public IReadOnlyList<EntitySnapshot> QueryAssignedEntities(EntityId regionId, bool includeDescendants = false)
    {
        var ids = includeDescendants
            ? QueryDescendants(regionId).Select(region => region.Id).Append(regionId).ToHashSet()
            : new HashSet<EntityId> { regionId };
        return entities.ExportSnapshots().Where(entity => entity.RegionId is { } id && ids.Contains(id)).ToArray();
    }

    public RegionResult SetSimulationState(EntityId id, RegionSimulationState state)
    {
        if (!Enum.IsDefined(state)) return RegionResult.Failure(RegionErrorCodes.InvalidState, "Simulation state is undefined.");
        if (!IsUsableRegion(id, out var error)) return error;
        regions[id].SimulationState = state;
        return RegionResult.Success();
    }

    public RegionResult SetSimulationOwner(EntityId id, EntityId ownerRegionId)
    {
        if (!IsUsableRegion(id, out var error)) return error;
        if (!IsUsableRegion(ownerRegionId, out var ownerError)) return ownerError;
        if (!Contains(ownerRegionId, id))
            return RegionResult.Failure(RegionErrorCodes.InvalidState, "Simulation owner must contain the coordinated region.");
        regions[id].SimulationOwnerId = ownerRegionId;
        return RegionResult.Success();
    }

    public RegionResult<EntityId> ResolveSimulationOwner(EntityId id) => IsUsableRegion(id, out var error)
        ? RegionResult<EntityId>.Success(regions[id].SimulationOwnerId)
        : RegionResult<EntityId>.Failure(error.Error!.Code, error.Error.Message);

    public RegionResult<RegionDiagnostic> Inspect(EntityId id)
    {
        if (!IsUsableRegion(id, out var error))
            return RegionResult<RegionDiagnostic>.Failure(error.Error!.Code, error.Error.Message);
        var state = regions[id];
        return RegionResult<RegionDiagnostic>.Success(new RegionDiagnostic(id, state.Category, state.ParentId,
            QueryChildren(id).Select(r => r.Id).ToArray(), QueryAdjacent(id).Select(r => r.Id).ToArray(),
            QueryAssignedEntities(id).Select(e => e.Id).ToArray(), state.SimulationState, state.SimulationOwnerId));
    }

    /// <summary>Validates Region-to-Entity references before persistence or simulation boundaries.</summary>
    public RegionResult ValidateReferences()
    {
        if (RootRegionId is not { } rootId || !regions.ContainsKey(rootId))
            return RegionResult.Failure(RegionErrorCodes.InvalidReference, "The root Region reference is missing.");

        foreach (var region in regions.Values)
        {
            if (!EntityMatchesRegion(region.Id)) return Missing(region.Id);
            var entity = entities.Find(region.Id).Value!;
            if (entity.ParentId != region.ParentId)
                return RegionResult.Failure(RegionErrorCodes.InvalidHierarchy, "Region and Entity hierarchy references disagree.");
            if (!regions.ContainsKey(region.SimulationOwnerId) || !EntityMatchesRegion(region.SimulationOwnerId))
                return RegionResult.Failure(RegionErrorCodes.InvalidReference, "Simulation owner is missing, terminal, or invalid.");
            if (!Contains(regions, region.SimulationOwnerId, region.Id))
                return RegionResult.Failure(RegionErrorCodes.InvalidState, "Simulation owner does not contain its Region.");
        }

        foreach (var edge in adjacency.Keys)
            if (!IsUsableRegion(edge.First, out _) || !IsUsableRegion(edge.Second, out _))
                return RegionResult.Failure(RegionErrorCodes.InvalidReference, "Adjacency contains a missing, terminal, or invalid Region.");

        foreach (var entity in entities.ExportSnapshots().Where(item => item.RegionId is not null))
            if (!IsUsableEntity(entity.Id, out _) || !IsUsableRegion(entity.RegionId!.Value, out _))
                return RegionResult.Failure(RegionErrorCodes.InvalidReference, "Region assignment contains a missing, terminal, or invalid reference.");

        return RegionResult.Success();
    }

    /// <summary>Validates one Entity's current Region assignment without scanning unrelated Region state.</summary>
    public RegionResult ValidateAssignment(EntityId entityId)
    {
        if (!IsUsableEntity(entityId, out var entityError)) return entityError;
        var entity = entities.Find(entityId).Value!;
        if (entity.RegionId is not { } regionId || !IsUsableRegion(regionId, out _))
            return RegionResult.Failure(RegionErrorCodes.InvalidReference,
                $"Entity '{entityId}' has a missing, terminal, or invalid Region assignment.");
        return RegionResult.Success();
    }

    public RegionFrameworkSnapshot ExportSnapshot()
    {
        var records = Ordered(regions.Values);
        var edges = adjacency.OrderBy(item => item.Key.First.Value).ThenBy(item => item.Key.Second.Value)
            .Select(item => new RegionAdjacencySnapshot(item.Key.First, item.Key.Second, item.Value)).ToArray();
        var assignments = entities.ExportSnapshots().Where(entity => entity.RegionId is not null)
            .Select(entity => new RegionAssignmentSnapshot(entity.Id, entity.RegionId!.Value)).ToArray();
        return new RegionFrameworkSnapshot(RegionFrameworkSnapshot.CurrentVersion, RootRegionId!.Value, records, edges, assignments);
    }

    public RegionResult Restore(RegionFrameworkSnapshot snapshot)
    {
        var prepared = PrepareRestore(snapshot);
        if (!prepared.IsSuccess) return RegionResult.Failure(prepared.Error!.Code, prepared.Error.Message);

        // All Entity operations have been validated above; retain old assignments for defensive rollback.
        var previous = entities.ExportSnapshots().ToDictionary(entity => entity.Id, entity => entity.RegionId);
        foreach (var entity in entities.ExportSnapshots().Where(entity => entity.RegionId is not null)) entities.AssignRegion(entity.Id, null);
        foreach (var assignment in snapshot.Assignments!)
        {
            var result = entities.AssignRegion(assignment.EntityId, assignment.RegionId);
            if (!result.IsSuccess)
            {
                foreach (var item in previous) entities.AssignRegion(item.Key, item.Value);
                return FromEntity(result);
            }
        }

        regions = prepared.Value!.Regions;
        adjacency = prepared.Value.Adjacency;
        RootRegionId = snapshot.RootRegionId;
        return RegionResult.Success();
    }

    private RegionResult<PreparedState> PrepareRestore(RegionFrameworkSnapshot snapshot)
    {
        if (snapshot is null || snapshot.Version != RegionFrameworkSnapshot.CurrentVersion)
            return RegionResult<PreparedState>.Failure(RegionErrorCodes.IncompatibleSnapshot, "Region snapshot version is unsupported.");
        if (snapshot.Regions is null || snapshot.Adjacency is null || snapshot.Assignments is null || snapshot.RootRegionId.Value == Guid.Empty)
            return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidSnapshot, "Region snapshot collections and root ID must be valid.");
        if (snapshot.Regions.Count == 0 || snapshot.Regions.Any(record => record is null) ||
            snapshot.Adjacency.Any(edge => edge is null) || snapshot.Assignments.Any(assignment => assignment is null))
            return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidSnapshot, "Region snapshot collections cannot contain null entries.");
        if (snapshot.Regions.Select(r => r.Id).Distinct().Count() != snapshot.Regions.Count)
            return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidSnapshot, "Region records must be non-empty and uniquely identified.");

        var built = new Dictionary<EntityId, RegionState>();
        foreach (var record in snapshot.Regions)
        {
            if (!ValidateRecord(record, snapshot.RootRegionId, out var error)) return RegionResult<PreparedState>.Failure(error!.Code, error.Message);
            built.Add(record.Id, RegionState.FromSnapshot(record));
        }
        if (!built.TryGetValue(snapshot.RootRegionId, out var root) || root.ParentId is not null || built.Values.Count(r => r.ParentId is null) != 1)
            return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidHierarchy, "Snapshot must contain exactly one parentless root world region.");
        foreach (var region in built.Values)
        {
            if (region.ParentId is { } parent && !built.ContainsKey(parent)) return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidReference, "Region parent is missing.");
            if (!EntityMatchesRegion(region.Id)) return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidCategory, $"Entity '{region.Id}' is missing, terminal, or not a Region.");
            if (entities.Find(region.Id).Value!.ParentId != region.ParentId)
                return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidHierarchy, "Region and Entity hierarchy references disagree.");
            if (!built.ContainsKey(region.SimulationOwnerId)) return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidReference, "Simulation owner is missing.");
            var visited = new HashSet<EntityId>();
            for (RegionState? cursor = region; cursor is not null; cursor = cursor.ParentId is { } p ? built[p] : null)
                if (!visited.Add(cursor.Id)) return RegionResult<PreparedState>.Failure(RegionErrorCodes.HierarchyCycle, "Region hierarchy contains a cycle.");
        }
        foreach (var region in built.Values)
            if (!Contains(built, region.SimulationOwnerId, region.Id)) return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidState, "Simulation owner does not contain its region.");

        var edges = new Dictionary<(EntityId, EntityId), IReadOnlyDictionary<string, string>>();
        foreach (var edge in snapshot.Adjacency)
        {
            if (edge.RegionId == edge.AdjacentRegionId || !built.ContainsKey(edge.RegionId) || !built.ContainsKey(edge.AdjacentRegionId) || edge.Metadata is null)
                return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidReference, "Adjacency contains an invalid reference.");
            var copy = CopyMetadata(edge.Metadata);
            if (!copy.IsSuccess || !edges.TryAdd(Pair(edge.RegionId, edge.AdjacentRegionId), copy.Value!))
                return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidSnapshot, "Adjacency is duplicated or malformed.");
        }
        if (snapshot.Assignments.Select(a => a.EntityId).Distinct().Count() != snapshot.Assignments.Count)
            return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidSnapshot, "Entity assignments must be unique.");
        foreach (var assignment in snapshot.Assignments)
            if (!built.ContainsKey(assignment.RegionId) || !IsUsableEntity(assignment.EntityId, out _))
                return RegionResult<PreparedState>.Failure(RegionErrorCodes.InvalidReference, "Entity assignment contains a missing, terminal, or invalid reference.");

        return RegionResult<PreparedState>.Success(new PreparedState(built, edges));
    }

    private RegionResult<RegionRecordSnapshot> CreateInternal(RegionCategory category, EntityId? parentId, long createdAt,
        string? boundaryReference, IReadOnlyDictionary<string, string>? metadata)
    {
        if (string.IsNullOrWhiteSpace(category.Value)) return RegionResult<RegionRecordSnapshot>.Failure(RegionErrorCodes.InvalidCategory, "Region category must be initialized.");
        if (boundaryReference is not null && string.IsNullOrWhiteSpace(boundaryReference))
            return RegionResult<RegionRecordSnapshot>.Failure(RegionErrorCodes.InvalidReference, "Boundary reference cannot be blank.");
        if (parentId is { } parent && !IsUsableRegion(parent, out var parentError)) return RegionResult<RegionRecordSnapshot>.Failure(parentError.Error!.Code, parentError.Error.Message);
        var copied = CopyMetadata(metadata);
        if (!copied.IsSuccess) return RegionResult<RegionRecordSnapshot>.Failure(copied.Error!.Code, copied.Error.Message);
        var created = entities.Create(EntityRegionCategory, createdAt);
        if (!created.IsSuccess) return RegionResult<RegionRecordSnapshot>.Failure(RegionErrorCodes.InvalidState, created.Error!.Message);
        if (parentId is { } containing) entities.AssignParent(created.Value!.Id, containing);
        var state = new RegionState(created.Value!.Id, category, parentId, RegionSimulationState.Abstract,
            parentId ?? created.Value.Id, NormalizeReference(boundaryReference), copied.Value!);
        regions.Add(state.Id, state);
        RootRegionId ??= state.Id;
        return RegionResult<RegionRecordSnapshot>.Success(state.ToSnapshot());
    }

    private bool IsUsableRegion(EntityId id, out RegionResult error)
    {
        if (!regions.ContainsKey(id) || !EntityMatchesRegion(id))
        {
            error = RegionResult.Failure(RegionErrorCodes.InvalidReference, $"Region '{id}' is missing, terminal, or invalid.");
            return false;
        }
        error = RegionResult.Success(); return true;
    }

    private bool IsUsableEntity(EntityId id, out RegionResult error)
    {
        var found = entities.Find(id);
        if (!found.IsSuccess || found.Value!.LifecycleState is EntityLifecycleState.Retired or EntityLifecycleState.Destroyed)
        {
            error = RegionResult.Failure(RegionErrorCodes.InvalidReference, $"Entity '{id}' is missing or terminal.");
            return false;
        }
        error = RegionResult.Success(); return true;
    }

    private bool EntityMatchesRegion(EntityId id)
    {
        var found = entities.Find(id);
        return found.IsSuccess && found.Value!.Category == EntityRegionCategory &&
            found.Value.LifecycleState is not EntityLifecycleState.Retired and not EntityLifecycleState.Destroyed;
    }

    private bool ValidateRecord(RegionRecordSnapshot record, EntityId rootId, out RegionError? error)
    {
        if (record is null || record.Id.Value == Guid.Empty || string.IsNullOrWhiteSpace(record.Category.Value) ||
            !Enum.IsDefined(record.SimulationState) || record.SimulationOwnerId.Value == Guid.Empty || record.Metadata is null ||
            record.ParentId == record.Id || (record.Id == rootId && record.ParentId is not null))
        { error = new RegionError(RegionErrorCodes.InvalidSnapshot, "Region record is malformed."); return false; }
        var metadata = CopyMetadata(record.Metadata);
        if (!metadata.IsSuccess) { error = metadata.Error; return false; }
        if (record.BoundaryReference is not null && string.IsNullOrWhiteSpace(record.BoundaryReference))
        { error = new RegionError(RegionErrorCodes.InvalidSnapshot, "Boundary reference cannot be blank."); return false; }
        error = null; return true;
    }

    private static RegionResult<IReadOnlyDictionary<string, string>> CopyMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is not null) foreach (var item in metadata)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Value is null)
                    return RegionResult<IReadOnlyDictionary<string, string>>.Failure(RegionErrorCodes.InvalidSnapshot, "Metadata keys and values must be valid.");
                copy[item.Key.Trim()] = item.Value;
            }
        return RegionResult<IReadOnlyDictionary<string, string>>.Success(new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(copy));
    }

    private static string? NormalizeReference(string? value) => value?.Trim();
    private static (EntityId, EntityId) Pair(EntityId a, EntityId b) => a.Value.CompareTo(b.Value) < 0 ? (a, b) : (b, a);
    private static IReadOnlyList<RegionRecordSnapshot> Ordered(IEnumerable<RegionState> values) => values.OrderBy(r => r.Id.Value).Select(r => r.ToSnapshot()).ToArray();
    private static bool Contains(Dictionary<EntityId, RegionState> values, EntityId containing, EntityId id)
    {
        for (var current = id; ;)
        {
            if (current == containing) return true;
            if (!values.TryGetValue(current, out var state) || state.ParentId is not { } parent) return false;
            current = parent;
        }
    }
    private static RegionResult Missing(EntityId id) => RegionResult.Failure(RegionErrorCodes.InvalidReference, $"Region '{id}' was not found.");
    private static RegionResult FromEntity(EntityResult result) => result.IsSuccess ? RegionResult.Success() : RegionResult.Failure(RegionErrorCodes.InvalidReference, result.Error!.Message);

    private sealed record PreparedState(Dictionary<EntityId, RegionState> Regions, Dictionary<(EntityId, EntityId), IReadOnlyDictionary<string, string>> Adjacency);
    private sealed class RegionState(EntityId id, RegionCategory category, EntityId? parentId, RegionSimulationState simulationState,
        EntityId simulationOwnerId, string? boundaryReference, IReadOnlyDictionary<string, string> metadata)
    {
        public EntityId Id { get; } = id;
        public RegionCategory Category { get; } = category;
        public EntityId? ParentId { get; set; } = parentId;
        public RegionSimulationState SimulationState { get; set; } = simulationState;
        public EntityId SimulationOwnerId { get; set; } = simulationOwnerId;
        public string? BoundaryReference { get; } = boundaryReference;
        public IReadOnlyDictionary<string, string> Metadata { get; } = metadata;
        public RegionRecordSnapshot ToSnapshot() => new(Id, Category, ParentId, SimulationState, SimulationOwnerId, BoundaryReference, Metadata);
        public static RegionState FromSnapshot(RegionRecordSnapshot value) => new(value.Id, value.Category, value.ParentId, value.SimulationState,
            value.SimulationOwnerId, value.BoundaryReference, CopyMetadata(value.Metadata).Value!);
    }
}
