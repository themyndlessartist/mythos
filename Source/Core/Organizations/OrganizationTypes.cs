using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Organizations;

public readonly record struct OrganizationKindId
{
    public OrganizationKindId(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); Value = value.Trim(); }
    public string Value { get; }
    public override string ToString() => Value;
}
public readonly record struct OrganizationRoleId
{
    public OrganizationRoleId(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); Value = value.Trim(); }
    public string Value { get; }
    public override string ToString() => Value;
}
public readonly record struct MembershipId(Guid Value) { public override string ToString() => Value.ToString("D"); }
public interface IMembershipIdGenerator { MembershipId Create(); }
public sealed class Version7MembershipIdGenerator : IMembershipIdGenerator { public MembershipId Create() => new(Guid.CreateVersion7()); }

public enum OrganizationLifecycleState { Active, Retired }
public enum MembershipLifecycleState { Active, Retired }

public sealed record OrganizationProfile(EntityId EntityId, OrganizationKindId KindId,
    OrganizationLifecycleState LifecycleState, WorldTimestamp RegisteredAt, WorldTimestamp LastChangedAt,
    string? ProvenanceReference);

public sealed record MembershipSnapshot
{
    public MembershipSnapshot(MembershipId id, EntityId organizationEntityId, EntityId memberEntityId,
        IReadOnlyList<OrganizationRoleId>? roleIds, MembershipLifecycleState lifecycleState,
        WorldTimestamp createdAt, WorldTimestamp lastChangedAt, string? provenanceReference)
    {
        Id = id;
        OrganizationEntityId = organizationEntityId;
        MemberEntityId = memberEntityId;
        RoleIds = roleIds is null ? null : Array.AsReadOnly(roleIds.ToArray());
        LifecycleState = lifecycleState;
        CreatedAt = createdAt;
        LastChangedAt = lastChangedAt;
        ProvenanceReference = provenanceReference;
    }
    public MembershipId Id { get; }
    public EntityId OrganizationEntityId { get; }
    public EntityId MemberEntityId { get; }
    public IReadOnlyList<OrganizationRoleId>? RoleIds { get; }
    public MembershipLifecycleState LifecycleState { get; }
    public WorldTimestamp CreatedAt { get; }
    public WorldTimestamp LastChangedAt { get; }
    public string? ProvenanceReference { get; }
}

public sealed record OrganizationFrameworkSnapshot
{
    public const int CurrentVersion = 1;
    public OrganizationFrameworkSnapshot(int version, IReadOnlyList<OrganizationProfile>? profiles,
        IReadOnlyList<MembershipSnapshot>? memberships)
    {
        Version = version;
        Profiles = profiles is null ? null : Array.AsReadOnly(profiles.ToArray());
        Memberships = memberships is null ? null : Array.AsReadOnly(memberships.ToArray());
    }
    public int Version { get; }
    public IReadOnlyList<OrganizationProfile>? Profiles { get; }
    public IReadOnlyList<MembershipSnapshot>? Memberships { get; }
}

public sealed record OrganizationDomainEvent(string Type, EntityId OrganizationEntityId, WorldTimestamp OccurredAt,
    MembershipId? MembershipId = null, EntityId? MemberEntityId = null);
public interface IOrganizationEventSink { OrganizationResult Publish(OrganizationDomainEvent domainEvent); }
public sealed record OrganizationDiagnostic(OrganizationProfile Profile, int ActiveMembershipCount, string ValidationStatus);
