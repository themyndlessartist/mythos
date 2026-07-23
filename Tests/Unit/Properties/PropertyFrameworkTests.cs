using Mythos.Framework.Entities;
using Mythos.Framework.Properties;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Properties;

public sealed class PropertyFrameworkTests
{
    private static readonly PropertyKindId Asset = new("asset");

    [Fact]
    public void RegisterClassifyAndQueryUseAuthoritativeEntityState()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Framework.AssignOwner(fixture.Property, fixture.Owner, new WorldTimestamp(2)).IsSuccess);
        Assert.Single(fixture.Framework.QueryByKind(Asset));
        Assert.Single(fixture.Framework.QueryByOwner(fixture.Owner));
        Assert.Single(fixture.Framework.QueryByRegion(fixture.Region));
        Assert.Equal(fixture.Owner, fixture.Entities.Find(fixture.Property).Value!.OwnerId);
    }

    [Fact]
    public void DuplicateMissingAndTerminalRegistrationAreRejected()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(1)).IsSuccess);
        Assert.Equal(PropertyErrorCodes.DuplicateProfile,
            fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(2)).Error?.Code);
        var missing = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000099"));
        Assert.Equal(PropertyErrorCodes.InvalidReference,
            fixture.Framework.Register(missing, Asset, new WorldTimestamp(1)).Error?.Code);
        Assert.True(fixture.Entities.Retire(fixture.Other, 1).IsSuccess);
        Assert.Equal(PropertyErrorCodes.InvalidLifecycle,
            fixture.Framework.Register(fixture.Other, Asset, new WorldTimestamp(2)).Error?.Code);
    }

    [Fact]
    public void OwnershipTransferClearAndCycleFailurePreserveSingleAuthority()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Framework.AssignOwner(fixture.Property, fixture.Owner, new WorldTimestamp(2)).IsSuccess);
        Assert.True(fixture.Framework.AssignOwner(fixture.Property, fixture.Other, new WorldTimestamp(3)).IsSuccess);
        Assert.Equal(fixture.Other, fixture.Entities.Find(fixture.Property).Value!.OwnerId);
        Assert.True(fixture.Framework.AssignOwner(fixture.Property, null, new WorldTimestamp(4)).IsSuccess);
        Assert.Null(fixture.Entities.Find(fixture.Property).Value!.OwnerId);

        Assert.True(fixture.Entities.AssignOwner(fixture.Owner, fixture.Property).IsSuccess);
        Assert.Equal(PropertyErrorCodes.OwnershipRejected,
            fixture.Framework.AssignOwner(fixture.Property, fixture.Owner, new WorldTimestamp(5)).Error?.Code);
        Assert.Null(fixture.Entities.Find(fixture.Property).Value!.OwnerId);
    }

    [Fact]
    public void OwnerAndPropertyLifecycleRulesAreEnforced()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Entities.Retire(fixture.Owner, 2).IsSuccess);
        Assert.Equal(PropertyErrorCodes.InvalidReference,
            fixture.Framework.AssignOwner(fixture.Property, fixture.Owner, new WorldTimestamp(2)).Error?.Code);
        Assert.True(fixture.Entities.Retire(fixture.Property, 3).IsSuccess);
        Assert.Equal(PropertyErrorCodes.InvalidLifecycle,
            fixture.Framework.AssignOwner(fixture.Property, fixture.Other, new WorldTimestamp(3)).Error?.Code);
        Assert.True(fixture.Framework.ValidateReferences().IsSuccess);
    }

    [Fact]
    public void RetiredProfileIsImmutableAndKeepsEntityOwnership()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Framework.AssignOwner(fixture.Property, fixture.Owner, new WorldTimestamp(2)).IsSuccess);
        Assert.True(fixture.Framework.Retire(fixture.Property, new WorldTimestamp(3)).IsSuccess);
        Assert.Equal(PropertyLifecycleState.Retired, fixture.Framework.Find(fixture.Property).Value!.LifecycleState);
        Assert.Equal(PropertyErrorCodes.InvalidLifecycle,
            fixture.Framework.ChangeKind(fixture.Property, new PropertyKindId("other"), new WorldTimestamp(4)).Error?.Code);
        Assert.Equal(fixture.Owner, fixture.Entities.Find(fixture.Property).Value!.OwnerId);
    }

    [Fact]
    public void EventFailureLeavesPropertyAndEntityUnchanged()
    {
        var sink = new FailingSink { Fail = true };
        var fixture = CreateFixture(sink);
        Assert.Equal(PropertyErrorCodes.EventPublicationFailed,
            fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(0, fixture.Framework.Count);
        sink.Fail = false;
        Assert.True(fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(1)).IsSuccess);
        sink.Fail = true;
        Assert.Equal(PropertyErrorCodes.EventPublicationFailed,
            fixture.Framework.AssignOwner(fixture.Property, fixture.Owner, new WorldTimestamp(2)).Error?.Code);
        Assert.Null(fixture.Entities.Find(fixture.Property).Value!.OwnerId);
    }

    [Fact]
    public void SnapshotIsDefensiveDeterministicAndRestoresAtomically()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Register(fixture.Other, new PropertyKindId("zeta"), new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Framework.Register(fixture.Property, Asset, new WorldTimestamp(1)).IsSuccess);
        var snapshot = fixture.Framework.ExportSnapshot();
        Assert.Equal(snapshot.Profiles!.OrderBy(profile => profile.EntityId.Value), snapshot.Profiles);
        var source = snapshot.Profiles!.ToList();
        var defensive = new PropertyFrameworkSnapshot(1, source);
        source.Clear();
        Assert.Equal(2, defensive.Profiles!.Count);
        var restored = new PropertyFramework(fixture.Entities);
        Assert.True(restored.RestoreSnapshot(snapshot).IsSuccess);
        var before = restored.ExportSnapshot();
        var malformed = snapshot.Profiles![0] with
        {
            RegisteredAt = new WorldTimestamp(2),
            LastChangedAt = new WorldTimestamp(1),
        };
        Assert.Equal(PropertyErrorCodes.InvalidTimestamp,
            restored.RestoreSnapshot(new PropertyFrameworkSnapshot(1, [malformed])).Error?.Code);
        Assert.Equal(before.Profiles, restored.ExportSnapshot().Profiles);
    }

    [Fact]
    public void RestoreRejectsNullVersionDuplicatesAndMissingReferences()
    {
        var fixture = CreateFixture();
        Assert.Equal(PropertyErrorCodes.InvalidSnapshot, fixture.Framework.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(PropertyErrorCodes.InvalidSnapshot,
            fixture.Framework.RestoreSnapshot(new PropertyFrameworkSnapshot(1, null)).Error?.Code);
        Assert.Equal(PropertyErrorCodes.UnsupportedSnapshotVersion,
            fixture.Framework.RestoreSnapshot(new PropertyFrameworkSnapshot(0, [])).Error?.Code);
        var profile = new PropertyProfile(fixture.Property, Asset, PropertyLifecycleState.Active,
            new WorldTimestamp(1), new WorldTimestamp(1), null);
        Assert.Equal(PropertyErrorCodes.DuplicateProfile,
            fixture.Framework.RestoreSnapshot(new PropertyFrameworkSnapshot(1, [profile, profile])).Error?.Code);
        var missing = profile with { EntityId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000099")) };
        Assert.Equal(PropertyErrorCodes.InvalidReference,
            fixture.Framework.RestoreSnapshot(new PropertyFrameworkSnapshot(1, [missing])).Error?.Code);
    }

    private static Fixture CreateFixture(IPropertyEventSink? sink = null)
    {
        var entities = new EntityRegistry();
        var region = entities.Create(new EntityCategory("Region"), 0).Value!.Id;
        var property = entities.Create(new EntityCategory("Asset"), 0).Value!.Id;
        var owner = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var other = entities.Create(new EntityCategory("Organization"), 0).Value!.Id;
        Assert.True(entities.AssignRegion(property, region).IsSuccess);
        return new Fixture(entities, new PropertyFramework(entities, sink), property, owner, other, region);
    }

    private sealed record Fixture(EntityRegistry Entities, PropertyFramework Framework, EntityId Property,
        EntityId Owner, EntityId Other, EntityId Region);

    private sealed class FailingSink : IPropertyEventSink
    {
        public bool Fail { get; set; }
        public PropertyResult Publish(PropertyDomainEvent domainEvent) => Fail
            ? PropertyResult.Failure("event.failed", "Fixture event failed.")
            : PropertyResult.Success();
    }
}
