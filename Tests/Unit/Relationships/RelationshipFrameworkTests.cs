using Mythos.Framework.Entities;
using Mythos.Framework.Relationships;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Relationships;

public sealed class RelationshipFrameworkTests
{
    private static readonly RelationshipKindId Acquaintance = new("acquaintance");
    private static readonly RelationshipDimensionId Trust = new("trust");

    [Fact]
    public void CreateAndDirectedQueriesPreserveIdentity()
    {
        var fixture = CreateFixture();
        var created = fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(4), "event:met");

        Assert.True(created.IsSuccess);
        Assert.Single(fixture.Framework.QueryFrom(fixture.Left));
        Assert.Single(fixture.Framework.QueryToward(fixture.Right));
        Assert.Empty(fixture.Framework.QueryFrom(fixture.Right));
        Assert.Equal(created.Value, fixture.Framework.Find(created.Value!.Id).Value);
    }

    [Fact]
    public void CreateRejectsSelfMissingDestroyedAndDuplicateActiveTuple()
    {
        var fixture = CreateFixture();
        var missing = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var destroyed = fixture.Entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        Assert.True(fixture.Entities.Destroy(destroyed, 1).IsSuccess);

        Assert.Equal(RelationshipErrorCodes.InvalidReference,
            fixture.Framework.Create(fixture.Left, fixture.Left, Acquaintance, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(RelationshipErrorCodes.InvalidReference,
            fixture.Framework.Create(fixture.Left, missing, Acquaintance, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(RelationshipErrorCodes.InvalidReference,
            fixture.Framework.Create(fixture.Left, destroyed, Acquaintance, new WorldTimestamp(1)).Error?.Code);
        Assert.True(fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(1)).IsSuccess);
        Assert.Equal(RelationshipErrorCodes.DuplicateActiveTuple,
            fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(2)).Error?.Code);
    }

    [Fact]
    public void DimensionMutationsAreBoundedExplicitAndTimestamped()
    {
        var fixture = CreateFixture();
        var id = fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(2)).Value!.Id;

        Assert.True(fixture.Framework.SetDimension(id, Trust, 10, new WorldTimestamp(3), "event:helped").IsSuccess);
        Assert.True(fixture.Framework.ApplyDelta(id, Trust, -4, new WorldTimestamp(4)).IsSuccess);
        Assert.Equal(6, fixture.Framework.Find(id).Value!.Dimensions![Trust.Value]);
        Assert.Equal(RelationshipErrorCodes.InvalidDimension,
            fixture.Framework.ApplyDelta(id, Trust, 1000, new WorldTimestamp(5)).Error?.Code);
        Assert.Equal(RelationshipErrorCodes.InvalidTimestamp,
            fixture.Framework.SetDimension(id, Trust, 1, new WorldTimestamp(2)).Error?.Code);
        Assert.True(fixture.Framework.RemoveDimension(id, Trust, new WorldTimestamp(5)).IsSuccess);
        Assert.Empty(fixture.Framework.Find(id).Value!.Dimensions!);
    }

    [Fact]
    public void RetiredRelationshipsRemainQueryableButImmutableAndAllowReplacementTuple()
    {
        var fixture = CreateFixture();
        var id = fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(1)).Value!.Id;

        Assert.True(fixture.Framework.Retire(id, new WorldTimestamp(2), "event:ended").IsSuccess);
        Assert.Equal(RelationshipLifecycleState.Retired, fixture.Framework.Find(id).Value!.LifecycleState);
        Assert.Equal(RelationshipErrorCodes.InvalidLifecycle,
            fixture.Framework.SetDimension(id, Trust, 1, new WorldTimestamp(3)).Error?.Code);
        Assert.True(fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(3)).IsSuccess);
        Assert.Equal(2, fixture.Framework.QueryInvolving(fixture.Left).Count);
    }

    [Fact]
    public void EventFailureLeavesCreateAndMutationUnchanged()
    {
        var sink = new RecordingSink();
        var fixture = CreateFixture(sink);
        sink.Fail = true;
        Assert.Equal(RelationshipErrorCodes.EventPublicationFailed,
            fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(0, fixture.Framework.Count);

        sink.Fail = false;
        var id = fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(1)).Value!.Id;
        sink.Fail = true;
        Assert.Equal(RelationshipErrorCodes.EventPublicationFailed,
            fixture.Framework.SetDimension(id, Trust, 20, new WorldTimestamp(2)).Error?.Code);
        Assert.Empty(fixture.Framework.Find(id).Value!.Dimensions!);
    }

    [Fact]
    public void SnapshotAndQueriesUseStableOrderingAndDefensiveDimensions()
    {
        var fixture = CreateFixture();
        var first = fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(1)).Value!;
        Assert.True(fixture.Framework.SetDimension(first.Id, new RelationshipDimensionId("zeta"), 1, new WorldTimestamp(2)).IsSuccess);
        Assert.True(fixture.Framework.SetDimension(first.Id, new RelationshipDimensionId("alpha"), 2, new WorldTimestamp(3)).IsSuccess);
        var reverse = fixture.Framework.Create(fixture.Right, fixture.Left, Acquaintance, new WorldTimestamp(1)).Value!;

        var exported = fixture.Framework.ExportSnapshot();
        Assert.Equal(exported.Relationships!.OrderBy(item => item.Id.Value), exported.Relationships);
        Assert.Equal(new[] { "alpha", "zeta" }, fixture.Framework.Find(first.Id).Value!.Dimensions!.Keys);
        Assert.Equal(new[] { first.Id, reverse.Id }.OrderBy(id => id.Value), fixture.Framework.QueryByKind(Acquaintance).Select(item => item.Id));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)fixture.Framework.Find(first.Id).Value!.Dimensions!).Add("mutable", 1));

        var source = exported.Relationships!.ToList();
        var defensive = new RelationshipFrameworkSnapshot(RelationshipFrameworkSnapshot.CurrentVersion, source);
        source.Clear();
        Assert.Equal(2, defensive.Relationships!.Count);
    }

    [Fact]
    public void RestoreRoundTripsAndIsAtomicOnMalformedInput()
    {
        var fixture = CreateFixture();
        var original = fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(1)).Value!;
        Assert.True(fixture.Framework.SetDimension(original.Id, Trust, 42, new WorldTimestamp(2), "event:gift").IsSuccess);
        var snapshot = fixture.Framework.ExportSnapshot();
        var restored = new RelationshipFramework(fixture.Entities);

        Assert.True(restored.RestoreSnapshot(snapshot).IsSuccess);
        AssertEquivalent(snapshot.Relationships![0], restored.ExportSnapshot().Relationships![0]);
        var before = restored.ExportSnapshot();
        var malformed = new RelationshipSnapshot(new RelationshipId(Guid.NewGuid()), fixture.Left, fixture.Left,
            Acquaintance, RelationshipLifecycleState.Active, new Dictionary<string, int>(), new WorldTimestamp(0), new WorldTimestamp(0), null);
        Assert.Equal(RelationshipErrorCodes.InvalidReference,
            restored.RestoreSnapshot(new RelationshipFrameworkSnapshot(RelationshipFrameworkSnapshot.CurrentVersion, [malformed])).Error?.Code);
        AssertEquivalent(before.Relationships![0], restored.ExportSnapshot().Relationships![0]);
    }

    [Fact]
    public void RestoreRejectsNullUnsupportedDuplicatesAndDuplicateActiveTuples()
    {
        var fixture = CreateFixture();
        Assert.Equal(RelationshipErrorCodes.InvalidSnapshot, fixture.Framework.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(RelationshipErrorCodes.InvalidSnapshot,
            fixture.Framework.RestoreSnapshot(new RelationshipFrameworkSnapshot(RelationshipFrameworkSnapshot.CurrentVersion, null)).Error?.Code);
        Assert.Equal(RelationshipErrorCodes.UnsupportedSnapshotVersion,
            fixture.Framework.RestoreSnapshot(new RelationshipFrameworkSnapshot(0, [])).Error?.Code);
        var one = Record(Guid.Parse("10000000-0000-0000-0000-000000000000"), fixture.Left, fixture.Right);
        var two = Record(Guid.Parse("20000000-0000-0000-0000-000000000000"), fixture.Left, fixture.Right);
        Assert.Equal(RelationshipErrorCodes.DuplicateId,
            fixture.Framework.RestoreSnapshot(new RelationshipFrameworkSnapshot(1, [one, one])).Error?.Code);
        Assert.Equal(RelationshipErrorCodes.DuplicateActiveTuple,
            fixture.Framework.RestoreSnapshot(new RelationshipFrameworkSnapshot(1, [one, two])).Error?.Code);
    }

    [Fact]
    public void ValidationDetectsParticipantsDestroyedAfterCreation()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Create(fixture.Left, fixture.Right, Acquaintance, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Entities.Destroy(fixture.Right, 2).IsSuccess);
        Assert.Equal(RelationshipErrorCodes.InvalidReference, fixture.Framework.ValidateReferences().Error?.Code);
        Assert.Contains("invalid_reference", fixture.Framework.Inspect(fixture.Framework.ExportSnapshot().Relationships![0].Id).Value!.ValidationStatus);
    }

    private static RelationshipSnapshot Record(Guid id, EntityId source, EntityId target) => new(new RelationshipId(id), source, target,
        Acquaintance, RelationshipLifecycleState.Active, new Dictionary<string, int>(), new WorldTimestamp(0), new WorldTimestamp(0), null);

    private static void AssertEquivalent(RelationshipSnapshot expected, RelationshipSnapshot actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.SourceEntityId, actual.SourceEntityId);
        Assert.Equal(expected.TargetEntityId, actual.TargetEntityId);
        Assert.Equal(expected.KindId, actual.KindId);
        Assert.Equal(expected.LifecycleState, actual.LifecycleState);
        Assert.Equal(expected.Dimensions, actual.Dimensions);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.LastChangedAt, actual.LastChangedAt);
        Assert.Equal(expected.ProvenanceReference, actual.ProvenanceReference);
    }

    private static Fixture CreateFixture(IRelationshipEventSink? sink = null)
    {
        var entities = new EntityRegistry();
        var left = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var right = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        return new Fixture(entities, new RelationshipFramework(entities, events: sink), left, right);
    }

    private sealed record Fixture(EntityRegistry Entities, RelationshipFramework Framework, EntityId Left, EntityId Right);

    private sealed class RecordingSink : IRelationshipEventSink
    {
        public bool Fail { get; set; }
        public RelationshipResult Publish(RelationshipDomainEvent domainEvent) => Fail
            ? RelationshipResult.Failure("event.failed", "Fixture event sink failed.")
            : RelationshipResult.Success();
    }
}
