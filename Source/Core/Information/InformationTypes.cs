using System.Collections.ObjectModel;
using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Information;

public readonly record struct InformationId(Guid Value) { public override string ToString() => Value.ToString("D"); }
public readonly record struct FactId(Guid Value) { public override string ToString() => Value.ToString("D"); }

public interface IInformationIdGenerator
{
    InformationId CreateInformationId();
    FactId CreateFactId();
}

public sealed class Version7InformationIdGenerator : IInformationIdGenerator
{
    public InformationId CreateInformationId() => new(Guid.CreateVersion7());
    public FactId CreateFactId() => new(Guid.CreateVersion7());
}

public readonly record struct InformationTypeId
{
    public InformationTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public enum EpistemicStance { Known, Believed, Disbelieved }

public sealed record InformationRecordSnapshot
{
    public InformationRecordSnapshot(InformationId id, InformationTypeId typeId, EntityId? subjectEntityId,
        EntityId? objectEntityId, IReadOnlyDictionary<string, string>? attributes, WorldTimestamp createdAt,
        string? provenanceReference)
    {
        Id = id;
        TypeId = typeId;
        SubjectEntityId = subjectEntityId;
        ObjectEntityId = objectEntityId;
        Attributes = attributes is null ? null : new ReadOnlyDictionary<string, string>(new SortedDictionary<string, string>(
            attributes.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal));
        CreatedAt = createdAt;
        ProvenanceReference = provenanceReference;
    }

    public InformationId Id { get; }
    public InformationTypeId TypeId { get; }
    public EntityId? SubjectEntityId { get; }
    public EntityId? ObjectEntityId { get; }
    public IReadOnlyDictionary<string, string>? Attributes { get; }
    public WorldTimestamp CreatedAt { get; }
    public string? ProvenanceReference { get; }
}

public sealed record FactSnapshot(FactId Id, InformationId InformationId, WorldTimestamp EffectiveAt, string? ProvenanceReference);

public sealed record AwarenessSnapshot(EntityId KnowerEntityId, InformationId InformationId, EpistemicStance Stance,
    int Confidence, WorldTimestamp AcquiredAt, WorldTimestamp LastUpdatedAt, EntityId? SourceEntityId,
    string? ProvenanceReference);

public sealed record InformationFrameworkSnapshot
{
    public const int CurrentVersion = 1;

    public InformationFrameworkSnapshot(int version, IReadOnlyList<InformationRecordSnapshot>? information,
        IReadOnlyList<FactSnapshot>? facts, IReadOnlyList<AwarenessSnapshot>? awareness)
    {
        Version = version;
        Information = information is null ? null : Array.AsReadOnly(information.ToArray());
        Facts = facts is null ? null : Array.AsReadOnly(facts.ToArray());
        Awareness = awareness is null ? null : Array.AsReadOnly(awareness.ToArray());
    }

    public int Version { get; }
    public IReadOnlyList<InformationRecordSnapshot>? Information { get; }
    public IReadOnlyList<FactSnapshot>? Facts { get; }
    public IReadOnlyList<AwarenessSnapshot>? Awareness { get; }
}

public sealed record InformationDomainEvent(string Type, InformationId InformationId, WorldTimestamp OccurredAt,
    FactId? FactId = null, EntityId? KnowerEntityId = null, EpistemicStance? PreviousStance = null,
    EpistemicStance? CurrentStance = null);

public interface IInformationEventSink { InformationResult Publish(InformationDomainEvent domainEvent); }

public sealed record InformationDiagnostic(InformationRecordSnapshot Information, FactSnapshot? Fact,
    IReadOnlyList<AwarenessSnapshot> Awareness, string ValidationStatus);

