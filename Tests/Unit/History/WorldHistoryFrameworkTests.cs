using Mythos.Framework.Entities;
using Mythos.Framework.History;
using Mythos.Framework.Regions;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.History;

public sealed class WorldHistoryFrameworkTests
{
    private static readonly HistoryTypeId Milestone = new("world-milestone");

    [Fact]
    public void RecordAndQueriesPreserveChronologyAndReferences()
    {
        var fixture = CreateFixture();
        var later = fixture.History.Record(Milestone, new WorldTimestamp(5), [fixture.Participant], fixture.Region,
            800, new Dictionary<string, string> { ["state"] = "later" }, "event:later").Value!;
        var earlier = fixture.History.Record(Milestone, new WorldTimestamp(2), [fixture.Participant], fixture.Region,
            200, new Dictionary<string, string> { ["state"] = "earlier" }, "event:earlier").Value!;

        Assert.Equal(new[] { earlier.Id, later.Id }, fixture.History.Timeline().Select(item => item.Id));
        Assert.Equal(2, fixture.History.QueryByParticipant(fixture.Participant).Count);
        Assert.Equal(2, fixture.History.QueryByRegion(fixture.Region).Count);
        Assert.Single(fixture.History.QueryMinimumImportance(500));
        Assert.Equal(earlier.Id, fixture.History.FindBySource("event:earlier").Value!.Id);
    }

