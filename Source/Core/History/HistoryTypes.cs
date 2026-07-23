using System.Collections.ObjectModel;
using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.History;

public readonly record struct HistoryEntryId(Guid Value) { public override string ToString() => Value.ToString("D"); }
public interface IHistoryEntryIdGenerator { HistoryEntryId Create(); }
public sealed class Version7HistoryEntryIdGenerator : IHistoryEntryIdGenerator
{
    public HistoryEntryId Create() => new(Guid.CreateVersion7());
}

public readonly record struct HistoryTypeId
{
    public HistoryTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record HistoryEntrySnapshot
{
    public HistoryEntrySnapshot(HistoryEntryId id, HistoryTypeId typeId, WorldTimestamp occurredAt,
        IReadOnlyList<EntityId>? participantEntityIds, EntityId? regionEntityId, int importance,
        IReadOnlyDictionary<string, string>? metadata, string? sourceEventReference, string? provenanceReference)
    {
        Id = id;
        TypeId = typeId;
        OccurredAt = occurredAt;
        ParticipantEntityIds = participantEntityIds is null ? null : Array.AsReadOnly(participantEntityIds.OrderBy(item => item.Value).ToArray());
        RegionEntityId = regionEntityId;
        Importance = importance;
        Metadata = metadata is null ? null : new ReadOnlyDictionary<string, string>(new SortedDictionary<string, string>(
            metadata.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal));
        SourceEventReference = sourceEventReference;
        ProvenanceReference = provenanceReference;
    }

    public HistoryEntryId Id { get; }
    public HistoryTypeId TypeId { get; }
    public WorldTimestamp OccurredAt { get; }
    public IReadOnlyList<EntityId>? ParticipantEntityIds { get; }
    public EntityId? RegionEntityId { get; }
    public int Importance { get; }
    public IReadOnlyDictionary<string, string>? Metadata { get; }
    public string? SourceEventReference { get; }
    public string? ProvenanceReference { get; }
}

public sealed record WorldHistorySnapshot
{
    public const int CurrentVersion = 1;
    public WorldHistorySnapshot(int version, IReadOnlyList<HistoryEntrySnapshot>? entries)
    {
        Version = version;
        Entries = entries is null ? null : Array.AsReadOnly(entries.ToArray());
    }
    public int Version { get; }
    public IReadOnlyList<HistoryEntrySnapshot>? Entries { get; }
}

public sealed record HistoryDomainEvent(string Type, HistoryEntryId EntryId, HistoryTypeId HistoryTypeId,
    WorldTimestamp OccurredAt, EntityId? RegionEntityId);
public interface IHistoryEventSink { HistoryResult Publish(HistoryDomainEvent domainEvent); }
public sealed record HistoryDiagnostic(HistoryEntrySnapshot Entry, string ValidationStatus);

