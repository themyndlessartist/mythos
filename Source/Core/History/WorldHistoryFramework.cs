using Mythos.Framework.Entities;
using Mythos.Framework.Regions;
using Mythos.Framework.Time;

namespace Mythos.Framework.History;

/// <summary>Owns immutable, append-only records of meaningful world changes.</summary>
public sealed class WorldHistoryFramework
{
    public const int MinimumImportance = 0;
    public const int MaximumImportance = 1000;

    private readonly EntityRegistry entities;
    private readonly RegionFramework regions;
    private readonly IHistoryEntryIdGenerator idGenerator;
    private readonly IHistoryEventSink? events;
    private Dictionary<HistoryEntryId, HistoryEntrySnapshot> entries = [];

    public WorldHistoryFramework(EntityRegistry entities, RegionFramework regions,
        IHistoryEntryIdGenerator? idGenerator = null, IHistoryEventSink? events = null)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
        this.idGenerator = idGenerator ?? new Version7HistoryEntryIdGenerator();
        this.events = events;
    }

    public int Count => entries.Count;

    public HistoryResult<HistoryEntrySnapshot> Record(HistoryTypeId typeId, WorldTimestamp occurredAt,
        IReadOnlyList<EntityId>? participantEntityIds = null, EntityId? regionEntityId = null, int importance = 0,
        IReadOnlyDictionary<string, string>? metadata = null, string? sourceEventReference = null,
        string? provenanceReference = null)
    {
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = idGenerator.Create();
            if (id.Value == Guid.Empty || entries.ContainsKey(id)) continue;
            var entry = new HistoryEntrySnapshot(id, typeId, occurredAt, participantEntityIds ?? [], regionEntityId,
                importance, metadata ?? new Dictionary<string, string>(), Normalize(sourceEventReference), Normalize(provenanceReference));
            var valid = ValidateEntry(entry);
            if (!valid.IsSuccess) return Fail<HistoryEntrySnapshot>(valid.Error!);
            if (entry.SourceEventReference is not null && entries.Values.Any(item => item.SourceEventReference == entry.SourceEventReference))
                return HistoryResult<HistoryEntrySnapshot>.Failure(HistoryErrorCodes.DuplicateSource, "Source event is already represented in history.");
            var published = Publish(new("HistoryRecorded", id, typeId, occurredAt, regionEntityId));
            if (!published.IsSuccess) return Fail<HistoryEntrySnapshot>(published.Error!);
            entries.Add(id, entry);
            return HistoryResult<HistoryEntrySnapshot>.Success(entry);
        }
        return HistoryResult<HistoryEntrySnapshot>.Failure(HistoryErrorCodes.DuplicateId,
            "The History ID generator did not produce a unique initialized ID.");
    }

    public HistoryResult<HistoryEntrySnapshot> Find(HistoryEntryId id) => entries.TryGetValue(id, out var entry)
        ? HistoryResult<HistoryEntrySnapshot>.Success(entry)
        : HistoryResult<HistoryEntrySnapshot>.Failure(HistoryErrorCodes.NotFound, "History Entry was not found.");

    public IReadOnlyList<HistoryEntrySnapshot> Timeline() => Query(_ => true);
    public IReadOnlyList<HistoryEntrySnapshot> QueryByType(HistoryTypeId id) => Query(item => item.TypeId == id);
    public IReadOnlyList<HistoryEntrySnapshot> QueryByParticipant(EntityId id) => Query(item => item.ParticipantEntityIds!.Contains(id));
    public IReadOnlyList<HistoryEntrySnapshot> QueryByRegion(EntityId id) => Query(item => item.RegionEntityId == id);
    public IReadOnlyList<HistoryEntrySnapshot> QueryByRange(WorldTimestamp from, WorldTimestamp through) =>
        from.Value > through.Value ? [] : Query(item => item.OccurredAt.Value >= from.Value && item.OccurredAt.Value <= through.Value);
    public IReadOnlyList<HistoryEntrySnapshot> QueryMinimumImportance(int importance) =>
        importance is < MinimumImportance or > MaximumImportance ? [] : Query(item => item.Importance >= importance);
    public HistoryResult<HistoryEntrySnapshot> FindBySource(string sourceEventReference)
    {
        if (!Valid(sourceEventReference)) return HistoryResult<HistoryEntrySnapshot>.Failure(HistoryErrorCodes.InvalidIdentifier, "Source reference must be normalized.");
        var found = entries.Values.FirstOrDefault(item => item.SourceEventReference == sourceEventReference);
        return found is null ? HistoryResult<HistoryEntrySnapshot>.Failure(HistoryErrorCodes.NotFound, "History source was not found.")
            : HistoryResult<HistoryEntrySnapshot>.Success(found);
    }

    public HistoryResult ValidateReferences()
    {
        foreach (var item in entries.Values.OrderBy(item => item.OccurredAt.Value).ThenBy(item => item.Id.Value))
        {
            var valid = ValidateEntry(item);
            if (!valid.IsSuccess) return valid;
        }
        if (entries.Values.Where(item => item.SourceEventReference is not null).GroupBy(item => item.SourceEventReference, StringComparer.Ordinal).Any(group => group.Count() > 1))
            return HistoryResult.Failure(HistoryErrorCodes.DuplicateSource, "History contains duplicate source-event references.");
        return HistoryResult.Success();
    }

    public HistoryResult<HistoryDiagnostic> Inspect(HistoryEntryId id)
    {
        var found = Find(id);
        if (!found.IsSuccess) return HistoryResult<HistoryDiagnostic>.Failure(found.Error!.Code, found.Error.Message);
        var valid = ValidateEntry(found.Value!);
        return HistoryResult<HistoryDiagnostic>.Success(new(found.Value!, valid.IsSuccess ? "valid" : $"{valid.Error!.Code}: {valid.Error.Message}"));
    }

    public WorldHistorySnapshot ExportSnapshot() => new(WorldHistorySnapshot.CurrentVersion, Timeline());

    public HistoryResult RestoreSnapshot(WorldHistorySnapshot? snapshot)
    {
        if (snapshot is null) return HistoryResult.Failure(HistoryErrorCodes.InvalidSnapshot, "History snapshot cannot be null.");
        if (snapshot.Version != WorldHistorySnapshot.CurrentVersion)
            return HistoryResult.Failure(HistoryErrorCodes.UnsupportedSnapshotVersion, "History snapshot version is unsupported.");
        if (snapshot.Entries is null || snapshot.Entries.Any(item => item is null))
            return HistoryResult.Failure(HistoryErrorCodes.InvalidSnapshot, "History entries cannot be null or contain null records.");
        var candidate = new Dictionary<HistoryEntryId, HistoryEntrySnapshot>();
        foreach (var item in snapshot.Entries)
        {
            if (!candidate.TryAdd(item.Id, Canonical(item)))
                return HistoryResult.Failure(HistoryErrorCodes.DuplicateId, "History snapshot contains duplicate Entry IDs.");
            var valid = ValidateEntry(item);
            if (!valid.IsSuccess) return valid;
        }
        if (candidate.Values.Where(item => item.SourceEventReference is not null)
            .GroupBy(item => item.SourceEventReference, StringComparer.Ordinal).Any(group => group.Count() > 1))
            return HistoryResult.Failure(HistoryErrorCodes.DuplicateSource, "History snapshot contains duplicate source-event references.");
        entries = candidate;
        return HistoryResult.Success();
    }

    private HistoryResult ValidateEntry(HistoryEntrySnapshot item)
    {
        if (item.Id.Value == Guid.Empty || !Valid(item.TypeId.Value) || !ValidOptional(item.SourceEventReference) || !ValidOptional(item.ProvenanceReference))
            return HistoryResult.Failure(HistoryErrorCodes.InvalidIdentifier, "History identifiers and references must be initialized and normalized.");
        if (item.Importance is < MinimumImportance or > MaximumImportance)
            return HistoryResult.Failure(HistoryErrorCodes.InvalidEntry, "History importance is outside the approved range.");
        if (item.ParticipantEntityIds is null || item.Metadata is null ||
            item.ParticipantEntityIds.Count == 0 && item.RegionEntityId is null && item.Metadata.Count == 0)
            return HistoryResult.Failure(HistoryErrorCodes.InvalidEntry, "History requires participants, a Region, or metadata.");
        if (item.ParticipantEntityIds.Any(id => id.Value == Guid.Empty || !entities.Exists(id)) ||
            item.ParticipantEntityIds.Distinct().Count() != item.ParticipantEntityIds.Count)
            return HistoryResult.Failure(HistoryErrorCodes.InvalidReference, "History participants must be unique registered Entities.");
        if (item.RegionEntityId is { } region && !regions.Find(region).IsSuccess)
            return HistoryResult.Failure(HistoryErrorCodes.InvalidReference, "History Region reference was not found.");
        if (item.Metadata.Any(pair => !Valid(pair.Key) || !Valid(pair.Value)))
            return HistoryResult.Failure(HistoryErrorCodes.InvalidEntry, "History metadata must be normalized.");
        return HistoryResult.Success();
    }

    private IReadOnlyList<HistoryEntrySnapshot> Query(Func<HistoryEntrySnapshot, bool> predicate) => entries.Values.Where(predicate)
        .OrderBy(item => item.OccurredAt.Value).ThenBy(item => item.Id.Value).ToArray();
    private HistoryResult Publish(HistoryDomainEvent value)
    {
        if (events is null) return HistoryResult.Success();
        var result = events.Publish(value);
        return result.IsSuccess ? result : HistoryResult.Failure(HistoryErrorCodes.EventPublicationFailed, result.Error!.Message);
    }
    private static HistoryEntrySnapshot Canonical(HistoryEntrySnapshot item) => new(item.Id, item.TypeId, item.OccurredAt,
        item.ParticipantEntityIds, item.RegionEntityId, item.Importance, item.Metadata, item.SourceEventReference, item.ProvenanceReference);
    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static bool ValidOptional(string? value) => value is null || Valid(value);
    private static string? Normalize(string? value) => value?.Trim();
    private static HistoryResult<T> Fail<T>(HistoryError error) => HistoryResult<T>.Failure(error.Code, error.Message);
}
