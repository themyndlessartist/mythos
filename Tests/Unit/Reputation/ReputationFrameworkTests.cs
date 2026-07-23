using Mythos.Framework.Entities;
using Mythos.Framework.Reputation;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Reputation;

public sealed class ReputationFrameworkTests
{
    private static readonly ReputationAudienceTypeId Regional = new("regional");
    private static readonly ReputationDimensionId Standing = new("standing");

    [Fact]
    public void CreateAndScopedQueriesPreserveExplicitNeutralState()
    {
        var fixture = CreateFixture();
        var record = fixture.Framework.Create(fixture.Subject, Regional, fixture.Audience, Standing, 0, new WorldTimestamp(1)).Value!;
        Assert.Equal(0, record.Value);
        Assert.Equal(record.Id, fixture.Framework.FindActive(fixture.Subject, Regional, fixture.Audience, Standing).Value!.Id);
        Assert.Single(fixture.Framework.QueryBySubject(fixture.Subject));
        Assert.Single(fixture.Framework.QueryByAudience(fixture.Audience));
        Assert.Single(fixture.Framework.QueryInvolving(fixture.Subject));
    }

    [Fact]
    public void CreateRejectsMissingReferencesMalformedValuesAndDuplicateActiveKeys()
    {
        var fixture = CreateFixture();
        var missing = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000099"));
        Assert.Equal(ReputationErrorCodes.InvalidReference,
            fixture.Framework.Create(missing, Regional, null, Standing, 0, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(ReputationErrorCodes.InvalidValue,
            fixture.Framework.Create(fixture.Subject, Regional, null, Standing, 1001, new WorldTimestamp(1)).Error?.Code);
        Assert.True(fixture.Framework.Create(fixture.Subject, Regional, fixture.Audience, Standing, 1, new WorldTimestamp(1)).IsSuccess);
        Assert.Equal(ReputationErrorCodes.DuplicateActiveKey,
            fixture.Framework.Create(fixture.Subject, Regional, fixture.Audience, Standing, 2, new WorldTimestamp(2)).Error?.Code);
    }

    [Fact]
    public void SetDeltaRetireAndReplacementAreAtomicAndBounded()
    {
        var fixture = CreateFixture();
        var id = fixture.Framework.Create(fixture.Subject, Regional, fixture.Audience, Standing, 10, new WorldTimestamp(1)).Value!.Id;
        Assert.True(fixture.Framework.ApplyDelta(id, -4, new WorldTimestamp(2)).IsSuccess);
        Assert.Equal(6, fixture.Framework.Find(id).Value!.Value);
        Assert.Equal(ReputationErrorCodes.InvalidValue, fixture.Framework.ApplyDelta(id, 1000, new WorldTimestamp(3)).Error?.Code);
        Assert.Equal(6, fixture.Framework.Find(id).Value!.Value);
        Assert.True(fixture.Framework.Retire(id, new WorldTimestamp(3)).IsSuccess);
        Assert.Equal(ReputationErrorCodes.InvalidLifecycle, fixture.Framework.SetValue(id, 1, new WorldTimestamp(4)).Error?.Code);
        Assert.True(fixture.Framework.Create(fixture.Subject, Regional, fixture.Audience, Standing, 1, new WorldTimestamp(4)).IsSuccess);
    }

    [Fact]
    public void TerminalEntitiesRemainValidForReputationContinuity()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Entities.Destroy(fixture.Subject, 1).IsSuccess);
        Assert.True(fixture.Framework.Create(fixture.Subject, Regional, fixture.Audience, Standing, 1, new WorldTimestamp(2)).IsSuccess);
        Assert.True(fixture.Framework.ValidateReferences().IsSuccess);
    }

