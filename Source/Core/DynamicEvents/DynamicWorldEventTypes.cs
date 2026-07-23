using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.DynamicEvents;

public readonly record struct DynamicWorldEventId(Guid Value) { public override string ToString() => Value.ToString("D"); }
public interface IDynamicWorldEventIdGenerator { DynamicWorldEventId Create(); }
public sealed class Version7DynamicWorldEventIdGenerator : IDynamicWorldEventIdGenerator
{
    public DynamicWorldEventId Create() => new(Guid.CreateVersion7());
}
public readonly record struct DynamicWorldEventTypeId
{
    public DynamicWorldEventTypeId(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); Value = value.Trim(); }
    public string Value { get; }
    public override string ToString() => Value;
}
public readonly record struct DynamicWorldEventOutcomeId
{
    public DynamicWorldEventOutcomeId(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); Value = value.Trim(); }
    public string Value { get; }
    public override string ToString() => Value;
}
public enum DynamicWorldEventLifecycleState { Scheduled, Active, Resolved, Expired, Cancelled }
public sealed record DynamicWorldEventSnapshot
{
    public DynamicWorldEventSnapshot(DynamicWorldEventId id, DynamicWorldEventTypeId typeId,
        DynamicWorldEventLifecycleState lifecycleState, WorldTimestamp createdAt, WorldTimestamp? scheduledStartAt,
        WorldTimestamp? startedAt, WorldTimestamp? concludedAt, EntityId? regionEntityId,
        IReadOnlyList<EntityId>? participantEntityIds, IReadOnlyDictionary<string, string>? attributes,
        DynamicWorldEventOutcomeId? outcomeId, string? sourceReference, string? provenanceReference)
    {
        Id = id; TypeId = typeId; LifecycleState = lifecycleState; CreatedAt = createdAt;
        ScheduledStartAt = scheduledStartAt; StartedAt = startedAt; ConcludedAt = concludedAt;
        RegionEntityId = regionEntityId;
        ParticipantEntityIds = participantEntityIds is null ? null : Array.AsReadOnly(participantEntityIds.ToArray());
        Attributes = attributes is null ? null : new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            new SortedDictionary<string, string>(attributes.ToDictionary(item => item.Key, item => item.Value), StringComparer.Ordinal));
        OutcomeId = outcomeId; SourceReference = sourceReference; ProvenanceReference = provenanceReference;
    }
    public DynamicWorldEventId Id { get; }
    public DynamicWorldEventTypeId TypeId { get; }
    public DynamicWorldEventLifecycleState LifecycleState { get; }
    public WorldTimestamp CreatedAt { get; }
    public WorldTimestamp? ScheduledStartAt { get; }
    public WorldTimestamp? StartedAt { get; }
    public WorldTimestamp? ConcludedAt { get; }
    public EntityId? RegionEntityId { get; }
    public IReadOnlyList<EntityId>? ParticipantEntityIds { get; }
    public IReadOnlyDictionary<string, string>? Attributes { get; }
    public DynamicWorldEventOutcomeId? OutcomeId { get; }
    public string? SourceReference { get; }
    public string? ProvenanceReference { get; }
}
public sealed record DynamicWorldEventFrameworkSnapshot
{
    public const int CurrentVersion = 1;
    public DynamicWorldEventFrameworkSnapshot(int version, IReadOnlyList<DynamicWorldEventSnapshot>? events)
    {
        Version = version;
        Events = events is null ? null : Array.AsReadOnly(events.ToArray());
    }
    public int Version { get; }
    public IReadOnlyList<DynamicWorldEventSnapshot>? Events { get; }
}
public sealed record DynamicWorldEventNotification(string Type, DynamicWorldEventId EventId,
    WorldTimestamp OccurredAt, EntityId? RegionEntityId = null);
public interface IDynamicWorldEventSink { DynamicWorldEventResult Publish(DynamicWorldEventNotification notification); }
public sealed record DynamicWorldEventDiagnostic(DynamicWorldEventSnapshot Event, string ValidationStatus);
