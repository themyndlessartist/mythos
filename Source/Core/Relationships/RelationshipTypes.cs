using Mythos.Framework.Entities;
using Mythos.Framework.Time;
using System.Collections.ObjectModel;

namespace Mythos.Framework.Relationships;

public readonly record struct RelationshipId(Guid Value)
{
    public override string ToString() => Value.ToString("D");
}

public interface IRelationshipIdGenerator { RelationshipId Create(); }
public sealed class Version7RelationshipIdGenerator : IRelationshipIdGenerator
{
    public RelationshipId Create() => new(Guid.CreateVersion7());
}

public readonly record struct RelationshipKindId
{
    public RelationshipKindId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct RelationshipDimensionId
{
    public RelationshipDimensionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public enum RelationshipLifecycleState { Active, Retired }

public sealed record RelationshipSnapshot
{
    public RelationshipSnapshot(RelationshipId id, EntityId sourceEntityId, EntityId targetEntityId,
        RelationshipKindId kindId, RelationshipLifecycleState lifecycleState,
        IReadOnlyDictionary<string, int>? dimensions, WorldTimestamp createdAt,
        WorldTimestamp lastChangedAt, string? provenanceReference)
    {
        Id = id;
        SourceEntityId = sourceEntityId;
        TargetEntityId = targetEntityId;
        KindId = kindId;
        LifecycleState = lifecycleState;
        Dimensions = dimensions is null ? null : new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(
            dimensions.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal));
        CreatedAt = createdAt;
        LastChangedAt = lastChangedAt;
        ProvenanceReference = provenanceReference;
    }

    public RelationshipId Id { get; }
    public EntityId SourceEntityId { get; }
    public EntityId TargetEntityId { get; }
    public RelationshipKindId KindId { get; }
    public RelationshipLifecycleState LifecycleState { get; }
    public IReadOnlyDictionary<string, int>? Dimensions { get; }
    public WorldTimestamp CreatedAt { get; }
    public WorldTimestamp LastChangedAt { get; }
    public string? ProvenanceReference { get; }
}

public sealed record RelationshipFrameworkSnapshot
{
    public const int CurrentVersion = 1;

    public RelationshipFrameworkSnapshot(int version, IReadOnlyList<RelationshipSnapshot>? relationships)
    {
        Version = version;
        Relationships = relationships is null ? null : Array.AsReadOnly(relationships.ToArray());
    }

    public int Version { get; }
    public IReadOnlyList<RelationshipSnapshot>? Relationships { get; }
}

public sealed record RelationshipDiagnostic(RelationshipSnapshot Snapshot, string ValidationStatus);

public sealed record RelationshipDomainEvent(string Type, RelationshipId RelationshipId,
    EntityId SourceEntityId, EntityId TargetEntityId, WorldTimestamp OccurredAt,
    RelationshipDimensionId? DimensionId = null, int? PreviousValue = null, int? CurrentValue = null);

public interface IRelationshipEventSink
{
    RelationshipResult Publish(RelationshipDomainEvent domainEvent);
}
