using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Relationships;

/// <summary>Owns directed, setting-agnostic relationship state between persistent entities.</summary>
public sealed class RelationshipFramework
{
    public const int MinimumDimensionValue = -1000;
    public const int MaximumDimensionValue = 1000;

    private readonly EntityRegistry entities;
    private readonly IRelationshipIdGenerator idGenerator;
    private readonly IRelationshipEventSink? events;
    private Dictionary<RelationshipId, RelationshipSnapshot> relationships = [];

    public RelationshipFramework(EntityRegistry entities, IRelationshipIdGenerator? idGenerator = null,
        IRelationshipEventSink? events = null)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.idGenerator = idGenerator ?? new Version7RelationshipIdGenerator();
        this.events = events;
    }

    public int Count => relationships.Count;

    public RelationshipResult<RelationshipSnapshot> Create(EntityId source, EntityId target,
        RelationshipKindId kind, WorldTimestamp timestamp, string? provenanceReference = null)
    {
        var baseValidation = ValidateInputs(source, target, kind, provenanceReference);
        if (!baseValidation.IsSuccess) return Fail<RelationshipSnapshot>(baseValidation.Error!);
        if (FindActive(source, target, kind).IsSuccess)
            return RelationshipResult<RelationshipSnapshot>.Failure(RelationshipErrorCodes.DuplicateActiveTuple,
                "An active relationship already exists for the source, target, and kind.");
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = idGenerator.Create();
            if (id.Value == Guid.Empty || relationships.ContainsKey(id)) continue;
            var snapshot = new RelationshipSnapshot(id, source, target, kind, RelationshipLifecycleState.Active,
                new Dictionary<string, int>(), timestamp, timestamp, NormalizeOptional(provenanceReference));
            var published = Publish(new("RelationshipCreated", id, source, target, timestamp));
            if (!published.IsSuccess) return Fail<RelationshipSnapshot>(published.Error!);
            relationships.Add(id, snapshot);
            return RelationshipResult<RelationshipSnapshot>.Success(snapshot);
        }
        return RelationshipResult<RelationshipSnapshot>.Failure(RelationshipErrorCodes.DuplicateId,
            "The relationship ID generator did not produce a unique initialized ID.");
    }

    public RelationshipResult<RelationshipSnapshot> Find(RelationshipId id) => relationships.TryGetValue(id, out var value)
        ? RelationshipResult<RelationshipSnapshot>.Success(value)
        : RelationshipResult<RelationshipSnapshot>.Failure(RelationshipErrorCodes.NotFound, "Relationship was not found.");

    public RelationshipResult<RelationshipSnapshot> FindActive(EntityId source, EntityId target, RelationshipKindId kind)
    {
        var found = relationships.Values.FirstOrDefault(item => item.LifecycleState == RelationshipLifecycleState.Active &&
            item.SourceEntityId == source && item.TargetEntityId == target && item.KindId == kind);
        return found is null
            ? RelationshipResult<RelationshipSnapshot>.Failure(RelationshipErrorCodes.NotFound, "Active relationship was not found.")
            : RelationshipResult<RelationshipSnapshot>.Success(found);
    }

    public IReadOnlyList<RelationshipSnapshot> QueryFrom(EntityId id) => Query(item => item.SourceEntityId == id);
    public IReadOnlyList<RelationshipSnapshot> QueryToward(EntityId id) => Query(item => item.TargetEntityId == id);
    public IReadOnlyList<RelationshipSnapshot> QueryInvolving(EntityId id) => Query(item => item.SourceEntityId == id || item.TargetEntityId == id);
    public IReadOnlyList<RelationshipSnapshot> QueryByKind(RelationshipKindId kind) => Query(item => item.KindId == kind);

    public RelationshipResult SetDimension(RelationshipId id, RelationshipDimensionId dimension, int value,
        WorldTimestamp timestamp, string? provenanceReference = null) => MutateDimension(id, dimension, timestamp,
            provenanceReference, _ => value, "RelationshipDimensionChanged");

    public RelationshipResult ApplyDelta(RelationshipId id, RelationshipDimensionId dimension, int delta,
        WorldTimestamp timestamp, string? provenanceReference = null) => MutateDimension(id, dimension, timestamp,
            provenanceReference, current => checked(current + delta), "RelationshipDimensionChanged");

    public RelationshipResult RemoveDimension(RelationshipId id, RelationshipDimensionId dimension,
        WorldTimestamp timestamp, string? provenanceReference = null)
    {
        if (!Valid(dimension.Value)) return RelationshipResult.Failure(RelationshipErrorCodes.InvalidDimension, "Dimension ID must be normalized.");
        var lookup = FindMutable(id, timestamp, provenanceReference);
        if (!lookup.IsSuccess) return RelationshipResult.Failure(lookup.Error!.Code, lookup.Error.Message);
        var current = lookup.Value!;
        if (current.Dimensions is null || !current.Dimensions.TryGetValue(dimension.Value, out var oldValue)) return RelationshipResult.Success();
        var values = new SortedDictionary<string, int>(current.Dimensions.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal);
        values.Remove(dimension.Value);
        var next = Copy(current, values, timestamp, provenanceReference, current.LifecycleState);
        var published = Publish(new("RelationshipDimensionRemoved", id, current.SourceEntityId, current.TargetEntityId,
            timestamp, dimension, oldValue, null));
        if (!published.IsSuccess) return published;
        relationships[id] = next;
        return RelationshipResult.Success();
    }

    public RelationshipResult Retire(RelationshipId id, WorldTimestamp timestamp, string? provenanceReference = null)
    {
        var lookup = FindMutable(id, timestamp, provenanceReference);
        if (!lookup.IsSuccess) return RelationshipResult.Failure(lookup.Error!.Code, lookup.Error.Message);
        var current = lookup.Value!;
        var next = Copy(current, current.Dimensions!, timestamp, provenanceReference, RelationshipLifecycleState.Retired);
        var published = Publish(new("RelationshipRetired", id, current.SourceEntityId, current.TargetEntityId, timestamp));
        if (!published.IsSuccess) return published;
        relationships[id] = next;
        return RelationshipResult.Success();
    }

    public RelationshipResult ValidateReferences()
    {
        foreach (var item in relationships.Values.OrderBy(item => item.Id.Value))
        {
            var valid = ValidateSnapshot(item);
            if (!valid.IsSuccess) return valid;
        }
        return ValidateTupleUniqueness(relationships.Values);
    }

    public RelationshipResult<RelationshipDiagnostic> Inspect(RelationshipId id)
    {
        var found = Find(id);
        if (!found.IsSuccess) return RelationshipResult<RelationshipDiagnostic>.Failure(found.Error!.Code, found.Error.Message);
        var valid = ValidateSnapshot(found.Value!);
        return RelationshipResult<RelationshipDiagnostic>.Success(new(found.Value!, valid.IsSuccess ? "valid" : $"{valid.Error!.Code}: {valid.Error.Message}"));
    }

    public RelationshipFrameworkSnapshot ExportSnapshot() => new(RelationshipFrameworkSnapshot.CurrentVersion,
        relationships.Values.OrderBy(item => item.Id.Value).ToArray());

    public RelationshipResult RestoreSnapshot(RelationshipFrameworkSnapshot? snapshot)
    {
        if (snapshot is null) return RelationshipResult.Failure(RelationshipErrorCodes.InvalidSnapshot, "Relationship snapshot cannot be null.");
        if (snapshot.Version != RelationshipFrameworkSnapshot.CurrentVersion)
            return RelationshipResult.Failure(RelationshipErrorCodes.UnsupportedSnapshotVersion, "Relationship snapshot version is unsupported.");
        if (snapshot.Relationships is null || snapshot.Relationships.Any(item => item is null))
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidSnapshot, "Relationship collection cannot be null or contain null records.");
        var candidate = new Dictionary<RelationshipId, RelationshipSnapshot>();
        foreach (var item in snapshot.Relationships)
        {
            if (!candidate.TryAdd(item.Id, Canonical(item)))
                return RelationshipResult.Failure(RelationshipErrorCodes.DuplicateId, "Relationship snapshot contains duplicate IDs.");
            var valid = ValidateSnapshot(item);
            if (!valid.IsSuccess) return valid;
        }
        var tuples = ValidateTupleUniqueness(candidate.Values);
        if (!tuples.IsSuccess) return tuples;
        relationships = candidate;
        return RelationshipResult.Success();
    }

    private RelationshipResult MutateDimension(RelationshipId id, RelationshipDimensionId dimension, WorldTimestamp timestamp,
        string? provenanceReference, Func<int, int> change, string eventType)
    {
        if (!Valid(dimension.Value)) return RelationshipResult.Failure(RelationshipErrorCodes.InvalidDimension, "Dimension ID must be normalized.");
        var lookup = FindMutable(id, timestamp, provenanceReference);
        if (!lookup.IsSuccess) return RelationshipResult.Failure(lookup.Error!.Code, lookup.Error.Message);
        var current = lookup.Value!;
        var oldValue = current.Dimensions?.GetValueOrDefault(dimension.Value) ?? 0;
        int value;
        try { value = change(oldValue); }
        catch (OverflowException) { return RelationshipResult.Failure(RelationshipErrorCodes.InvalidDimension, "Dimension arithmetic overflowed."); }
        if (value is < MinimumDimensionValue or > MaximumDimensionValue)
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidDimension, "Dimension value is outside the approved range.");
        var values = new SortedDictionary<string, int>((current.Dimensions ?? new Dictionary<string, int>()).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal)
        { [dimension.Value] = value };
        var next = Copy(current, values, timestamp, provenanceReference, current.LifecycleState);
        var published = Publish(new(eventType, id, current.SourceEntityId, current.TargetEntityId, timestamp, dimension, oldValue, value));
        if (!published.IsSuccess) return published;
        relationships[id] = next;
        return RelationshipResult.Success();
    }

    private RelationshipResult<RelationshipSnapshot> FindMutable(RelationshipId id, WorldTimestamp timestamp, string? provenanceReference)
    {
        var found = Find(id);
        if (!found.IsSuccess) return found;
        var current = found.Value!;
        if (current.LifecycleState != RelationshipLifecycleState.Active)
            return RelationshipResult<RelationshipSnapshot>.Failure(RelationshipErrorCodes.InvalidLifecycle, "Only active relationships can change.");
        if (timestamp.Value < current.LastChangedAt.Value)
            return RelationshipResult<RelationshipSnapshot>.Failure(RelationshipErrorCodes.InvalidTimestamp, "Change timestamp cannot precede the previous change.");
        if (!ValidOptional(provenanceReference))
            return RelationshipResult<RelationshipSnapshot>.Failure(RelationshipErrorCodes.InvalidIdentifier, "Provenance reference must be normalized.");
        return found;
    }

    private RelationshipResult ValidateSnapshot(RelationshipSnapshot item)
    {
        if (item.Id.Value == Guid.Empty || item.SourceEntityId.Value == Guid.Empty || item.TargetEntityId.Value == Guid.Empty)
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidIdentifier, "Relationship and Entity IDs must be initialized.");
        if (item.SourceEntityId == item.TargetEntityId)
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidReference, "Relationship source and target must be distinct.");
        if (!Valid(item.KindId.Value))
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidIdentifier, "Relationship kind must be normalized.");
        if (!ValidOptional(item.ProvenanceReference))
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidIdentifier, "Provenance reference must be normalized.");
        if (!Enum.IsDefined(item.LifecycleState))
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidLifecycle, "Relationship lifecycle is invalid.");
        if (!ValidEntity(item.SourceEntityId) || !ValidEntity(item.TargetEntityId))
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidReference, "Relationship participants must resolve to non-destroyed entities.");
        if (item.CreatedAt.Value > item.LastChangedAt.Value)
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidTimestamp, "Creation time cannot follow last-change time.");
        if (item.Dimensions is null)
            return RelationshipResult.Failure(RelationshipErrorCodes.InvalidSnapshot, "Relationship dimensions cannot be null.");
        foreach (var dimension in item.Dimensions)
            if (!Valid(dimension.Key) || dimension.Value is < MinimumDimensionValue or > MaximumDimensionValue)
                return RelationshipResult.Failure(RelationshipErrorCodes.InvalidDimension, "Relationship dimension is malformed or outside range.");
        return RelationshipResult.Success();
    }

    private RelationshipResult ValidateInputs(EntityId source, EntityId target, RelationshipKindId kind, string? provenance)
    {
        if (source == target) return RelationshipResult.Failure(RelationshipErrorCodes.InvalidReference, "Relationship source and target must be distinct.");
        if (!Valid(kind.Value) || !ValidOptional(provenance)) return RelationshipResult.Failure(RelationshipErrorCodes.InvalidIdentifier, "Kind and provenance identifiers must be normalized.");
        if (!ValidEntity(source) || !ValidEntity(target)) return RelationshipResult.Failure(RelationshipErrorCodes.InvalidReference, "Relationship participants must resolve to non-destroyed entities.");
        return RelationshipResult.Success();
    }

    private bool ValidEntity(EntityId id)
    {
        var found = entities.Find(id);
        return found.IsSuccess && found.Value!.LifecycleState != EntityLifecycleState.Destroyed;
    }

    private static RelationshipResult ValidateTupleUniqueness(IEnumerable<RelationshipSnapshot> items) =>
        items.Where(item => item.LifecycleState == RelationshipLifecycleState.Active)
            .GroupBy(item => (item.SourceEntityId, item.TargetEntityId, item.KindId))
            .Any(group => group.Count() > 1)
            ? RelationshipResult.Failure(RelationshipErrorCodes.DuplicateActiveTuple, "Snapshot contains duplicate active relationship tuples.")
            : RelationshipResult.Success();

    private IReadOnlyList<RelationshipSnapshot> Query(Func<RelationshipSnapshot, bool> predicate) =>
        relationships.Values.Where(predicate).OrderBy(item => item.Id.Value).ToArray();

    private RelationshipResult Publish(RelationshipDomainEvent value)
    {
        if (events is null) return RelationshipResult.Success();
        var result = events.Publish(value);
        return result.IsSuccess ? result : RelationshipResult.Failure(RelationshipErrorCodes.EventPublicationFailed, result.Error!.Message);
    }

    private static RelationshipSnapshot Copy(RelationshipSnapshot current, IReadOnlyDictionary<string, int> dimensions,
        WorldTimestamp timestamp, string? provenance, RelationshipLifecycleState lifecycle) =>
        new(current.Id, current.SourceEntityId, current.TargetEntityId, current.KindId, lifecycle, dimensions,
            current.CreatedAt, timestamp, NormalizeOptional(provenance));

    private static RelationshipSnapshot Canonical(RelationshipSnapshot item) =>
        new(item.Id, item.SourceEntityId, item.TargetEntityId, item.KindId, item.LifecycleState,
            item.Dimensions, item.CreatedAt, item.LastChangedAt, item.ProvenanceReference);

    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static bool ValidOptional(string? value) => value is null || Valid(value);
    private static string? NormalizeOptional(string? value) => value?.Trim();
    private static RelationshipResult<T> Fail<T>(RelationshipError error) => RelationshipResult<T>.Failure(error.Code, error.Message);
}