    [Fact]
    public void RecordAcceptsTerminalParticipantsButRejectsMissingParticipantsAndRegions()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Entities.Destroy(fixture.Participant, 1).IsSuccess);
        Assert.True(fixture.History.Record(Milestone, new WorldTimestamp(2), [fixture.Participant], fixture.Region).IsSuccess);
        var missing = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000099"));
        Assert.Equal(HistoryErrorCodes.InvalidReference,
            fixture.History.Record(Milestone, new WorldTimestamp(2), [missing], fixture.Region).Error?.Code);
        Assert.Equal(HistoryErrorCodes.InvalidReference,
            fixture.History.Record(Milestone, new WorldTimestamp(2), [], missing).Error?.Code);
    }

    [Fact]
    public void ValidationRejectsEmptyMalformedDuplicateAndOutOfRangeEntries()
    {
        var fixture = CreateFixture();
        Assert.Equal(HistoryErrorCodes.InvalidEntry,
            fixture.History.Record(Milestone, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(HistoryErrorCodes.InvalidEntry,
            fixture.History.Record(Milestone, new WorldTimestamp(1), [fixture.Participant], importance: 1001).Error?.Code);
        Assert.Equal(HistoryErrorCodes.InvalidReference,
            fixture.History.Record(Milestone, new WorldTimestamp(1), [fixture.Participant, fixture.Participant]).Error?.Code);
        Assert.Equal(HistoryErrorCodes.InvalidEntry,
            fixture.History.Record(Milestone, new WorldTimestamp(1), metadata: new Dictionary<string, string> { [" bad"] = "value" }).Error?.Code);
    }

    [Fact]
    public void SourceEventCanOnlyBePromotedOnce()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.History.Record(Milestone, new WorldTimestamp(1), [fixture.Participant],
            sourceEventReference: "event:one").IsSuccess);
        Assert.Equal(HistoryErrorCodes.DuplicateSource,
            fixture.History.Record(Milestone, new WorldTimestamp(2), [fixture.Participant],
                sourceEventReference: "event:one").Error?.Code);
    }

    [Fact]
    public void EventFailureLeavesHistoryUnchanged()
    {
        var sink = new FailingSink { Fail = true };
        var fixture = CreateFixture(sink);
        Assert.Equal(HistoryErrorCodes.EventPublicationFailed,
            fixture.History.Record(Milestone, new WorldTimestamp(1), [fixture.Participant]).Error?.Code);
        Assert.Equal(0, fixture.History.Count);
    }

    [Fact]
    public void CollectionsAreCanonicalAndDefensive()
    {
        var fixture = CreateFixture();
        var second = fixture.Entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var entry = fixture.History.Record(Milestone, new WorldTimestamp(1), [second, fixture.Participant], fixture.Region,
            metadata: new Dictionary<string, string> { ["zeta"] = "z", ["alpha"] = "a" }).Value!;

        Assert.Equal(entry.ParticipantEntityIds!.OrderBy(item => item.Value), entry.ParticipantEntityIds);
        Assert.Equal(new[] { "alpha", "zeta" }, entry.Metadata!.Keys);
        Assert.Throws<NotSupportedException>(() => ((IList<EntityId>)entry.ParticipantEntityIds!).Add(second));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)entry.Metadata!).Add("mutate", "no"));
        var source = fixture.History.ExportSnapshot().Entries!.ToList();
        var snapshot = new WorldHistorySnapshot(1, source);
        source.Clear();
        Assert.Single(snapshot.Entries!);
    }

    [Fact]
    public void RestoreRoundTripsAndRejectsMalformedStateAtomically()
    {
        var fixture = CreateFixture();
        var entry = fixture.History.Record(Milestone, new WorldTimestamp(1), [fixture.Participant], fixture.Region,
            500, new Dictionary<string, string> { ["state"] = "done" }, "event:one", "domain:test").Value!;
        var restored = new WorldHistoryFramework(fixture.Entities, fixture.Regions);
        Assert.True(restored.RestoreSnapshot(fixture.History.ExportSnapshot()).IsSuccess);
        Assert.Equal(entry.Id, restored.Timeline().Single().Id);
        var before = restored.ExportSnapshot();
        var malformed = new HistoryEntrySnapshot(new HistoryEntryId(Guid.NewGuid()), Milestone, new WorldTimestamp(2),
            [], null, 0, new Dictionary<string, string>(), null, null);
        Assert.Equal(HistoryErrorCodes.InvalidEntry,
            restored.RestoreSnapshot(new WorldHistorySnapshot(1, [malformed])).Error?.Code);
        Assert.Equal(before.Entries, restored.ExportSnapshot().Entries);
    }

    [Fact]
    public void RestoreRejectsNullVersionDuplicatesAndDuplicateSources()
    {
        var fixture = CreateFixture();
        Assert.Equal(HistoryErrorCodes.InvalidSnapshot, fixture.History.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(HistoryErrorCodes.InvalidSnapshot, fixture.History.RestoreSnapshot(new WorldHistorySnapshot(1, null)).Error?.Code);
        Assert.Equal(HistoryErrorCodes.UnsupportedSnapshotVersion, fixture.History.RestoreSnapshot(new WorldHistorySnapshot(0, [])).Error?.Code);
        var one = Entry(Guid.Parse("10000000-0000-0000-0000-000000000000"), fixture.Participant, "event:same");
        var two = Entry(Guid.Parse("20000000-0000-0000-0000-000000000000"), fixture.Participant, "event:same");
        Assert.Equal(HistoryErrorCodes.DuplicateId, fixture.History.RestoreSnapshot(new WorldHistorySnapshot(1, [one, one])).Error?.Code);
        Assert.Equal(HistoryErrorCodes.DuplicateSource, fixture.History.RestoreSnapshot(new WorldHistorySnapshot(1, [one, two])).Error?.Code);
    }

    private static HistoryEntrySnapshot Entry(Guid id, EntityId participant, string source) => new(new HistoryEntryId(id),
        Milestone, new WorldTimestamp(1), [participant], null, 1, new Dictionary<string, string>(), source, null);

    private static Fixture CreateFixture(IHistoryEventSink? sink = null)
    {
        var entities = new EntityRegistry();
        var region = entities.Create(new EntityCategory("Region"), 0).Value!.Id;
        var participant = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var regions = new RegionFramework(entities);
        Assert.True(regions.Restore(new RegionFrameworkSnapshot(1, region,
            [new(region, new RegionCategory("WorldScope"), null, RegionSimulationState.Active, region, null,
                new Dictionary<string, string>())], [], [])).IsSuccess);
        return new Fixture(entities, regions, new WorldHistoryFramework(entities, regions, events: sink), region, participant);
    }

    private sealed record Fixture(EntityRegistry Entities, RegionFramework Regions, WorldHistoryFramework History,
        EntityId Region, EntityId Participant);
    private sealed class FailingSink : IHistoryEventSink
    {
        public bool Fail { get; set; }
        public HistoryResult Publish(HistoryDomainEvent domainEvent) => Fail
            ? HistoryResult.Failure("event.failed", "Fixture event failed.") : HistoryResult.Success();
    }
}
