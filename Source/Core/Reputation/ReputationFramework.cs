using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Reputation;

/// <summary>Owns audience-scoped assessments without deriving behavior or direct relationships.</summary>
public sealed class ReputationFramework
{
    public const int MinimumValue = -1000;
    public const int MaximumValue = 1000;
    private readonly EntityRegistry entities;
    private readonly IReputationIdGenerator idGenerator;
    private readonly IReputationEventSink? events;
    private Dictionary<ReputationId, ReputationSnapshot> records = [];

    public ReputationFramework(EntityRegistry entities, IReputationIdGenerator? idGenerator = null, IReputationEventSink? events = null)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.idGenerator = idGenerator ?? new Version7ReputationIdGenerator();
        this.events = events;
    }
    public int Count => records.Count;

    public ReputationResult<ReputationSnapshot> Create(EntityId subject, ReputationAudienceTypeId audienceType,
        EntityId? audienceEntity, ReputationDimensionId dimension, int value, WorldTimestamp timestamp,
        string? provenanceReference = null)
    {
        var input = ValidateInputs(subject, audienceType, audienceEntity, dimension, value, provenanceReference);
        if (!input.IsSuccess) return Fail<ReputationSnapshot>(input.Error!);
        if (FindActive(subject, audienceType, audienceEntity, dimension).IsSuccess)
            return ReputationResult<ReputationSnapshot>.Failure(ReputationErrorCodes.DuplicateActiveKey, "An active Reputation record already exists for this key.");
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = idGenerator.Create();
            if (id.Value == Guid.Empty || records.ContainsKey(id)) continue;
            var record = new ReputationSnapshot(id, subject, audienceType, audienceEntity, dimension, value,
                ReputationLifecycleState.Active, timestamp, timestamp, Normalize(provenanceReference));
            var published = Publish(new("ReputationCreated", id, subject, timestamp, null, value));
            if (!published.IsSuccess) return Fail<ReputationSnapshot>(published.Error!);
            records.Add(id, record);
            return ReputationResult<ReputationSnapshot>.Success(record);
        }
        return ReputationResult<ReputationSnapshot>.Failure(ReputationErrorCodes.DuplicateId, "The Reputation ID generator did not produce a unique initialized ID.");
    }

    public ReputationResult<ReputationSnapshot> Find(ReputationId id) => records.TryGetValue(id, out var value)
        ? ReputationResult<ReputationSnapshot>.Success(value)
        : ReputationResult<ReputationSnapshot>.Failure(ReputationErrorCodes.NotFound, "Reputation record was not found.");

    public ReputationResult<ReputationSnapshot> FindActive(EntityId subject, ReputationAudienceTypeId audienceType,
        EntityId? audienceEntity, ReputationDimensionId dimension)
    {
        var found = records.Values.FirstOrDefault(item => item.LifecycleState == ReputationLifecycleState.Active &&
            item.SubjectEntityId == subject && item.AudienceTypeId == audienceType && item.AudienceEntityId == audienceEntity && item.DimensionId == dimension);
        return found is null ? ReputationResult<ReputationSnapshot>.Failure(ReputationErrorCodes.NotFound, "Active Reputation record was not found.")
            : ReputationResult<ReputationSnapshot>.Success(found);
    }

    public ReputationResult SetValue(ReputationId id, int value, WorldTimestamp timestamp, string? provenanceReference = null) =>
        Mutate(id, timestamp, provenanceReference, _ => value);
    public ReputationResult ApplyDelta(ReputationId id, int delta, WorldTimestamp timestamp, string? provenanceReference = null) =>
        Mutate(id, timestamp, provenanceReference, current => checked(current + delta));

    public ReputationResult Retire(ReputationId id, WorldTimestamp timestamp, string? provenanceReference = null)
    {
        var found = FindMutable(id, timestamp, provenanceReference);
        if (!found.IsSuccess) return ReputationResult.Failure(found.Error!.Code, found.Error.Message);
        var current = found.Value!;
        var next = current with
        {
            LifecycleState = ReputationLifecycleState.Retired,
            LastChangedAt = timestamp,
            ProvenanceReference = Normalize(provenanceReference)
        };
        var published = Publish(new("ReputationRetired", id, current.SubjectEntityId, timestamp, current.Value, null));
        if (!published.IsSuccess) return published;
        records[id] = next;
        return ReputationResult.Success();
    }

    public IReadOnlyList<ReputationSnapshot> QueryBySubject(EntityId id) => Query(item => item.SubjectEntityId == id);
    public IReadOnlyList<ReputationSnapshot> QueryByAudienceType(ReputationAudienceTypeId id) => Query(item => item.AudienceTypeId == id);
    public IReadOnlyList<ReputationSnapshot> QueryByAudience(EntityId id) => Query(item => item.AudienceEntityId == id);
    public IReadOnlyList<ReputationSnapshot> QueryByDimension(ReputationDimensionId id) => Query(item => item.DimensionId == id);
    public IReadOnlyList<ReputationSnapshot> QueryInvolving(EntityId id) => Query(item => item.SubjectEntityId == id || item.AudienceEntityId == id);

    public ReputationResult ValidateReferences()
    {
        foreach (var item in records.Values.OrderBy(item => item.Id.Value))
        {
            var valid = ValidateRecord(item);
            if (!valid.IsSuccess) return valid;
        }
        return ValidateUniqueKeys(records.Values);
    }

    public ReputationResult<ReputationDiagnostic> Inspect(ReputationId id)
    {
        var found = Find(id);
        if (!found.IsSuccess) return ReputationResult<ReputationDiagnostic>.Failure(found.Error!.Code, found.Error.Message);
        var valid = ValidateRecord(found.Value!);
        return ReputationResult<ReputationDiagnostic>.Success(new(found.Value!, valid.IsSuccess ? "valid" : $"{valid.Error!.Code}: {valid.Error.Message}"));
    }

    public ReputationFrameworkSnapshot ExportSnapshot() => new(ReputationFrameworkSnapshot.CurrentVersion,
        records.Values.OrderBy(item => item.Id.Value).ToArray());

    public ReputationResult RestoreSnapshot(ReputationFrameworkSnapshot? snapshot)
    {
        if (snapshot is null) return ReputationResult.Failure(ReputationErrorCodes.InvalidSnapshot, "Reputation snapshot cannot be null.");
        if (snapshot.Version != ReputationFrameworkSnapshot.CurrentVersion)
            return ReputationResult.Failure(ReputationErrorCodes.UnsupportedSnapshotVersion, "Reputation snapshot version is unsupported.");
        if (snapshot.Records is null || snapshot.Records.Any(item => item is null))
            return ReputationResult.Failure(ReputationErrorCodes.InvalidSnapshot, "Reputation records cannot be null or contain null values.");
        var candidate = new Dictionary<ReputationId, ReputationSnapshot>();
        foreach (var item in snapshot.Records)
        {
            if (!candidate.TryAdd(item.Id, item)) return ReputationResult.Failure(ReputationErrorCodes.DuplicateId, "Snapshot contains duplicate Reputation IDs.");
            var valid = ValidateRecord(item);
            if (!valid.IsSuccess) return valid;
        }
        var keys = ValidateUniqueKeys(candidate.Values);
        if (!keys.IsSuccess) return keys;
        records = candidate;
        return ReputationResult.Success();
    }

    private ReputationResult Mutate(ReputationId id, WorldTimestamp timestamp, string? provenance, Func<int, int> change)
    {
        var found = FindMutable(id, timestamp, provenance);
        if (!found.IsSuccess) return ReputationResult.Failure(found.Error!.Code, found.Error.Message);
        var current = found.Value!;
        int value;
        try { value = change(current.Value); }
        catch (OverflowException) { return ReputationResult.Failure(ReputationErrorCodes.InvalidValue, "Reputation arithmetic overflowed."); }
        if (value is < MinimumValue or > MaximumValue) return ReputationResult.Failure(ReputationErrorCodes.InvalidValue, "Reputation value is outside range.");
        var next = current with { Value = value, LastChangedAt = timestamp, ProvenanceReference = Normalize(provenance) };
        var published = Publish(new("ReputationChanged", id, current.SubjectEntityId, timestamp, current.Value, value));
        if (!published.IsSuccess) return published;
        records[id] = next;
        return ReputationResult.Success();
    }

    private ReputationResult<ReputationSnapshot> FindMutable(ReputationId id, WorldTimestamp timestamp, string? provenance)
    {
        var found = Find(id);
        if (!found.IsSuccess) return found;
        if (found.Value!.LifecycleState != ReputationLifecycleState.Active)
            return ReputationResult<ReputationSnapshot>.Failure(ReputationErrorCodes.InvalidLifecycle, "Only active Reputation can change.");
        if (timestamp.Value < found.Value.LastChangedAt.Value)
            return ReputationResult<ReputationSnapshot>.Failure(ReputationErrorCodes.InvalidTimestamp, "Change cannot precede the previous change.");
        if (!ValidOptional(provenance)) return ReputationResult<ReputationSnapshot>.Failure(ReputationErrorCodes.InvalidIdentifier, "Provenance must be normalized.");
        return found;
    }

    private ReputationResult ValidateRecord(ReputationSnapshot item)
    {
        var input = ValidateInputs(item.SubjectEntityId, item.AudienceTypeId, item.AudienceEntityId,
            item.DimensionId, item.Value, item.ProvenanceReference);
        if (!input.IsSuccess) return input;
        if (item.Id.Value == Guid.Empty) return ReputationResult.Failure(ReputationErrorCodes.InvalidIdentifier, "Reputation ID must be initialized.");
        if (!Enum.IsDefined(item.LifecycleState)) return ReputationResult.Failure(ReputationErrorCodes.InvalidLifecycle, "Reputation lifecycle is invalid.");
        if (item.CreatedAt.Value > item.LastChangedAt.Value) return ReputationResult.Failure(ReputationErrorCodes.InvalidTimestamp, "Creation cannot follow last change.");
        return ReputationResult.Success();
    }

    private ReputationResult ValidateInputs(EntityId subject, ReputationAudienceTypeId audienceType, EntityId? audience,
        ReputationDimensionId dimension, int value, string? provenance)
    {
        if (!entities.Exists(subject) || audience is { } audienceId && !entities.Exists(audienceId))
            return ReputationResult.Failure(ReputationErrorCodes.InvalidReference, "Reputation Entity references must remain registered.");
        if (!Valid(audienceType.Value) || !Valid(dimension.Value) || !ValidOptional(provenance))
            return ReputationResult.Failure(ReputationErrorCodes.InvalidIdentifier, "Reputation identifiers must be normalized.");
        if (value is < MinimumValue or > MaximumValue) return ReputationResult.Failure(ReputationErrorCodes.InvalidValue, "Reputation value is outside range.");
        return ReputationResult.Success();
    }

    private static ReputationResult ValidateUniqueKeys(IEnumerable<ReputationSnapshot> items) => items
        .Where(item => item.LifecycleState == ReputationLifecycleState.Active)
        .GroupBy(item => (item.SubjectEntityId, item.AudienceTypeId, item.AudienceEntityId, item.DimensionId))
        .Any(group => group.Count() > 1)
        ? ReputationResult.Failure(ReputationErrorCodes.DuplicateActiveKey, "Snapshot contains duplicate active Reputation keys.")
        : ReputationResult.Success();
    private IReadOnlyList<ReputationSnapshot> Query(Func<ReputationSnapshot, bool> predicate) => records.Values.Where(predicate)
        .OrderBy(item => item.Id.Value).ToArray();
    private ReputationResult Publish(ReputationDomainEvent value)
    {
        if (events is null) return ReputationResult.Success();
        var result = events.Publish(value);
        return result.IsSuccess ? result : ReputationResult.Failure(ReputationErrorCodes.EventPublicationFailed, result.Error!.Message);
    }
    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static bool ValidOptional(string? value) => value is null || Valid(value);
    private static string? Normalize(string? value) => value?.Trim();
    private static ReputationResult<T> Fail<T>(ReputationError error) => ReputationResult<T>.Failure(error.Code, error.Message);
}
