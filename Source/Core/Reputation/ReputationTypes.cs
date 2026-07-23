using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Reputation;

public readonly record struct ReputationId(Guid Value) { public override string ToString() => Value.ToString("D"); }
public interface IReputationIdGenerator { ReputationId Create(); }
public sealed class Version7ReputationIdGenerator : IReputationIdGenerator { public ReputationId Create() => new(Guid.CreateVersion7()); }

public readonly record struct ReputationAudienceTypeId
{
    public ReputationAudienceTypeId(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); Value = value.Trim(); }
    public string Value { get; }
    public override string ToString() => Value;
}
public readonly record struct ReputationDimensionId
{
    public ReputationDimensionId(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); Value = value.Trim(); }
    public string Value { get; }
    public override string ToString() => Value;
}

public enum ReputationLifecycleState { Active, Retired }
public sealed record ReputationSnapshot(ReputationId Id, EntityId SubjectEntityId, ReputationAudienceTypeId AudienceTypeId,
    EntityId? AudienceEntityId, ReputationDimensionId DimensionId, int Value, ReputationLifecycleState LifecycleState,
    WorldTimestamp CreatedAt, WorldTimestamp LastChangedAt, string? ProvenanceReference);

public sealed record ReputationFrameworkSnapshot
{
    public const int CurrentVersion = 1;
    public ReputationFrameworkSnapshot(int version, IReadOnlyList<ReputationSnapshot>? records)
    {
        Version = version;
        Records = records is null ? null : Array.AsReadOnly(records.ToArray());
    }
    public int Version { get; }
    public IReadOnlyList<ReputationSnapshot>? Records { get; }
}

public sealed record ReputationDomainEvent(string Type, ReputationId ReputationId, EntityId SubjectEntityId,
    WorldTimestamp OccurredAt, int? PreviousValue = null, int? CurrentValue = null);
public interface IReputationEventSink { ReputationResult Publish(ReputationDomainEvent domainEvent); }
public sealed record ReputationDiagnostic(ReputationSnapshot Record, string ValidationStatus);

