using Mythos.Framework.DynamicEvents;
using Mythos.Framework.Entities;
using Mythos.Framework.Regions;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.DynamicEvents;

public sealed class DynamicWorldEventFrameworkTests
{
    private static readonly DynamicWorldEventTypeId Type = new("situation");

    [Fact]
    public void ScheduledEventActivatesAndResolvesWithDeterministicQueries()
    {
        var fixture = CreateFixture();
        var item = fixture.Framework.Create(Type, new(1), new WorldTimestamp(2), false, fixture.Region,
            [fixture.Second, fixture.First], new Dictionary<string, string> { ["zeta"] = "z", ["alpha"] = "a" }, "source:1").Value!;
        Assert.Equal([fixture.First, fixture.Second], item.ParticipantEntityIds);
        Assert.Equal(["alpha", "zeta"], item.Attributes!.Keys);
        Assert.True(fixture.Framework.Activate(item.Id, new(2)).IsSuccess);
        Assert.True(fixture.Framework.Resolve(item.Id, new("completed"), new(3)).IsSuccess);
        Assert.Single(fixture.Framework.QueryByParticipant(fixture.First));
        Assert.Single(fixture.Framework.QueryByRegion(fixture.Region));
        Assert.Single(fixture.Framework.QueryByOutcome(new("completed")));
        Assert.Single(fixture.Framework.QueryBySource("source:1"));
    }

    [Fact]
    public void LifecycleTransitionsAreExplicitAndTerminal()
    {
        var fixture = CreateFixture();
        var scheduled = fixture.Framework.Create(Type, new(1), null, false, fixture.Region, [], null).Value!;
        Assert.Equal(DynamicWorldEventErrorCodes.InvalidLifecycle,
            fixture.Framework.Resolve(scheduled.Id, new("done"), new(2)).Error?.Code);
        Assert.True(fixture.Framework.Cancel(scheduled.Id, new(2)).IsSuccess);
        Assert.Equal(DynamicWorldEventErrorCodes.InvalidLifecycle,
            fixture.Framework.Activate(scheduled.Id, new(3)).Error?.Code);
        var active = fixture.Framework.Create(Type, new(1), null, true, fixture.Region, [], null).Value!;
        Assert.True(fixture.Framework.Expire(active.Id, new(2)).IsSuccess);
        Assert.Equal(DynamicWorldEventLifecycleState.Expired, fixture.Framework.Find(active.Id).Value!.LifecycleState);
    }

    [Fact]
    public void MissingReferencesAndInvalidTimeAreRejected()
    {
        var fixture = CreateFixture();
        var missing = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000099"));
        Assert.Equal(DynamicWorldEventErrorCodes.InvalidReference,
            fixture.Framework.Create(Type, new(1), null, true, fixture.Region, [missing], null).Error?.Code);
        Assert.Equal(DynamicWorldEventErrorCodes.InvalidTimestamp,
            fixture.Framework.Create(Type, new(2), new WorldTimestamp(1), false, fixture.Region, [], null).Error?.Code);
    }

    [Fact]
    public void TerminalParticipantsRemainValid()
    {
        var fixture = CreateFixture();
        var item = fixture.Framework.Create(Type, new(1), null, true, fixture.Region, [fixture.First], null).Value!;
        Assert.True(fixture.Entities.Destroy(fixture.First, 2).IsSuccess);
        Assert.True(fixture.Framework.Resolve(item.Id, new("done"), new(2)).IsSuccess);
        Assert.True(fixture.Framework.ValidateReferences().IsSuccess);
    }

    [Fact]
    public void EventFailureLeavesStateUnchanged()
    {
        var sink = new FailingSink { Fail = true };
        var fixture = CreateFixture(sink);
        Assert.Equal(DynamicWorldEventErrorCodes.EventPublicationFailed,
            fixture.Framework.Create(Type, new(1), null, true, fixture.Region, [], null).Error?.Code);
        Assert.Equal(0, fixture.Framework.Count);
        sink.Fail = false;
        var item = fixture.Framework.Create(Type, new(1), null, true, fixture.Region, [], null).Value!;
        sink.Fail = true;
        Assert.Equal(DynamicWorldEventErrorCodes.EventPublicationFailed,
            fixture.Framework.Resolve(item.Id, new("done"), new(2)).Error?.Code);
        Assert.Equal(DynamicWorldEventLifecycleState.Active, fixture.Framework.Find(item.Id).Value!.LifecycleState);
    }

