using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Properties;

public readonly record struct PropertyKindId
{
    public PropertyKindId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public enum PropertyLifecycleState
{
    Active,
    Retired,
}

public sealed record PropertyProfile(EntityId EntityId, PropertyKindId KindId, PropertyLifecycleState LifecycleState,
    WorldTimestamp RegisteredAt, WorldTimestamp LastChangedAt, string? ProvenanceReference);

public sealed record PropertyFrameworkSnapshot
{
    public const int CurrentVersion = 1;

    public PropertyFrameworkSnapshot(int version, IReadOnlyList<PropertyProfile>? profiles)
    {
        Version = version;
        Profiles = profiles is null ? null : Array.AsReadOnly(profiles.ToArray());
    }

    public int Version { get; }
    public IReadOnlyList<PropertyProfile>? Profiles { get; }
}

public sealed record PropertyDomainEvent(string Type, EntityId PropertyEntityId, WorldTimestamp OccurredAt,
    EntityId? PreviousOwnerId = null, EntityId? CurrentOwnerId = null);

public interface IPropertyEventSink
{
    PropertyResult Publish(PropertyDomainEvent domainEvent);
}

public sealed record PropertyDiagnostic(PropertyProfile Profile, EntityId? OwnerId, EntityId? RegionId, string ValidationStatus);
