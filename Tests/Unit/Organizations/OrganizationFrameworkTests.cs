using Mythos.Framework.Entities;
using Mythos.Framework.Organizations;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Organizations;

public sealed class OrganizationFrameworkTests
{
    private static readonly OrganizationKindId Generic = new("generic");
    private static readonly OrganizationRoleId Member = new("member");

    [Fact]
    public void ProfileAndMembershipLifecycleSupportsReplacement()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Organization, Generic, new WorldTimestamp(1)).IsSuccess);
        var membership = fixture.Framework.AddMembership(fixture.Organization, fixture.Member,
            [Member], new WorldTimestamp(2)).Value!;
        Assert.Equal(membership.Id, fixture.Framework.FindActiveMembership(fixture.Organization, fixture.Member).Value!.Id);
        Assert.Single(fixture.Framework.QueryByRole(Member));
        Assert.True(fixture.Framework.ReplaceRoles(membership.Id, [new("observer"), Member], new WorldTimestamp(3)).IsSuccess);
        Assert.Equal([Member, new OrganizationRoleId("observer")], fixture.Framework.FindMembership(membership.Id).Value!.RoleIds);
        Assert.True(fixture.Framework.RetireMembership(membership.Id, new WorldTimestamp(4)).IsSuccess);
        Assert.True(fixture.Framework.AddMembership(fixture.Organization, fixture.Member, [], new WorldTimestamp(5)).IsSuccess);
    }

    [Fact]
    public void RegistrationRequiresActiveOrganizationEntity()
    {
        var fixture = CreateFixture();
        Assert.Equal(OrganizationErrorCodes.InvalidReference,
            fixture.Framework.Register(fixture.Member, Generic, new WorldTimestamp(1)).Error?.Code);
        Assert.True(fixture.Entities.Retire(fixture.Organization, 1).IsSuccess);
        Assert.Equal(OrganizationErrorCodes.InvalidLifecycle,
            fixture.Framework.Register(fixture.Organization, Generic, new WorldTimestamp(2)).Error?.Code);
    }

    [Fact]
    public void MembershipRejectsSelfMissingDuplicateAndMalformedRoles()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Organization, Generic, new WorldTimestamp(1)).IsSuccess);
        Assert.Equal(OrganizationErrorCodes.InvalidReference,
            fixture.Framework.AddMembership(fixture.Organization, fixture.Organization, [], new WorldTimestamp(2)).Error?.Code);
        Assert.Equal(OrganizationErrorCodes.InvalidIdentifier,
            fixture.Framework.AddMembership(fixture.Organization, fixture.Member, [Member, Member], new WorldTimestamp(2)).Error?.Code);
        Assert.True(fixture.Framework.AddMembership(fixture.Organization, fixture.Member, [], new WorldTimestamp(2)).IsSuccess);
        Assert.Equal(OrganizationErrorCodes.DuplicateActiveMembership,
            fixture.Framework.AddMembership(fixture.Organization, fixture.Member, [], new WorldTimestamp(3)).Error?.Code);
    }

    [Fact]
    public void OrganizationRetirementRequiresExplicitMembershipRetirement()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Organization, Generic, new WorldTimestamp(1)).IsSuccess);
        var membership = fixture.Framework.AddMembership(fixture.Organization, fixture.Member, [], new WorldTimestamp(2)).Value!;
        Assert.Equal(OrganizationErrorCodes.ActiveMembershipsRemain,
            fixture.Framework.RetireOrganization(fixture.Organization, new WorldTimestamp(3)).Error?.Code);
        Assert.True(fixture.Framework.RetireMembership(membership.Id, new WorldTimestamp(3)).IsSuccess);
        Assert.True(fixture.Framework.RetireOrganization(fixture.Organization, new WorldTimestamp(4)).IsSuccess);
        Assert.Equal(OrganizationLifecycleState.Retired,
            fixture.Framework.FindOrganization(fixture.Organization).Value!.LifecycleState);
    }

    [Fact]
    public void TerminalEntitiesRemainValidForHistoricalMembership()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Organization, Generic, new WorldTimestamp(1)).IsSuccess);
        var membership = fixture.Framework.AddMembership(fixture.Organization, fixture.Member, [], new WorldTimestamp(2)).Value!;
        Assert.True(fixture.Framework.RetireMembership(membership.Id, new WorldTimestamp(3)).IsSuccess);
        Assert.True(fixture.Entities.Destroy(fixture.Member, 4).IsSuccess);
        Assert.True(fixture.Framework.ValidateReferences().IsSuccess);
    }

    [Fact]
    public void EventFailureLeavesStateUnchanged()
    {
        var sink = new FailingSink { Fail = true };
        var fixture = CreateFixture(sink);
        Assert.Equal(OrganizationErrorCodes.EventPublicationFailed,
            fixture.Framework.Register(fixture.Organization, Generic, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(0, fixture.Framework.OrganizationCount);
        sink.Fail = false;
        Assert.True(fixture.Framework.Register(fixture.Organization, Generic, new WorldTimestamp(1)).IsSuccess);
        sink.Fail = true;
        Assert.Equal(OrganizationErrorCodes.EventPublicationFailed,
            fixture.Framework.AddMembership(fixture.Organization, fixture.Member, [], new WorldTimestamp(2)).Error?.Code);
        Assert.Equal(0, fixture.Framework.MembershipCount);
    }

    [Fact]
    public void SnapshotIsDefensiveDeterministicAndAtomic()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Organization, Generic, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Framework.AddMembership(fixture.Organization, fixture.Member,
            [new("zeta"), new("alpha")], new WorldTimestamp(2)).IsSuccess);
        var snapshot = fixture.Framework.ExportSnapshot();
        Assert.Equal([new OrganizationRoleId("alpha"), new OrganizationRoleId("zeta")], snapshot.Memberships!.Single().RoleIds);
        var source = snapshot.Memberships!.ToList();
        var defensive = new OrganizationFrameworkSnapshot(1, snapshot.Profiles, source);
        source.Clear();
        Assert.Single(defensive.Memberships!);
        var restored = new OrganizationFramework(fixture.Entities);
        Assert.True(restored.RestoreSnapshot(snapshot).IsSuccess);
        var before = restored.ExportSnapshot();
        var malformed = new MembershipSnapshot(snapshot.Memberships![0].Id, fixture.Organization, fixture.Member,
            null, MembershipLifecycleState.Active, new WorldTimestamp(2), new WorldTimestamp(2), null);
        Assert.Equal(OrganizationErrorCodes.InvalidIdentifier,
            restored.RestoreSnapshot(new OrganizationFrameworkSnapshot(1, snapshot.Profiles, [malformed])).Error?.Code);
        Assert.Equal(before.Memberships, restored.ExportSnapshot().Memberships);
    }

    [Fact]
    public void RestoreRejectsVersionDuplicateIdsAndDuplicateActiveKeys()
    {
        var fixture = CreateFixture();
        var profile = new OrganizationProfile(fixture.Organization, Generic, OrganizationLifecycleState.Active,
            new WorldTimestamp(1), new WorldTimestamp(1), null);
        Assert.Equal(OrganizationErrorCodes.InvalidSnapshot, fixture.Framework.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(OrganizationErrorCodes.UnsupportedSnapshotVersion,
            fixture.Framework.RestoreSnapshot(new OrganizationFrameworkSnapshot(0, [], [])).Error?.Code);
        var one = Membership(Guid.Parse("10000000-0000-0000-0000-000000000000"), fixture);
        var two = Membership(Guid.Parse("20000000-0000-0000-0000-000000000000"), fixture);
        Assert.Equal(OrganizationErrorCodes.DuplicateMembershipId,
            fixture.Framework.RestoreSnapshot(new OrganizationFrameworkSnapshot(1, [profile], [one, one])).Error?.Code);
        Assert.Equal(OrganizationErrorCodes.DuplicateActiveMembership,
            fixture.Framework.RestoreSnapshot(new OrganizationFrameworkSnapshot(1, [profile], [one, two])).Error?.Code);
    }

    private static MembershipSnapshot Membership(Guid id, Fixture fixture) => new(new MembershipId(id),
        fixture.Organization, fixture.Member, [], MembershipLifecycleState.Active,
        new WorldTimestamp(1), new WorldTimestamp(1), null);

    private static Fixture CreateFixture(IOrganizationEventSink? sink = null)
    {
        var entities = new EntityRegistry();
        var organization = entities.Create(new EntityCategory("Organization"), 0).Value!.Id;
        var member = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        return new Fixture(entities, new OrganizationFramework(entities, new FixedIdGenerator(), sink), organization, member);
    }

    private sealed record Fixture(EntityRegistry Entities, OrganizationFramework Framework, EntityId Organization, EntityId Member);
    private sealed class FixedIdGenerator : IMembershipIdGenerator
    {
        private int next = 1;
        public MembershipId Create() => new(new Guid(next++, 0, 0, new byte[8]));
    }
    private sealed class FailingSink : IOrganizationEventSink
    {
        public bool Fail { get; set; }
        public OrganizationResult Publish(OrganizationDomainEvent domainEvent) => Fail
            ? OrganizationResult.Failure("event.failed", "Fixture event failed.") : OrganizationResult.Success();
    }
}