    [Fact]
    public void SnapshotIsDefensiveAndMalformedRestoreIsAtomic()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Create(Type, new(1), null, true, fixture.Region, [fixture.First], null).IsSuccess);
        var snapshot = fixture.Framework.ExportSnapshot();
        var source = snapshot.Events!.ToList();
        var defensive = new DynamicWorldEventFrameworkSnapshot(1, source);
        source.Clear();
        Assert.Single(defensive.Events!);
        var restored = new DynamicWorldEventFramework(fixture.Entities, fixture.Regions);
        Assert.True(restored.RestoreSnapshot(snapshot).IsSuccess);
        var before = restored.ExportSnapshot();
        var malformed = new DynamicWorldEventSnapshot(snapshot.Events![0].Id, Type,
            DynamicWorldEventLifecycleState.Resolved, new(1), null, null, new WorldTimestamp(2), fixture.Region,
            [], new Dictionary<string, string>(), new DynamicWorldEventOutcomeId("done"), null, null);
        Assert.Equal(DynamicWorldEventErrorCodes.InvalidLifecycle,
            restored.RestoreSnapshot(new DynamicWorldEventFrameworkSnapshot(1, [malformed])).Error?.Code);
        Assert.Equal(before.Events, restored.ExportSnapshot().Events);
    }

    [Fact]
    public void RestoreRejectsNullVersionAndDuplicateIds()
    {
        var fixture = CreateFixture();
        Assert.Equal(DynamicWorldEventErrorCodes.InvalidSnapshot, fixture.Framework.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(DynamicWorldEventErrorCodes.UnsupportedSnapshotVersion,
            fixture.Framework.RestoreSnapshot(new DynamicWorldEventFrameworkSnapshot(0, [])).Error?.Code);
        var item = fixture.Framework.Create(Type, new(1), null, true, fixture.Region, [], null).Value!;
        Assert.Equal(DynamicWorldEventErrorCodes.DuplicateId,
            fixture.Framework.RestoreSnapshot(new DynamicWorldEventFrameworkSnapshot(1, [item, item])).Error?.Code);
    }

    private static Fixture CreateFixture(IDynamicWorldEventSink? sink = null)
    {
        var entities = new EntityRegistry();
        var region = entities.Create(new EntityCategory("Region"), 0).Value!.Id;
        var first = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var second = entities.Create(new EntityCategory("Organization"), 0).Value!.Id;
        var regions = new RegionFramework(entities);
        Assert.True(regions.Restore(new RegionFrameworkSnapshot(1, region,
            [new(region, new RegionCategory("WorldScope"), null, RegionSimulationState.Active, region, null,
                new Dictionary<string, string>())], [], [])).IsSuccess);
        return new Fixture(entities, regions, new DynamicWorldEventFramework(entities, regions, new FixedIds(), sink), region, first, second);
    }
    private sealed record Fixture(EntityRegistry Entities, RegionFramework Regions, DynamicWorldEventFramework Framework,
        EntityId Region, EntityId First, EntityId Second);
    private sealed class FixedIds : IDynamicWorldEventIdGenerator
    {
        private int next = 1;
        public DynamicWorldEventId Create() => new(new Guid(next++, 0, 0, new byte[8]));
    }
    private sealed class FailingSink : IDynamicWorldEventSink
    {
        public bool Fail { get; set; }
        public DynamicWorldEventResult Publish(DynamicWorldEventNotification notification) => Fail
            ? DynamicWorldEventResult.Failure("event.failed", "Fixture event failed.") : DynamicWorldEventResult.Success();
    }
}
