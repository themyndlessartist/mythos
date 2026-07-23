using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Information;

/// <summary>Separates immutable propositions, authoritative facts, and entity awareness.</summary>
public sealed class InformationFramework
{
    public const int MinimumConfidence = 0;
    public const int MaximumConfidence = 1000;

    private readonly EntityRegistry entities;
    private readonly IInformationIdGenerator idGenerator;
    private readonly IInformationEventSink? events;
    private Dictionary<InformationId, InformationRecordSnapshot> information = [];
    private Dictionary<FactId, FactSnapshot> facts = [];
    private Dictionary<(EntityId Knower, InformationId Information), AwarenessSnapshot> awareness = [];

    public InformationFramework(EntityRegistry entities, IInformationIdGenerator? idGenerator = null, IInformationEventSink? events = null)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.idGenerator = idGenerator ?? new Version7InformationIdGenerator();
        this.events = events;
    }

    public int InformationCount => information.Count;
    public int FactCount => facts.Count;
    public int AwarenessCount => awareness.Count;

    public InformationResult<InformationRecordSnapshot> Create(InformationTypeId typeId, EntityId? subjectEntityId,
        EntityId? objectEntityId, IReadOnlyDictionary<string, string>? attributes, WorldTimestamp createdAt,
        string? provenanceReference = null)
    {
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = idGenerator.CreateInformationId();
            if (id.Value == Guid.Empty || information.ContainsKey(id)) continue;
            var record = new InformationRecordSnapshot(id, typeId, subjectEntityId, objectEntityId,
                attributes ?? new Dictionary<string, string>(),
                createdAt, NormalizeOptional(provenanceReference));
            var valid = ValidateInformation(record);
            if (!valid.IsSuccess) return Fail<InformationRecordSnapshot>(valid.Error!);
            var published = Publish(new("InformationCreated", id, createdAt));
            if (!published.IsSuccess) return Fail<InformationRecordSnapshot>(published.Error!);
            information.Add(id, record);
            return InformationResult<InformationRecordSnapshot>.Success(record);
        }
        return InformationResult<InformationRecordSnapshot>.Failure(InformationErrorCodes.DuplicateId,
            "The information ID generator did not produce a unique initialized ID.");
    }

    public InformationResult<InformationRecordSnapshot> Find(InformationId id) => information.TryGetValue(id, out var value)
        ? InformationResult<InformationRecordSnapshot>.Success(value)
        : InformationResult<InformationRecordSnapshot>.Failure(InformationErrorCodes.NotFound, "Information was not found.");

    public IReadOnlyList<InformationRecordSnapshot> QueryByType(InformationTypeId id) => QueryInformation(item => item.TypeId == id);
    public IReadOnlyList<InformationRecordSnapshot> QueryBySubject(EntityId id) => QueryInformation(item => item.SubjectEntityId == id);
    public IReadOnlyList<InformationRecordSnapshot> QueryByObject(EntityId id) => QueryInformation(item => item.ObjectEntityId == id);
    public IReadOnlyList<InformationRecordSnapshot> QueryInvolving(EntityId id) => QueryInformation(item => item.SubjectEntityId == id || item.ObjectEntityId == id);

    public InformationResult<FactSnapshot> DeclareFact(InformationId informationId, WorldTimestamp effectiveAt,
        string? provenanceReference = null)
    {
        if (!information.TryGetValue(informationId, out var record))
            return InformationResult<FactSnapshot>.Failure(InformationErrorCodes.InvalidReference, "Fact requires existing Information.");
        if (facts.Values.Any(item => item.InformationId == informationId))
            return InformationResult<FactSnapshot>.Failure(InformationErrorCodes.DuplicateFact, "Information already has a Fact declaration.");
        if (effectiveAt.Value < record.CreatedAt.Value)
            return InformationResult<FactSnapshot>.Failure(InformationErrorCodes.InvalidTimestamp, "Fact cannot take effect before Information creation.");
        if (!ValidOptional(provenanceReference))
            return InformationResult<FactSnapshot>.Failure(InformationErrorCodes.InvalidIdentifier, "Fact provenance must be normalized.");
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = idGenerator.CreateFactId();
            if (id.Value == Guid.Empty || facts.ContainsKey(id)) continue;
            var fact = new FactSnapshot(id, informationId, effectiveAt, NormalizeOptional(provenanceReference));
            var published = Publish(new("FactDeclared", informationId, effectiveAt, id));
            if (!published.IsSuccess) return Fail<FactSnapshot>(published.Error!);
            facts.Add(id, fact);
            return InformationResult<FactSnapshot>.Success(fact);
        }
        return InformationResult<FactSnapshot>.Failure(InformationErrorCodes.DuplicateId,
            "The Fact ID generator did not produce a unique initialized ID.");
    }

    public InformationResult<FactSnapshot> FindFact(FactId id) => facts.TryGetValue(id, out var value)
        ? InformationResult<FactSnapshot>.Success(value)
        : InformationResult<FactSnapshot>.Failure(InformationErrorCodes.NotFound, "Fact was not found.");

    public InformationResult<FactSnapshot> FindFactFor(InformationId id)
    {
        var fact = facts.Values.FirstOrDefault(item => item.InformationId == id);
        return fact is null ? InformationResult<FactSnapshot>.Failure(InformationErrorCodes.NotFound, "Fact was not found.")
            : InformationResult<FactSnapshot>.Success(fact);
    }

    public bool IsAuthoritative(InformationId id) => facts.Values.Any(item => item.InformationId == id);
    public IReadOnlyList<FactSnapshot> QueryFacts() => facts.Values.OrderBy(item => item.Id.Value).ToArray();

    public InformationResult SetAwareness(EntityId knower, InformationId informationId, EpistemicStance stance,
        int confidence, WorldTimestamp timestamp, EntityId? sourceEntityId = null, string? provenanceReference = null)
    {
        if (!ValidEntity(knower) || sourceEntityId is { } source && !ValidEntity(source))
            return InformationResult.Failure(InformationErrorCodes.InvalidReference, "Awareness Entity references must resolve and not be Destroyed.");
        if (!information.ContainsKey(informationId))
            return InformationResult.Failure(InformationErrorCodes.InvalidReference, "Awareness requires existing Information.");
        if (!Enum.IsDefined(stance) || confidence is < MinimumConfidence or > MaximumConfidence)
            return InformationResult.Failure(InformationErrorCodes.InvalidAwareness, "Awareness stance or confidence is invalid.");
        if (stance == EpistemicStance.Known && !IsAuthoritative(informationId))
            return InformationResult.Failure(InformationErrorCodes.InvalidAwareness, "Known awareness requires an authoritative Fact.");
        if (!ValidOptional(provenanceReference))
            return InformationResult.Failure(InformationErrorCodes.InvalidIdentifier, "Awareness provenance must be normalized.");
        var key = (knower, informationId);
        awareness.TryGetValue(key, out var existing);
        if (existing is not null && timestamp.Value < existing.LastUpdatedAt.Value)
            return InformationResult.Failure(InformationErrorCodes.InvalidTimestamp, "Awareness update cannot precede its previous update.");
        var next = new AwarenessSnapshot(knower, informationId, stance, confidence, existing?.AcquiredAt ?? timestamp,
            timestamp, sourceEntityId, NormalizeOptional(provenanceReference));
        var valid = ValidateAwareness(next);
        if (!valid.IsSuccess) return valid;
        var published = Publish(new(existing is null ? "AwarenessCreated" : "AwarenessChanged", informationId,
            timestamp, KnowerEntityId: knower, PreviousStance: existing?.Stance, CurrentStance: stance));
        if (!published.IsSuccess) return published;
        awareness[key] = next;
        return InformationResult.Success();
    }

    public InformationResult<AwarenessSnapshot> FindAwareness(EntityId knower, InformationId informationId) =>
        awareness.TryGetValue((knower, informationId), out var value)
            ? InformationResult<AwarenessSnapshot>.Success(value)
            : InformationResult<AwarenessSnapshot>.Failure(InformationErrorCodes.NotFound, "Awareness was not found.");

    public IReadOnlyList<AwarenessSnapshot> QueryAwarenessByKnower(EntityId id) => QueryAwareness(item => item.KnowerEntityId == id);
    public IReadOnlyList<AwarenessSnapshot> QueryAwarenessFor(InformationId id) => QueryAwareness(item => item.InformationId == id);
    public IReadOnlyList<AwarenessSnapshot> QueryAwarenessByStance(EpistemicStance stance) => QueryAwareness(item => item.Stance == stance);
    public IReadOnlyList<AwarenessSnapshot> QueryAwarenessBySource(EntityId id) => QueryAwareness(item => item.SourceEntityId == id);

    public InformationResult Forget(EntityId knower, InformationId informationId, WorldTimestamp timestamp)
    {
        if (!awareness.TryGetValue((knower, informationId), out var existing))
            return InformationResult.Failure(InformationErrorCodes.NotFound, "Awareness was not found.");
        if (timestamp.Value < existing.LastUpdatedAt.Value)
            return InformationResult.Failure(InformationErrorCodes.InvalidTimestamp, "Forgetting cannot precede the last awareness update.");
        var published = Publish(new("AwarenessForgotten", informationId, timestamp, KnowerEntityId: knower,
            PreviousStance: existing.Stance));
        if (!published.IsSuccess) return published;
        awareness.Remove((knower, informationId));
        return InformationResult.Success();
    }

    public InformationResult ValidateReferences()
    {
        foreach (var item in information.Values.OrderBy(item => item.Id.Value))
        {
            var valid = ValidateInformation(item);
            if (!valid.IsSuccess) return valid;
        }
        foreach (var item in facts.Values.OrderBy(item => item.Id.Value))
        {
            var valid = ValidateFact(item);
            if (!valid.IsSuccess) return valid;
        }
        foreach (var item in awareness.Values.OrderBy(item => item.KnowerEntityId.Value).ThenBy(item => item.InformationId.Value))
        {
            var valid = ValidateAwareness(item);
            if (!valid.IsSuccess) return valid;
        }
        return InformationResult.Success();
    }

    public InformationResult<InformationDiagnostic> Inspect(InformationId id)
    {
        var found = Find(id);
        if (!found.IsSuccess) return InformationResult<InformationDiagnostic>.Failure(found.Error!.Code, found.Error.Message);
        var valid = ValidateInformation(found.Value!);
        var fact = facts.Values.FirstOrDefault(item => item.InformationId == id);
        return InformationResult<InformationDiagnostic>.Success(new(found.Value!, fact,
            QueryAwarenessFor(id), valid.IsSuccess ? "valid" : $"{valid.Error!.Code}: {valid.Error.Message}"));
    }

    public InformationFrameworkSnapshot ExportSnapshot() => new(InformationFrameworkSnapshot.CurrentVersion,
        information.Values.OrderBy(item => item.Id.Value).ToArray(), facts.Values.OrderBy(item => item.Id.Value).ToArray(),
        awareness.Values.OrderBy(item => item.KnowerEntityId.Value).ThenBy(item => item.InformationId.Value).ToArray());

    public InformationResult RestoreSnapshot(InformationFrameworkSnapshot? snapshot)
    {
        if (snapshot is null) return InformationResult.Failure(InformationErrorCodes.InvalidSnapshot, "Information snapshot cannot be null.");
        if (snapshot.Version != InformationFrameworkSnapshot.CurrentVersion)
            return InformationResult.Failure(InformationErrorCodes.UnsupportedSnapshotVersion, "Information snapshot version is unsupported.");
        if (snapshot.Information is null || snapshot.Facts is null || snapshot.Awareness is null ||
            snapshot.Information.Any(item => item is null) || snapshot.Facts.Any(item => item is null) || snapshot.Awareness.Any(item => item is null))
            return InformationResult.Failure(InformationErrorCodes.InvalidSnapshot, "Information snapshot collections cannot be null or contain null records.");

        var candidateInformation = new Dictionary<InformationId, InformationRecordSnapshot>();
        foreach (var item in snapshot.Information)
        {
            if (!candidateInformation.TryAdd(item.Id, Canonical(item)))
                return InformationResult.Failure(InformationErrorCodes.DuplicateId, "Snapshot contains duplicate Information IDs.");
            var valid = ValidateInformation(item);
            if (!valid.IsSuccess) return valid;
        }
        var candidateFacts = new Dictionary<FactId, FactSnapshot>();
        foreach (var item in snapshot.Facts)
        {
            if (!candidateFacts.TryAdd(item.Id, item)) return InformationResult.Failure(InformationErrorCodes.DuplicateId, "Snapshot contains duplicate Fact IDs.");
            if (!candidateInformation.TryGetValue(item.InformationId, out var info)) return InformationResult.Failure(InformationErrorCodes.InvalidReference, "Fact references missing Information.");
            if (item.Id.Value == Guid.Empty || item.EffectiveAt.Value < info.CreatedAt.Value || !ValidOptional(item.ProvenanceReference))
                return InformationResult.Failure(InformationErrorCodes.InvalidRecord, "Fact snapshot is malformed.");
        }
        if (candidateFacts.Values.GroupBy(item => item.InformationId).Any(group => group.Count() > 1))
            return InformationResult.Failure(InformationErrorCodes.DuplicateFact, "Snapshot contains multiple Facts for one Information record.");

        var candidateAwareness = new Dictionary<(EntityId, InformationId), AwarenessSnapshot>();
        foreach (var item in snapshot.Awareness)
        {
            if (!candidateAwareness.TryAdd((item.KnowerEntityId, item.InformationId), item))
                return InformationResult.Failure(InformationErrorCodes.InvalidAwareness, "Snapshot contains duplicate Awareness tuples.");
            var valid = ValidateAwareness(item, candidateInformation, candidateFacts);
            if (!valid.IsSuccess) return valid;
        }
        information = candidateInformation;
        facts = candidateFacts;
        awareness = candidateAwareness;
        return InformationResult.Success();
    }

    private InformationResult ValidateInformation(InformationRecordSnapshot item)
    {
        if (item.Id.Value == Guid.Empty || !Valid(item.TypeId.Value) || !ValidOptional(item.ProvenanceReference))
            return InformationResult.Failure(InformationErrorCodes.InvalidIdentifier, "Information identifiers must be initialized and normalized.");
        if (item.SubjectEntityId is { } subject && !ValidEntity(subject) || item.ObjectEntityId is { } obj && !ValidEntity(obj))
            return InformationResult.Failure(InformationErrorCodes.InvalidReference, "Information Entity references must resolve and not be Destroyed.");
        if (item.Attributes is null || item.SubjectEntityId is null && item.ObjectEntityId is null && item.Attributes.Count == 0)
            return InformationResult.Failure(InformationErrorCodes.InvalidRecord, "Information requires attributes or an Entity reference.");
        if (item.Attributes.Any(pair => !Valid(pair.Key) || !Valid(pair.Value)))
            return InformationResult.Failure(InformationErrorCodes.InvalidRecord, "Information attributes must be normalized.");
        return InformationResult.Success();
    }

    private InformationResult ValidateFact(FactSnapshot item)
    {
        if (item.Id.Value == Guid.Empty || !information.TryGetValue(item.InformationId, out var info))
            return InformationResult.Failure(InformationErrorCodes.InvalidReference, "Fact identity or Information reference is invalid.");
        if (item.EffectiveAt.Value < info.CreatedAt.Value || !ValidOptional(item.ProvenanceReference))
            return InformationResult.Failure(InformationErrorCodes.InvalidRecord, "Fact timestamp or provenance is invalid.");
        return InformationResult.Success();
    }

    private InformationResult ValidateAwareness(AwarenessSnapshot item) => ValidateAwareness(item, information, facts);

    private InformationResult ValidateAwareness(AwarenessSnapshot item,
        IReadOnlyDictionary<InformationId, InformationRecordSnapshot> candidateInformation,
        IReadOnlyDictionary<FactId, FactSnapshot> candidateFacts)
    {
        if (!ValidEntity(item.KnowerEntityId) || item.SourceEntityId is { } source && !ValidEntity(source) ||
            !candidateInformation.ContainsKey(item.InformationId))
            return InformationResult.Failure(InformationErrorCodes.InvalidReference, "Awareness references are invalid.");
        if (!Enum.IsDefined(item.Stance) || item.Confidence is < MinimumConfidence or > MaximumConfidence ||
            item.AcquiredAt.Value > item.LastUpdatedAt.Value || !ValidOptional(item.ProvenanceReference))
            return InformationResult.Failure(InformationErrorCodes.InvalidAwareness, "Awareness state is malformed.");
        if (item.Stance == EpistemicStance.Known && !candidateFacts.Values.Any(fact => fact.InformationId == item.InformationId))
            return InformationResult.Failure(InformationErrorCodes.InvalidAwareness, "Known awareness requires an authoritative Fact.");
        return InformationResult.Success();
    }

    private bool ValidEntity(EntityId id)
    {
        var found = entities.Find(id);
        return found.IsSuccess && found.Value!.LifecycleState != EntityLifecycleState.Destroyed;
    }

    private IReadOnlyList<InformationRecordSnapshot> QueryInformation(Func<InformationRecordSnapshot, bool> predicate) =>
        information.Values.Where(predicate).OrderBy(item => item.Id.Value).ToArray();
    private IReadOnlyList<AwarenessSnapshot> QueryAwareness(Func<AwarenessSnapshot, bool> predicate) => awareness.Values.Where(predicate)
        .OrderBy(item => item.KnowerEntityId.Value).ThenBy(item => item.InformationId.Value).ToArray();

    private InformationResult Publish(InformationDomainEvent value)
    {
        if (events is null) return InformationResult.Success();
        var result = events.Publish(value);
        return result.IsSuccess ? result : InformationResult.Failure(InformationErrorCodes.EventPublicationFailed, result.Error!.Message);
    }

    private static InformationRecordSnapshot Canonical(InformationRecordSnapshot item) => new(item.Id, item.TypeId,
        item.SubjectEntityId, item.ObjectEntityId, item.Attributes, item.CreatedAt, item.ProvenanceReference);
    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static bool ValidOptional(string? value) => value is null || Valid(value);
    private static string? NormalizeOptional(string? value) => value?.Trim();
    private static InformationResult<T> Fail<T>(InformationError error) => InformationResult<T>.Failure(error.Code, error.Message);
}
