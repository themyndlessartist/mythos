using Mythos.Framework.Entities;
using Mythos.Framework.Information;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Information;

public sealed class InformationFrameworkTests
{
    private static readonly InformationTypeId Location = new("location-claim");

    [Fact]
    public void InformationFactAndKnownAwarenessPreserveThreeLayers()
    {
        var fixture = CreateFixture();
        var information = fixture.Framework.Create(Location, fixture.Subject, fixture.Object,
            new Dictionary<string, string> { ["state"] = "present" }, new WorldTimestamp(1), "observation:1").Value!;
        var fact = fixture.Framework.DeclareFact(information.Id, new WorldTimestamp(1), "world:event").Value!;

        Assert.True(fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Known,
            1000, new WorldTimestamp(2), fixture.Subject).IsSuccess);
        Assert.True(fixture.Framework.IsAuthoritative(information.Id));
        Assert.Equal(fact, fixture.Framework.FindFactFor(information.Id).Value);
        Assert.Equal(EpistemicStance.Known, fixture.Framework.FindAwareness(fixture.Knower, information.Id).Value!.Stance);
    }

    [Fact]
    public void UnverifiedInformationSupportsBeliefButNotKnowledge()
    {
        var fixture = CreateFixture();
        var information = fixture.Framework.Create(Location, fixture.Subject, fixture.Object,
            new Dictionary<string, string> { ["state"] = "absent" }, new WorldTimestamp(1)).Value!;

        Assert.True(fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Believed,
            700, new WorldTimestamp(2)).IsSuccess);
        Assert.Equal(InformationErrorCodes.InvalidAwareness,
            fixture.Framework.SetAwareness(fixture.Subject, information.Id, EpistemicStance.Known,
                700, new WorldTimestamp(2)).Error?.Code);
        Assert.False(fixture.Framework.IsAuthoritative(information.Id));
    }

    [Fact]
    public void CreationRejectsEmptyMalformedAndDestroyedReferences()
    {
        var fixture = CreateFixture();
        var destroyed = fixture.Entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        Assert.True(fixture.Entities.Destroy(destroyed, 1).IsSuccess);

        Assert.Equal(InformationErrorCodes.InvalidRecord,
            fixture.Framework.Create(Location, null, null, new Dictionary<string, string>(), new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(InformationErrorCodes.InvalidRecord,
            fixture.Framework.Create(Location, null, null, new Dictionary<string, string> { [" bad"] = "value" }, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(InformationErrorCodes.InvalidReference,
            fixture.Framework.Create(Location, destroyed, null, null, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(0, fixture.Framework.InformationCount);
    }

    [Fact]
    public void FactAndAwarenessValidationRejectsDuplicatesRangesAndTimeReversal()
    {
        var fixture = CreateFixture();
        var information = fixture.Framework.Create(Location, fixture.Subject, fixture.Object, null, new WorldTimestamp(2)).Value!;
        Assert.Equal(InformationErrorCodes.InvalidTimestamp,
            fixture.Framework.DeclareFact(information.Id, new WorldTimestamp(1)).Error?.Code);
        Assert.True(fixture.Framework.DeclareFact(information.Id, new WorldTimestamp(2)).IsSuccess);
        Assert.Equal(InformationErrorCodes.DuplicateFact,
            fixture.Framework.DeclareFact(information.Id, new WorldTimestamp(3)).Error?.Code);
        Assert.Equal(InformationErrorCodes.InvalidAwareness,
            fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Believed, 1001, new WorldTimestamp(3)).Error?.Code);
        Assert.True(fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Believed, 500, new WorldTimestamp(4)).IsSuccess);
        Assert.Equal(InformationErrorCodes.InvalidTimestamp,
            fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Known, 900, new WorldTimestamp(3)).Error?.Code);
    }

    [Fact]
    public void AwarenessCanChangeAndBeForgottenWithoutDeletingInformation()
    {
        var fixture = CreateFixture();
        var information = fixture.Framework.Create(Location, fixture.Subject, fixture.Object, null, new WorldTimestamp(1)).Value!;
        Assert.True(fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Believed, 400, new WorldTimestamp(2)).IsSuccess);
        Assert.True(fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Disbelieved, 600, new WorldTimestamp(3)).IsSuccess);
        Assert.Equal(new WorldTimestamp(2), fixture.Framework.FindAwareness(fixture.Knower, information.Id).Value!.AcquiredAt);
        Assert.True(fixture.Framework.Forget(fixture.Knower, information.Id, new WorldTimestamp(4)).IsSuccess);
        Assert.Equal(InformationErrorCodes.NotFound, fixture.Framework.FindAwareness(fixture.Knower, information.Id).Error?.Code);
        Assert.True(fixture.Framework.Find(information.Id).IsSuccess);
    }

    [Fact]
    public void QueriesAndSnapshotsAreDeterministicAndDefensive()
    {
        var fixture = CreateFixture();
        var first = fixture.Framework.Create(Location, fixture.Subject, fixture.Object,
            new Dictionary<string, string> { ["zeta"] = "z", ["alpha"] = "a" }, new WorldTimestamp(1)).Value!;
        var second = fixture.Framework.Create(Location, fixture.Object, fixture.Subject, null, new WorldTimestamp(1)).Value!;
        Assert.True(fixture.Framework.SetAwareness(fixture.Knower, first.Id, EpistemicStance.Believed, 5, new WorldTimestamp(2)).IsSuccess);

        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id.Value), fixture.Framework.QueryByType(Location).Select(item => item.Id));
        Assert.Equal(new[] { "alpha", "zeta" }, first.Attributes!.Keys);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)first.Attributes).Add("mutate", "no"));
        var exported = fixture.Framework.ExportSnapshot();
        var source = exported.Information!.ToList();
        var defensive = new InformationFrameworkSnapshot(1, source, exported.Facts, exported.Awareness);
        source.Clear();
        Assert.Equal(2, defensive.Information!.Count);
    }

    [Fact]
    public void EventFailureLeavesAllDomainStateUnchanged()
    {
        var sink = new FailingSink();
        var fixture = CreateFixture(sink);
        sink.Fail = true;
        Assert.Equal(InformationErrorCodes.EventPublicationFailed,
            fixture.Framework.Create(Location, fixture.Subject, fixture.Object, null, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(0, fixture.Framework.InformationCount);
        sink.Fail = false;
        var information = fixture.Framework.Create(Location, fixture.Subject, fixture.Object, null, new WorldTimestamp(1)).Value!;
        sink.Fail = true;
        Assert.Equal(InformationErrorCodes.EventPublicationFailed,
            fixture.Framework.DeclareFact(information.Id, new WorldTimestamp(1)).Error?.Code);
        Assert.Equal(0, fixture.Framework.FactCount);
        Assert.Equal(InformationErrorCodes.EventPublicationFailed,
            fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Believed, 1, new WorldTimestamp(2)).Error?.Code);
        Assert.Equal(0, fixture.Framework.AwarenessCount);
    }

    [Fact]
    public void RestoreRoundTripsAndRejectsMalformedStateAtomically()
    {
        var fixture = CreateFixture();
        var information = fixture.Framework.Create(Location, fixture.Subject, fixture.Object,
            new Dictionary<string, string> { ["state"] = "present" }, new WorldTimestamp(1)).Value!;
        Assert.True(fixture.Framework.DeclareFact(information.Id, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Framework.SetAwareness(fixture.Knower, information.Id, EpistemicStance.Known, 900, new WorldTimestamp(2)).IsSuccess);
        var snapshot = fixture.Framework.ExportSnapshot();
        var restored = new InformationFramework(fixture.Entities);
        Assert.True(restored.RestoreSnapshot(snapshot).IsSuccess);
        Assert.Equal(information.Id, restored.ExportSnapshot().Information!.Single().Id);
        var before = restored.ExportSnapshot();
        var invalidAwareness = snapshot.Awareness!.Single() with { Confidence = 1001 };
        Assert.Equal(InformationErrorCodes.InvalidAwareness,
            restored.RestoreSnapshot(new InformationFrameworkSnapshot(1, snapshot.Information, snapshot.Facts, [invalidAwareness])).Error?.Code);
        Assert.Equal(before.Awareness, restored.ExportSnapshot().Awareness);
    }

    [Fact]
    public void ValidationDetectsEntityReferenceDrift()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.Create(Location, fixture.Subject, fixture.Object, null, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Entities.Destroy(fixture.Object, 2).IsSuccess);
        Assert.Equal(InformationErrorCodes.InvalidReference, fixture.Framework.ValidateReferences().Error?.Code);
    }

    private static Fixture CreateFixture(IInformationEventSink? sink = null)
    {
        var entities = new EntityRegistry();
        var subject = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var obj = entities.Create(new EntityCategory("Region"), 0).Value!.Id;
        var knower = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        return new Fixture(entities, new InformationFramework(entities, events: sink), subject, obj, knower);
    }

    private sealed record Fixture(EntityRegistry Entities, InformationFramework Framework, EntityId Subject, EntityId Object, EntityId Knower);
    private sealed class FailingSink : IInformationEventSink
    {
        public bool Fail { get; set; }
        public InformationResult Publish(InformationDomainEvent domainEvent) => Fail
            ? InformationResult.Failure("event.failed", "Fixture event failed.") : InformationResult.Success();
    }
}
