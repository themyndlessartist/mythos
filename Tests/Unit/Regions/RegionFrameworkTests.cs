using Mythos.Framework.Entities;
using Mythos.Framework.Regions;

namespace Mythos.Framework.UnitTests.Regions;

public sealed class RegionFrameworkTests
{
    private static readonly RegionCategory World = new("WorldScope");
    private static readonly RegionCategory NeutralArea = new("NeutralArea");
    private static readonly EntityCategory Character = new("Character");

    [Fact]
    public void CreatesRootAndConfigurableNestedNeutralRegionsUsingEntityHierarchy()
    {
        var fixture = CreateFixture();
        var nested = fixture.Regions.CreateRegion(new RegionCategory("CustomNestedScope"), fixture.ChildId, 3);

        Assert.True(nested.IsSuccess);
        Assert.Equal("CustomNestedScope", nested.Value!.Category.Value);
        Assert.Equal(fixture.ChildId, fixture.Entities.Find(nested.Value.Id).Value!.ParentId);
        Assert.True(fixture.Regions.Contains(fixture.RootId, nested.Value.Id));
        Assert.Equal([fixture.ChildId, nested.Value.Id], fixture.Regions.QueryDescendants(fixture.RootId).Select(r => r.Id));
    }

    [Fact]
    public void HierarchyRejectsCyclesSelfParentingAndOrphansAtomically()
    {
        var fixture = CreateFixture();

        var cycle = fixture.Regions.AssignParent(fixture.RootId, fixture.ChildId);
        var self = fixture.Regions.AssignParent(fixture.ChildId, fixture.ChildId);
        var orphan = fixture.Regions.AssignParent(fixture.ChildId, null);

        Assert.Equal(RegionErrorCodes.InvalidHierarchy, cycle.Error!.Code);
        Assert.Equal(RegionErrorCodes.HierarchyCycle, self.Error!.Code);
        Assert.Equal(RegionErrorCodes.InvalidHierarchy, orphan.Error!.Code);
        Assert.Equal(fixture.RootId, fixture.Regions.Find(fixture.ChildId).Value!.ParentId);
        Assert.Null(fixture.Regions.Find(fixture.RootId).Value!.ParentId);
    }

    [Fact]
    public void AdjacencyIsSymmetricMetadataAndDoesNotChangeContainment()
    {
        var fixture = CreateFixture();
        var sibling = fixture.Regions.CreateRegion(NeutralArea, fixture.RootId, 4).Value!;

        Assert.True(fixture.Regions.AddAdjacency(fixture.ChildId, sibling.Id, new Dictionary<string, string> { ["transition"] = "gate-a" }).IsSuccess);

        Assert.Equal(sibling.Id, Assert.Single(fixture.Regions.QueryAdjacent(fixture.ChildId)).Id);
        Assert.Equal(fixture.ChildId, Assert.Single(fixture.Regions.QueryAdjacent(sibling.Id)).Id);
        Assert.False(fixture.Regions.Contains(fixture.ChildId, sibling.Id));
    }