    [Fact]
    public void EventFailureLeavesCreateAndMutationUnchanged()
    {
        var sink = new FailingSink { Fail = true };
        var fixture = CreateFixture(sink);
        Assert.Equal(ReputationErrorCodes.EventPublicationFailed,
            fixture.Framework.Create(fixture.Subject, Regional, null, Standing, 1, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(0, fixture.Framework.Count);
        sink.Fail = false;
        var id = fixture.Framework.Create(fixture.Subject, Regional, null, Standing, 1, new WorldTimestamp(1)).Value!.Id;
        sink.Fail = true;
        Assert.Equal(ReputationErrorCodes.EventPublicationFailed, fixture.Framework.SetValue(id, 9, new WorldTimestamp(2)).Error?.Code);
        Assert.Equal(1, fixture.Framework.Find(id).Value!.Value);
    }

    [Fact]
    public void SnapshotIsDefensiveDeterministicAndRestoresAtomically()
    {
        var fixture = CreateFixture();
        var first = fixture.Framework.Create(fixture.Subject, Regional, fixture.Audience, Standing, 1, new WorldTimestamp(1)).Value!;
        var second = fixture.Framework.Create(fixture.Audience, Regional, fixture.Subject, Standing, 2, new WorldTimestamp(1)).Value!;
        var snapshot = fixture.Framework.ExportSnapshot();
        Assert.Equal(snapshot.Records!.OrderBy(item => item.Id.Value), snapshot.Records);
        var source = snapshot.Records!.ToList();
        var defensive = new ReputationFrameworkSnapshot(1, source);
        source.Clear();
        Assert.Equal(2, defensive.Records!.Count);
        var restored = new ReputationFramework(fixture.Entities);
        Assert.True(restored.RestoreSnapshot(snapshot).IsSuccess);
        var before = restored.ExportSnapshot();
        var malformed = first with { Value = 1001 };
        Assert.Equal(ReputationErrorCodes.InvalidValue,
            restored.RestoreSnapshot(new ReputationFrameworkSnapshot(1, [malformed])).Error?.Code);
        Assert.Equal(before.Records, restored.ExportSnapshot().Records);
        Assert.Contains(second.Id, restored.QueryByDimension(Standing).Select(item => item.Id));
    }

    [Fact]
    public void RestoreRejectsNullVersionDuplicatesAndDuplicateKeys()
    {
        var fixture = CreateFixture();
        Assert.Equal(ReputationErrorCodes.InvalidSnapshot, fixture.Framework.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(ReputationErrorCodes.InvalidSnapshot, fixture.Framework.RestoreSnapshot(new ReputationFrameworkSnapshot(1, null)).Error?.Code);
        Assert.Equal(ReputationErrorCodes.UnsupportedSnapshotVersion, fixture.Framework.RestoreSnapshot(new ReputationFrameworkSnapshot(0, [])).Error?.Code);
        var one = Record(Guid.Parse("10000000-0000-0000-0000-000000000000"), fixture);
        var two = Record(Guid.Parse("20000000-0000-0000-0000-000000000000"), fixture);
        Assert.Equal(ReputationErrorCodes.DuplicateId, fixture.Framework.RestoreSnapshot(new ReputationFrameworkSnapshot(1, [one, one])).Error?.Code);
        Assert.Equal(ReputationErrorCodes.DuplicateActiveKey, fixture.Framework.RestoreSnapshot(new ReputationFrameworkSnapshot(1, [one, two])).Error?.Code);
    }

    private static ReputationSnapshot Record(Guid id, Fixture fixture) => new(new ReputationId(id), fixture.Subject, Regional,
        fixture.Audience, Standing, 1, ReputationLifecycleState.Active, new WorldTimestamp(1), new WorldTimestamp(1), null);
    private static Fixture CreateFixture(IReputationEventSink? sink = null)
    {
        var entities = new EntityRegistry();
        var subject = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var audience = entities.Create(new EntityCategory("Region"), 0).Value!.Id;
        return new Fixture(entities, new ReputationFramework(entities, events: sink), subject, audience);
    }
    private sealed record Fixture(EntityRegistry Entities, ReputationFramework Framework, EntityId Subject, EntityId Audience);
    private sealed class FailingSink : IReputationEventSink
    {
        public bool Fail { get; set; }
        public ReputationResult Publish(ReputationDomainEvent domainEvent) => Fail
            ? ReputationResult.Failure("event.failed", "Fixture event failed.") : ReputationResult.Success();
    }
}
