using Mythos.Framework.Entities;
using Mythos.Framework.Regions;
using Mythos.Framework.Time;

namespace Mythos.Framework.DynamicEvents;

/// <summary>Owns persistent world situations without applying their effects.</summary>
public sealed class DynamicWorldEventFramework
{
    private readonly EntityRegistry entities;
    private readonly RegionFramework regions;
    private readonly IDynamicWorldEventIdGenerator ids;
    private readonly IDynamicWorldEventSink? events;
    private Dictionary<DynamicWorldEventId, DynamicWorldEventSnapshot> records = [];

    public DynamicWorldEventFramework(EntityRegistry entities, RegionFramework regions,
        IDynamicWorldEventIdGenerator? ids = null, IDynamicWorldEventSink? events = null)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
        this.ids = ids ?? new Version7DynamicWorldEventIdGenerator();
        this.events = events;
    }
    public int Count => records.Count;

    public DynamicWorldEventResult<DynamicWorldEventSnapshot> Create(DynamicWorldEventTypeId typeId,
        WorldTimestamp createdAt, WorldTimestamp? scheduledStartAt, bool active, EntityId? regionId,
        IReadOnlyList<EntityId>? participants, IReadOnlyDictionary<string, string>? attributes,
        string? sourceReference = null, string? provenanceReference = null)
    {
        var canonicalParticipants = CanonicalParticipants(participants);
        var canonicalAttributes = CanonicalAttributes(attributes);
        var state = active ? DynamicWorldEventLifecycleState.Active : DynamicWorldEventLifecycleState.Scheduled;
        WorldTimestamp? started = active ? createdAt : null;
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = ids.Create();
            if (id.Value == Guid.Empty || records.ContainsKey(id)) continue;
            var value = new DynamicWorldEventSnapshot(id, typeId, state, createdAt, scheduledStartAt, started, null,
                regionId, canonicalParticipants, canonicalAttributes, null, Normalize(sourceReference), Normalize(provenanceReference));
            var valid = Validate(value);
            if (!valid.IsSuccess) return Fail<DynamicWorldEventSnapshot>(valid.Error!);
            var published = Publish(new("DynamicWorldEventCreated", id, createdAt, regionId));
            if (!published.IsSuccess) return Fail<DynamicWorldEventSnapshot>(published.Error!);
            records.Add(id, value);
            return DynamicWorldEventResult<DynamicWorldEventSnapshot>.Success(value);
        }
        return DynamicWorldEventResult<DynamicWorldEventSnapshot>.Failure(DynamicWorldEventErrorCodes.DuplicateId,
            "Dynamic World Event ID generator did not produce a unique initialized ID.");
    }

    public DynamicWorldEventResult<DynamicWorldEventSnapshot> Find(DynamicWorldEventId id) => records.TryGetValue(id, out var value)
        ? DynamicWorldEventResult<DynamicWorldEventSnapshot>.Success(value)
        : DynamicWorldEventResult<DynamicWorldEventSnapshot>.Failure(DynamicWorldEventErrorCodes.NotFound,
            "Dynamic World Event was not found.");
    public DynamicWorldEventResult Activate(DynamicWorldEventId id, WorldTimestamp timestamp) => Transition(id,
        DynamicWorldEventLifecycleState.Scheduled, DynamicWorldEventLifecycleState.Active, timestamp, null,
        "DynamicWorldEventActivated");
    public DynamicWorldEventResult Resolve(DynamicWorldEventId id, DynamicWorldEventOutcomeId outcome,
        WorldTimestamp timestamp) => Transition(id, DynamicWorldEventLifecycleState.Active,
        DynamicWorldEventLifecycleState.Resolved, timestamp, outcome, "DynamicWorldEventResolved");
    public DynamicWorldEventResult Expire(DynamicWorldEventId id, WorldTimestamp timestamp)
    {
        var found = Find(id);
        if (!found.IsSuccess) return AsResult(found.Error!);
        if (found.Value!.LifecycleState is not (DynamicWorldEventLifecycleState.Scheduled or DynamicWorldEventLifecycleState.Active))
            return DynamicWorldEventResult.Failure(DynamicWorldEventErrorCodes.InvalidLifecycle,
                "Only Scheduled or Active Dynamic World Events may expire.");
        return CommitTransition(found.Value, DynamicWorldEventLifecycleState.Expired, timestamp, null,
            "DynamicWorldEventExpired");
    }
    public DynamicWorldEventResult Cancel(DynamicWorldEventId id, WorldTimestamp timestamp) => Transition(id,
        DynamicWorldEventLifecycleState.Scheduled, DynamicWorldEventLifecycleState.Cancelled, timestamp, null,
        "DynamicWorldEventCancelled");

    public IReadOnlyList<DynamicWorldEventSnapshot> QueryByType(DynamicWorldEventTypeId id) => Query(item => item.TypeId == id);
    public IReadOnlyList<DynamicWorldEventSnapshot> QueryByLifecycle(DynamicWorldEventLifecycleState state) => Query(item => item.LifecycleState == state);
    public IReadOnlyList<DynamicWorldEventSnapshot> QueryByParticipant(EntityId id) => Query(item => item.ParticipantEntityIds!.Contains(id));
    public IReadOnlyList<DynamicWorldEventSnapshot> QueryByRegion(EntityId id) => Query(item => item.RegionEntityId == id);
    public IReadOnlyList<DynamicWorldEventSnapshot> QueryByOutcome(DynamicWorldEventOutcomeId id) => Query(item => item.OutcomeId == id);
    public IReadOnlyList<DynamicWorldEventSnapshot> QueryBySource(string reference) => Query(item => item.SourceReference == reference);
    public IReadOnlyList<DynamicWorldEventSnapshot> QueryByTime(WorldTimestamp start, WorldTimestamp end) => start.Value > end.Value
        ? [] : Query(item => item.CreatedAt.Value >= start.Value && item.CreatedAt.Value <= end.Value);

    public DynamicWorldEventResult ValidateReferences()
    {
        foreach (var item in records.Values)
        {
            var valid = Validate(item);
            if (!valid.IsSuccess) return valid;
        }
        return DynamicWorldEventResult.Success();
    }
    public DynamicWorldEventResult<DynamicWorldEventDiagnostic> Inspect(DynamicWorldEventId id)
    {
        var found = Find(id);
        if (!found.IsSuccess) return DynamicWorldEventResult<DynamicWorldEventDiagnostic>.Failure(found.Error!.Code, found.Error.Message);
        var valid = Validate(found.Value!);
        return DynamicWorldEventResult<DynamicWorldEventDiagnostic>.Success(new(found.Value!,
            valid.IsSuccess ? "valid" : $"{valid.Error!.Code}: {valid.Error.Message}"));
    }
    public DynamicWorldEventFrameworkSnapshot ExportSnapshot() => new(DynamicWorldEventFrameworkSnapshot.CurrentVersion,
        records.Values.OrderBy(item => item.CreatedAt.Value).ThenBy(item => item.Id.Value).ToArray());
    public DynamicWorldEventResult RestoreSnapshot(DynamicWorldEventFrameworkSnapshot? snapshot)
    {
        if (snapshot is null) return DynamicWorldEventResult.Failure(DynamicWorldEventErrorCodes.InvalidSnapshot,
            "Dynamic World Event snapshot cannot be null.");
        if (snapshot.Version != DynamicWorldEventFrameworkSnapshot.CurrentVersion) return DynamicWorldEventResult.Failure(
            DynamicWorldEventErrorCodes.UnsupportedSnapshotVersion, "Dynamic World Event snapshot version is unsupported.");
        if (snapshot.Events is null || snapshot.Events.Any(item => item is null)) return DynamicWorldEventResult.Failure(
            DynamicWorldEventErrorCodes.InvalidSnapshot, "Dynamic World Event snapshot collection is malformed.");
        var candidate = new Dictionary<DynamicWorldEventId, DynamicWorldEventSnapshot>();
        foreach (var item in snapshot.Events)
        {
            if (!candidate.TryAdd(item.Id, item)) return DynamicWorldEventResult.Failure(
                DynamicWorldEventErrorCodes.DuplicateId, "Snapshot contains duplicate Dynamic World Event IDs.");
            var valid = Validate(item);
            if (!valid.IsSuccess) return valid;
        }
        records = candidate;
        return DynamicWorldEventResult.Success();
    }

    private DynamicWorldEventResult Transition(DynamicWorldEventId id, DynamicWorldEventLifecycleState expected,
        DynamicWorldEventLifecycleState next, WorldTimestamp timestamp, DynamicWorldEventOutcomeId? outcome, string eventType)
    {
        var found = Find(id);
        if (!found.IsSuccess) return AsResult(found.Error!);
        if (found.Value!.LifecycleState != expected) return DynamicWorldEventResult.Failure(
            DynamicWorldEventErrorCodes.InvalidLifecycle, $"Transition requires {expected} lifecycle.");
        return CommitTransition(found.Value, next, timestamp, outcome, eventType);
    }
    private DynamicWorldEventResult CommitTransition(DynamicWorldEventSnapshot current,
        DynamicWorldEventLifecycleState next, WorldTimestamp timestamp, DynamicWorldEventOutcomeId? outcome, string eventType)
    {
        if (timestamp.Value < current.CreatedAt.Value || current.StartedAt is { } started && timestamp.Value < started.Value ||
            outcome is { } outcomeId && !Valid(outcomeId.Value)) return DynamicWorldEventResult.Failure(
            outcome is not null ? DynamicWorldEventErrorCodes.InvalidIdentifier : DynamicWorldEventErrorCodes.InvalidTimestamp,
            "Transition timestamp or outcome is invalid.");
        var candidate = new DynamicWorldEventSnapshot(current.Id, current.TypeId, next, current.CreatedAt,
            current.ScheduledStartAt, next == DynamicWorldEventLifecycleState.Active ? timestamp : current.StartedAt,
            next is DynamicWorldEventLifecycleState.Resolved or DynamicWorldEventLifecycleState.Expired or DynamicWorldEventLifecycleState.Cancelled
                ? timestamp : null, current.RegionEntityId, current.ParticipantEntityIds, current.Attributes,
            next == DynamicWorldEventLifecycleState.Resolved ? outcome : null, current.SourceReference, current.ProvenanceReference);
        var valid = Validate(candidate);
        if (!valid.IsSuccess) return valid;
        var published = Publish(new(eventType, current.Id, timestamp, current.RegionEntityId));
        if (!published.IsSuccess) return published;
        records[current.Id] = candidate;
        return DynamicWorldEventResult.Success();
    }

    private DynamicWorldEventResult Validate(DynamicWorldEventSnapshot item)
    {
        if (item.Id.Value == Guid.Empty || !Valid(item.TypeId.Value) || !ValidOptional(item.SourceReference) ||
            !ValidOptional(item.ProvenanceReference) || item.OutcomeId is { } outcome && !Valid(outcome.Value))
            return DynamicWorldEventResult.Failure(DynamicWorldEventErrorCodes.InvalidIdentifier,
                "Dynamic World Event identifiers must be initialized and normalized.");
        if (!Enum.IsDefined(item.LifecycleState)) return DynamicWorldEventResult.Failure(
            DynamicWorldEventErrorCodes.InvalidLifecycle, "Dynamic World Event lifecycle is invalid.");
        if (item.ParticipantEntityIds is null || item.Attributes is null ||
            item.ParticipantEntityIds.Distinct().Count() != item.ParticipantEntityIds.Count ||
            !item.ParticipantEntityIds.SequenceEqual(item.ParticipantEntityIds.OrderBy(id => id.Value)) ||
            item.ParticipantEntityIds.Any(id => !entities.Exists(id)) ||
            item.Attributes.Any(pair => !Valid(pair.Key) || pair.Value is null) ||
            !item.Attributes.Keys.SequenceEqual(item.Attributes.Keys.Order(StringComparer.Ordinal)))
            return DynamicWorldEventResult.Failure(DynamicWorldEventErrorCodes.InvalidReference,
                "Dynamic World Event participants or attributes are malformed.");
        if (item.RegionEntityId is { } region && !regions.Find(region).IsSuccess) return DynamicWorldEventResult.Failure(
            DynamicWorldEventErrorCodes.InvalidReference, "Dynamic World Event Region is invalid.");
        if (item.ScheduledStartAt is { } scheduled && scheduled.Value < item.CreatedAt.Value ||
            item.StartedAt is { } started && started.Value < item.CreatedAt.Value ||
            item.ConcludedAt is { } concluded && (concluded.Value < item.CreatedAt.Value ||
                item.StartedAt is { } actual && concluded.Value < actual.Value))
            return DynamicWorldEventResult.Failure(DynamicWorldEventErrorCodes.InvalidTimestamp,
                "Dynamic World Event timestamps are invalid.");
        var shape = item.LifecycleState switch
        {
            DynamicWorldEventLifecycleState.Scheduled => item.StartedAt is null && item.ConcludedAt is null && item.OutcomeId is null,
            DynamicWorldEventLifecycleState.Active => item.StartedAt is not null && item.ConcludedAt is null && item.OutcomeId is null,
            DynamicWorldEventLifecycleState.Resolved => item.StartedAt is not null && item.ConcludedAt is not null && item.OutcomeId is not null,
            DynamicWorldEventLifecycleState.Expired => item.ConcludedAt is not null && item.OutcomeId is null,
            DynamicWorldEventLifecycleState.Cancelled => item.StartedAt is null && item.ConcludedAt is not null && item.OutcomeId is null,
            _ => false,
        };
        return shape ? DynamicWorldEventResult.Success() : DynamicWorldEventResult.Failure(
            DynamicWorldEventErrorCodes.InvalidLifecycle, "Dynamic World Event lifecycle fields are inconsistent.");
    }

    private IReadOnlyList<DynamicWorldEventSnapshot> Query(Func<DynamicWorldEventSnapshot, bool> predicate) => records.Values
        .Where(predicate).OrderBy(item => item.CreatedAt.Value).ThenBy(item => item.Id.Value).ToArray();
    private DynamicWorldEventResult Publish(DynamicWorldEventNotification value)
    {
        if (events is null) return DynamicWorldEventResult.Success();
        var result = events.Publish(value);
        return result.IsSuccess ? result : DynamicWorldEventResult.Failure(
            DynamicWorldEventErrorCodes.EventPublicationFailed, result.Error!.Message);
    }
    private static IReadOnlyList<EntityId> CanonicalParticipants(IReadOnlyList<EntityId>? values) =>
        values?.Distinct().OrderBy(id => id.Value).ToArray() ?? [];
    private static IReadOnlyDictionary<string, string> CanonicalAttributes(IReadOnlyDictionary<string, string>? values) =>
        new SortedDictionary<string, string>((values ?? new Dictionary<string, string>())
            .ToDictionary(item => item.Key, item => item.Value), StringComparer.Ordinal);
    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static bool ValidOptional(string? value) => value is null || Valid(value);
    private static string? Normalize(string? value) => value?.Trim();
    private static DynamicWorldEventResult AsResult(DynamicWorldEventError error) =>
        DynamicWorldEventResult.Failure(error.Code, error.Message);
    private static DynamicWorldEventResult<T> Fail<T>(DynamicWorldEventError error) =>
        DynamicWorldEventResult<T>.Failure(error.Code, error.Message);
}