    [Fact]
    public void AdjacencyRejectsSelfMissingAndTerminalReferences()
    {
        var fixture = CreateFixture();
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.AddAdjacency(fixture.ChildId, fixture.ChildId).Error!.Code);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.AddAdjacency(fixture.ChildId, Id(99)).Error!.Code);
        Assert.True(fixture.Entities.Retire(fixture.ChildId, 10).IsSuccess);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.AddAdjacency(fixture.RootId, fixture.ChildId).Error!.Code);
    }

    [Fact]
    public void AssignmentAndTransferValidateSourceDestinationAndLifecycle()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Regions.AssignEntity(fixture.CharacterId, fixture.ChildId).IsSuccess);

        var wrongSource = fixture.Regions.TransferEntity(fixture.CharacterId, fixture.RootId, fixture.ChildId);
        var transfer = fixture.Regions.TransferEntity(fixture.CharacterId, fixture.ChildId, fixture.RootId);

        Assert.Equal(RegionErrorCodes.InvalidState, wrongSource.Error!.Code);
        Assert.True(transfer.IsSuccess);
        Assert.Equal(fixture.RootId, fixture.Entities.Find(fixture.CharacterId).Value!.RegionId);
        Assert.True(fixture.Entities.Retire(fixture.CharacterId, 20).IsSuccess);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.AssignEntity(fixture.CharacterId, fixture.ChildId).Error!.Code);
    }

    [Fact]
    public void AssignmentRejectsMissingWrongCategoryAndTerminalRegionReferences()
    {
        var fixture = CreateFixture();
        var wrongCategory = fixture.Entities.Create(Character, 4).Value!.Id;

        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.AssignEntity(fixture.CharacterId, Id(99)).Error!.Code);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.AssignEntity(fixture.CharacterId, wrongCategory).Error!.Code);
        Assert.True(fixture.Entities.Retire(fixture.ChildId, 10).IsSuccess);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.AssignEntity(fixture.CharacterId, fixture.ChildId).Error!.Code);
    }

    [Fact]
    public void ActivationAbstractionAndSimulationOwnershipAreValidated()
    {
        var fixture = CreateFixture();
        Assert.Equal(RegionSimulationState.Abstract, fixture.Regions.Find(fixture.ChildId).Value!.SimulationState);
        Assert.True(fixture.Regions.SetSimulationState(fixture.ChildId, RegionSimulationState.Active).IsSuccess);
        Assert.True(fixture.Regions.SetSimulationState(fixture.ChildId, RegionSimulationState.Abstract).IsSuccess);
        Assert.True(fixture.Regions.SetSimulationOwner(fixture.ChildId, fixture.RootId).IsSuccess);
        Assert.Equal(fixture.RootId, fixture.Regions.ResolveSimulationOwner(fixture.ChildId).Value);

        var sibling = fixture.Regions.CreateRegion(NeutralArea, fixture.RootId, 5).Value!;
        Assert.Equal(RegionErrorCodes.InvalidState, fixture.Regions.SetSimulationOwner(fixture.ChildId, sibling.Id).Error!.Code);
        Assert.Equal(RegionErrorCodes.InvalidState, fixture.Regions.SetSimulationState(fixture.ChildId, (RegionSimulationState)99).Error!.Code);
    }

    [Fact]
    public void ReparentingCannotInvalidateSimulationOwnership()
    {
        var fixture = CreateFixture();
        var grandchild = fixture.Regions.CreateRegion(NeutralArea, fixture.ChildId, 4).Value!;
        var sibling = fixture.Regions.CreateRegion(NeutralArea, fixture.RootId, 5).Value!;
        Assert.True(fixture.Regions.SetSimulationOwner(grandchild.Id, fixture.ChildId).IsSuccess);

        var result = fixture.Regions.AssignParent(grandchild.Id, sibling.Id);

        Assert.Equal(RegionErrorCodes.InvalidState, result.Error!.Code);
        Assert.Equal(fixture.ChildId, fixture.Regions.Find(grandchild.Id).Value!.ParentId);
        Assert.Equal(fixture.ChildId, fixture.Entities.Find(grandchild.Id).Value!.ParentId);
    }

    [Fact]
    public void QueriesAndDiagnosticsAreDeterministic()
    {
        var ids = new[] { Id(30), Id(20), Id(40), Id(10), Id(50) };
        var entities = new EntityRegistry(new SequenceIdGenerator(ids));
        var regions = new RegionFramework(entities);
        var root = regions.CreateRoot(World, 0).Value!;
        var high = regions.CreateRegion(NeutralArea, root.Id, 1).Value!;
        var low = regions.CreateRegion(NeutralArea, root.Id, 1).Value!;
        var character = entities.Create(Character, 1).Value!;
        regions.AssignEntity(character.Id, low.Id);
        regions.AddAdjacency(high.Id, low.Id);

        Assert.Equal([Id(20), Id(40)], regions.QueryChildren(root.Id).Select(r => r.Id));
        Assert.Equal([Id(20), Id(40)], regions.QueryDescendants(root.Id).Select(r => r.Id));
        var diagnostic = regions.Inspect(low.Id).Value!;
        Assert.Equal([high.Id], diagnostic.AdjacentRegionIds);
        Assert.Equal([character.Id], diagnostic.AssignedEntityIds);
    }

    [Fact]
    public void InspectionReturnsStructuredFailuresForMissingAndTerminalRegions()
    {
        var fixture = CreateFixture();

        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.ResolveSimulationOwner(Id(99)).Error?.Code);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.Inspect(Id(99)).Error?.Code);
        Assert.True(fixture.Entities.Retire(fixture.ChildId, 10).IsSuccess);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.ResolveSimulationOwner(fixture.ChildId).Error?.Code);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.Inspect(fixture.ChildId).Error?.Code);
    }

    [Fact]
    public void ValidateReferencesDetectsEntityLifecycleDrift()
    {
        var fixture = CreateFixture();
        fixture.Regions.AssignEntity(fixture.CharacterId, fixture.ChildId);
        Assert.True(fixture.Entities.Retire(fixture.ChildId, 10).IsSuccess);

        var result = fixture.Regions.ValidateReferences();

        Assert.Equal(RegionErrorCodes.InvalidReference, result.Error?.Code);
    }

    [Fact]
    public void ScopedAssignmentValidationIgnoresUnrelatedRegionDrift()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Regions.AssignEntity(fixture.CharacterId, fixture.ChildId).IsSuccess);
        var unrelated = fixture.Regions.CreateRegion(NeutralArea, fixture.RootId, 1).Value!;
        Assert.True(fixture.Entities.Retire(unrelated.Id, 10).IsSuccess);

        Assert.True(fixture.Regions.ValidateAssignment(fixture.CharacterId).IsSuccess);
        Assert.Equal(RegionErrorCodes.InvalidReference, fixture.Regions.ValidateReferences().Error?.Code);
    }

    [Fact]
    public void ExportIsDefensiveDeterministicAndRoundTripsIntoFreshRuntime()
    {
        var source = CreateFixture();
        source.Regions.AssignEntity(source.CharacterId, source.ChildId);
        source.Regions.SetSimulationState(source.ChildId, RegionSimulationState.Active);
        source.Regions.AddAdjacency(source.RootId, source.ChildId, new Dictionary<string, string> { ["kind"] = "lookup" });
        var snapshot = source.Regions.ExportSnapshot();

        var targetEntities = new EntityRegistry();
        foreach (var entity in source.Entities.ExportSnapshots()) Assert.True(targetEntities.Register(entity).IsSuccess);
        var target = new RegionFramework(targetEntities);
        Assert.True(target.Restore(snapshot).IsSuccess);

        AssertSnapshotEqual(snapshot, target.ExportSnapshot());
        Assert.Equal(source.ChildId, targetEntities.Find(source.CharacterId).Value!.RegionId);
    }

    [Theory]
    [MemberData(nameof(MalformedSnapshots))]
    public void RestoreRejectsMalformedSnapshotsWithoutChangingLiveState(Func<Fixture, RegionFrameworkSnapshot> malformed)
    {
        var fixture = CreateFixture();
        fixture.Regions.AssignEntity(fixture.CharacterId, fixture.ChildId);
        var before = fixture.Regions.ExportSnapshot();

        var result = fixture.Regions.Restore(malformed(fixture));

        Assert.False(result.IsSuccess);
        AssertSnapshotEqual(before, fixture.Regions.ExportSnapshot());
        Assert.Equal(fixture.ChildId, fixture.Entities.Find(fixture.CharacterId).Value!.RegionId);
    }

    public static TheoryData<Func<Fixture, RegionFrameworkSnapshot>> MalformedSnapshots() => new()
    {
        fixture => new RegionFrameworkSnapshot(99, fixture.RootId, [], [], []),
        fixture => new RegionFrameworkSnapshot(1, fixture.RootId, null, [], []),
        fixture => new RegionFrameworkSnapshot(1, fixture.RootId,
            [Record(fixture.RootId, null), Record(fixture.ChildId, fixture.ChildId)], [], []),
        fixture => new RegionFrameworkSnapshot(1, fixture.RootId,
            [Record(fixture.RootId, null)], [new RegionAdjacencySnapshot(fixture.RootId, Id(99), new Dictionary<string, string>())], []),
        fixture => new RegionFrameworkSnapshot(1, fixture.RootId,
            [Record(fixture.RootId, null), Record(fixture.ChildId, fixture.RootId)], [],
            [new RegionAssignmentSnapshot(fixture.CharacterId, Id(99))]),
        fixture => new RegionFrameworkSnapshot(1, fixture.RootId, [null!], [], []),
        fixture => new RegionFrameworkSnapshot(1, fixture.RootId,
            [Record(fixture.RootId, null), Record(fixture.ChildId, fixture.RootId)], [null!], []),
        fixture => new RegionFrameworkSnapshot(1, fixture.RootId,
            [Record(fixture.RootId, null), Record(fixture.ChildId, fixture.RootId)], [], [null!]),
    };

    private static RegionRecordSnapshot Record(EntityId id, EntityId? parent) =>
        new(id, NeutralArea, parent, RegionSimulationState.Abstract, parent ?? id, null, new Dictionary<string, string>());

    private static void AssertSnapshotEqual(RegionFrameworkSnapshot expected, RegionFrameworkSnapshot actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.RootRegionId, actual.RootRegionId);
        Assert.Equal(expected.Regions!.Select(region => (region.Id, region.Category, region.ParentId, region.SimulationState,
            region.SimulationOwnerId, region.BoundaryReference,
            string.Join(';', region.Metadata!.Select(item => $"{item.Key}={item.Value}")))),
            actual.Regions!.Select(region => (region.Id, region.Category, region.ParentId, region.SimulationState,
                region.SimulationOwnerId, region.BoundaryReference,
                string.Join(';', region.Metadata!.Select(item => $"{item.Key}={item.Value}")))));
        Assert.Equal(expected.Adjacency!.Select(edge => (edge.RegionId, edge.AdjacentRegionId,
            string.Join(';', edge.Metadata!.Select(item => $"{item.Key}={item.Value}")))),
            actual.Adjacency!.Select(edge => (edge.RegionId, edge.AdjacentRegionId,
                string.Join(';', edge.Metadata!.Select(item => $"{item.Key}={item.Value}")))));
        Assert.Equal(expected.Assignments, actual.Assignments);
    }

    private static Fixture CreateFixture()
    {
        var entities = new EntityRegistry(new SequenceIdGenerator(Id(1), Id(2), Id(3), Id(4), Id(5), Id(6)));
        var regions = new RegionFramework(entities);
        var root = regions.CreateRoot(World, 0, "boundary:world", new Dictionary<string, string> { ["neutral"] = "true" }).Value!;
        var child = regions.CreateRegion(NeutralArea, root.Id, 1).Value!;
        var character = entities.Create(Character, 2).Value!;
        return new Fixture(entities, regions, root.Id, child.Id, character.Id);
    }

    private static EntityId Id(int value) => new(new Guid(value, 0, 0, new byte[8]));

    public sealed record Fixture(EntityRegistry Entities, RegionFramework Regions, EntityId RootId, EntityId ChildId, EntityId CharacterId);

    private sealed class SequenceIdGenerator(params EntityId[] ids) : IEntityIdGenerator
    {
        private readonly Queue<EntityId> ids = new(ids);
        public EntityId Create() => ids.Dequeue();
    }
}
