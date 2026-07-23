using System.Text.Json;
using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Information;
using Mythos.Framework.Npcs;
using Mythos.Framework.Persistence;
using Mythos.Framework.Regions;
using Mythos.Framework.Relationships;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Persistence;

public sealed class WorldPersistenceTests
{
    [Fact]
    public void CompleteWorldRoundTripPreservesStableStateAndReferences()
    {
        var fixture = Fixture.Create();
        var storage = new InMemorySaveStorage();
        var persistence = new WorldPersistence(storage);

        Assert.True(persistence.Save("slot", "neutral-world", fixture.World).IsSuccess);
        var loaded = persistence.Load("slot", fixture.Context);

        Assert.True(loaded.IsSuccess, loaded.Error?.Message);
        Assert.Equal(fixture.CharacterId, loaded.Value!.Characters.QueryAll().Single().EntityId);
        Assert.Single(loaded.Value.Clock.Scheduler.ExportSnapshots());
        Assert.Equal(fixture.RegionId, loaded.Value.Entities.Find(fixture.CharacterId).Value!.RegionId);
        Assert.Equal(fixture.World.Characters.ExportSnapshot().Profiles!, loaded.Value.Characters.ExportSnapshot().Profiles!);
        Assert.Equal(fixture.World.Npcs.ExportSnapshot().Profiles!, loaded.Value.Npcs.ExportSnapshot().Profiles!);
        Assert.Equal(fixture.World.Relationships.ExportSnapshot().Relationships!.Single().Id,
            loaded.Value.Relationships.ExportSnapshot().Relationships!.Single().Id);
        Assert.Equal(fixture.World.Relationships.ExportSnapshot().Relationships!.Single().Dimensions,
            loaded.Value.Relationships.ExportSnapshot().Relationships!.Single().Dimensions);
        Assert.Equal(fixture.World.Information.ExportSnapshot().Information!.Single().Id,
            loaded.Value.Information.ExportSnapshot().Information!.Single().Id);
        Assert.Equal(fixture.World.Information.ExportSnapshot().Awareness, loaded.Value.Information.ExportSnapshot().Awareness);
        Assert.True(loaded.Value.Regions.ValidateReferences().IsSuccess);
        Assert.True(loaded.Value.Npcs.ValidateReferences().IsSuccess);
        Assert.True(persistence.Save("roundtrip", "neutral-world", loaded.Value).IsSuccess);
        var before = storage.Read("slot").Value!;
        var after = storage.Read("roundtrip").Value!;
        foreach (var key in before.Keys) Assert.Equal(before[key], after[key]);
    }

    [Fact]
    public void SameWorldProducesDeterministicPartitionBytes()
    {
        var fixture = Fixture.Create();
        var reverseFixture = Fixture.Create(reverseMetadataInsertion: true);
        var storage = new InMemorySaveStorage();
        var persistence = new WorldPersistence(storage);
        Assert.True(persistence.Save("a", "neutral-world", fixture.World).IsSuccess);
        Assert.True(persistence.Save("b", "neutral-world", reverseFixture.World).IsSuccess);
        var first = storage.Read("a").Value!;
        var second = storage.Read("b").Value!;
        Assert.Equal(first.Keys.Order(), second.Keys.Order());
        foreach (var key in first.Keys) Assert.Equal(first[key], second[key]);
    }

    [Fact]
    public void OversizedPartitionAndAggregateAreRejectedBeforeIntegrityWork()
    {
        var (fixture, storage, persistence) = Saved();
        var data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value);
        data["characters"] = new byte[PersistenceLimits.DomainPartitionBytes + 1];
        Replace(storage, data);
        var partitionFailure = persistence.Load("slot", fixture.Context);
        Assert.Equal(PersistenceErrorCodes.SizeLimitExceeded, partitionFailure.Error!.Code);
        Assert.Equal("characters", partitionFailure.Error.Partition);

        (fixture, storage, persistence) = Saved();
        data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value);
        data["characters"] = new byte[800_000];
        data["entities"] = new byte[800_000];
        data["npcs"] = new byte[800_000];
        Replace(storage, data);
        var aggregateFailure = persistence.Load("slot", fixture.Context);
        Assert.Equal(PersistenceErrorCodes.SizeLimitExceeded, aggregateFailure.Error!.Code);
        Assert.Null(aggregateFailure.Error.Partition);
    }

    [Fact]
    public void UndeclaredPhysicalPartitionAndUnknownJsonPropertyAreRejected()
    {
        var (fixture, storage, persistence) = Saved();
        var data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value);
        data["unknown"] = "{}"u8.ToArray();
        Replace(storage, data);
        Assert.Equal(PersistenceErrorCodes.InvalidData, persistence.Load("slot", fixture.Context).Error!.Code);

        (fixture, storage, persistence) = Saved();
        data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value);
        var json = System.Text.Encoding.UTF8.GetString(data["characters"]);
        data["characters"] = System.Text.Encoding.UTF8.GetBytes(json.Insert(json.Length - 1, ",\"unknown\":true"));
        RewriteManifest(data, "characters");
        Replace(storage, data);
        Assert.Equal(PersistenceErrorCodes.InvalidData, persistence.Load("slot", fixture.Context).Error!.Code);
    }

    [Fact]
    public void UnsupportedManifestVersionIsRejected()
    {
        var (fixture, storage, persistence) = Saved();
        var data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value);
        var manifest = JsonSerializer.Deserialize<SaveManifest>(data["manifest"], Options)! with { Version = 99 };
        data["manifest"] = JsonSerializer.SerializeToUtf8Bytes(manifest, Options);
        Replace(storage, data);
        var loaded = persistence.Load("slot", fixture.Context);
        Assert.False(loaded.IsSuccess);
        Assert.Equal(PersistenceErrorCodes.UnsupportedVersion, loaded.Error!.Code);
    }

    [Fact]
    public void CorruptionAndMissingPartitionAreRejected()
    {
        var (fixture, storage, persistence) = Saved();
        var data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value);
        data["npcs"][0] ^= 0xff;
        Replace(storage, data);
        Assert.Equal(PersistenceErrorCodes.CorruptData, persistence.Load("slot", fixture.Context).Error!.Code);

        (fixture, storage, persistence) = Saved();
        data = storage.Read("slot").Value!.Where(item => item.Key != "regions").ToDictionary(item => item.Key, item => item.Value);
        Replace(storage, data);
        Assert.Equal(PersistenceErrorCodes.MissingPartition, persistence.Load("slot", fixture.Context).Error!.Code);
    }

    [Fact]
    public void FailedCommitPreservesOriginalSaveAtomically()
    {
        var (fixture, storage, persistence) = Saved();
        var before = storage.Read("slot").Value!;
        storage.FailNextCommit = true;
        Assert.False(persistence.Save("slot", "changed-world", fixture.World).IsSuccess);
        var after = storage.Read("slot").Value!;
        foreach (var key in before.Keys) Assert.Equal(before[key], after[key]);
        Assert.True(persistence.Load("slot", fixture.Context).IsSuccess);
    }

    [Fact]
    public void StagedWriteFailureNeverCommitsAndPreservesPriorSlotBytes()
    {
        var fixture = Fixture.Create();
        var storage = new InMemorySaveStorage();
        var baselinePersistence = new WorldPersistence(storage);
        Assert.True(baselinePersistence.Save("slot", "neutral-world", fixture.World).IsSuccess);
        var before = storage.Read("slot").Value!;
        var failingStorage = new FailingWriteStorage(storage, failAtWrite: 3);

        var result = new WorldPersistence(failingStorage).Save("slot", "changed-world", fixture.World);

        Assert.False(result.IsSuccess);
        Assert.False(failingStorage.CommitCalled);
        var after = storage.Read("slot").Value!;
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var key in before.Keys) Assert.Equal(before[key], after[key]);
    }

    [Fact]
    public void MalformedAndUnresolvedReferenceLoadsDoNotExposePartialWorld()
    {
        var (fixture, storage, persistence) = Saved();
        var data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value);
        data["characters"] = "null"u8.ToArray();
        RewriteManifest(data);
        Replace(storage, data);
        Assert.Equal(PersistenceErrorCodes.InvalidData, persistence.Load("slot", fixture.Context).Error!.Code);

        (fixture, storage, persistence) = Saved();
        var brokenContext = fixture.Context with { NpcReferences = new References(false) };
        var broken = persistence.Load("slot", brokenContext);
        Assert.False(broken.IsSuccess);
        Assert.Equal(PersistenceErrorCodes.UnresolvedReference, broken.Error!.Code);
        Assert.True(fixture.World.Npcs.ValidateReferences().IsSuccess);
    }

    [Fact]
    public void BrokenRelationshipParticipantRejectsCompleteWorldLoad()
    {
        var fixture = Fixture.Create();
        var storage = new InMemorySaveStorage();
        var persistence = new WorldPersistence(storage);
        Assert.True(persistence.Save("slot", "neutral-world", fixture.World).IsSuccess);
        var data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value.ToArray());
        var originalTarget = "00000000-0000-0000-0000-000000000004"u8.ToArray();
        var missingTarget = "00000000-0000-0000-0000-000000000099"u8.ToArray();
        var index = data["relationships"].AsSpan().IndexOf(originalTarget);
        Assert.True(index >= 0);
        missingTarget.CopyTo(data["relationships"], index);
        RewriteManifest(data, "relationships");
        Replace(storage, data);

        var result = persistence.Load("slot", fixture.Context);

        Assert.Equal(PersistenceErrorCodes.UnresolvedReference, result.Error?.Code);
        Assert.Null(result.Value);
    }

    [Fact]
    public void BrokenInformationEntityReferenceRejectsCompleteWorldLoad()
    {
        var fixture = Fixture.Create();
        var storage = new InMemorySaveStorage();
        var persistence = new WorldPersistence(storage);
        Assert.True(persistence.Save("slot", "neutral-world", fixture.World).IsSuccess);
        var data = storage.Read("slot").Value!.ToDictionary(item => item.Key, item => item.Value.ToArray());
        var original = "00000000-0000-0000-0000-000000000002"u8.ToArray();
        var missing = "00000000-0000-0000-0000-000000000099"u8.ToArray();
        var index = data["information"].AsSpan().IndexOf(original);
        Assert.True(index >= 0);
        missing.CopyTo(data["information"], index);
        RewriteManifest(data, "information");
        Replace(storage, data);

        var result = persistence.Load("slot", fixture.Context);

        Assert.Equal(PersistenceErrorCodes.UnresolvedReference, result.Error?.Code);
        Assert.Null(result.Value);
    }

    private static (Fixture Fixture, InMemorySaveStorage Storage, WorldPersistence Persistence) Saved()
    {
        var fixture = Fixture.Create();
        var storage = new InMemorySaveStorage();
        var persistence = new WorldPersistence(storage);
        Assert.True(persistence.Save("slot", "neutral-world", fixture.World).IsSuccess);
        return (fixture, storage, persistence);
    }

    private static void Replace(InMemorySaveStorage storage, IReadOnlyDictionary<string, byte[]> data)
    {
        using var write = storage.BeginWrite("slot");
        foreach (var item in data) Assert.True(write.Write(item.Key, item.Value).IsSuccess);
        Assert.True(write.Commit().IsSuccess);
    }

    private static void RewriteManifest(Dictionary<string, byte[]> data, string partitionId = "characters")
    {
        var manifest = JsonSerializer.Deserialize<SaveManifest>(data["manifest"], Options)!;
        var descriptors = manifest.Partitions!.Select(item => item.Id == partitionId
            ? item with { Sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data[partitionId])) }
            : item).ToArray();
        data["manifest"] = JsonSerializer.SerializeToUtf8Bytes(manifest with { Partitions = descriptors }, Options);
    }

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private sealed record Fixture(PersistentWorldState World, PersistenceLoadContext Context, EntityId CharacterId, EntityId RegionId)
    {
        public static Fixture Create(bool reverseMetadataInsertion = false)
        {
            var entities = new EntityRegistry();
            var rootId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            var regionId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
            var characterId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000003"));
            var ownerId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000004"));
            var siblingId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000005"));
            var retiredId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000006"));
            Assert.True(entities.Register(Entity(rootId, "Region", null, null)).IsSuccess);
            Assert.True(entities.Register(Entity(regionId, "Region", rootId, null)).IsSuccess);
            Assert.True(entities.Register(Entity(ownerId, "Reference", null, null, EntityLifecycleState.Inactive)).IsSuccess);
            Assert.True(entities.Register(Entity(siblingId, "Region", rootId, null)).IsSuccess);
            Assert.True(entities.Register(new EntitySnapshot(retiredId, new("Reference"), EntityLifecycleState.Retired,
                [new("historical")], null, null, null, [new("archive")], 1, 6)).IsSuccess);
            Assert.True(entities.Register(new EntitySnapshot(characterId, new("Character"), EntityLifecycleState.Active,
                [new("tag-z"), new("tag-a")], null, ownerId, regionId, [new("component-z"), new("component-a")], 2, null)).IsSuccess);

            var metadata = Metadata(reverseMetadataInsertion);

            var regions = new RegionFramework(entities);
            var regionSnapshot = new RegionFrameworkSnapshot(1, rootId,
                [new(rootId, new RegionCategory("WorldScope"), null, RegionSimulationState.Abstract, rootId, "root-boundary", metadata),
                 new(regionId, new RegionCategory("NeutralArea"), rootId, RegionSimulationState.Active, rootId, "area-boundary", metadata),
                 new(siblingId, new RegionCategory("NeutralArea"), rootId, RegionSimulationState.Abstract, rootId, null, metadata)],
                [new(regionId, siblingId, metadata)], [new(characterId, regionId)]);
            Assert.True(regions.Restore(regionSnapshot).IsSuccess);

            var references = new References(true);
            var characters = new CharacterRegistry(entities, references);
            Assert.True(characters.Register(new(characterId, new("neutral-fixture"), new("available"), new("established"))).IsSuccess);
            var npcs = new NpcFramework(entities, characters, regions, references);
            Assert.True(npcs.Register(new(characterId, references.Purpose, references.Schedule.Id,
                references.Schedule.Entries![1].StateId, 1, new WorldTimestamp(9), NpcSimulationTier.Abstract, 4)).IsSuccess);

            var calendar = CalendarModel.Create(new CalendarDefinition(new CalendarId("neutral-calendar"), 1, 10,
                [new("period", 10)], [])).Value!;
            var clock = new WorldClock(calendar, new WorldTimestamp(7));
            Assert.True(clock.SetScale(new TimeScale(3, 2)).IsSuccess);
            Assert.True(clock.Advance(new WorldDuration(1)).IsSuccess);
            Assert.True(clock.Scheduler.ScheduleAbsolute(new ScheduleId("pending"), new WorldTimestamp(12), clock.Timestamp,
                "fixture", metadata, new WorldDuration(5)).IsSuccess);
            Assert.True(clock.SimulationLayers.Register(new SimulationLayerId("abstract-layer"), new WorldDuration(3), clock.Timestamp).IsSuccess);
            Assert.True(clock.Pause(new PauseReason("fixture-pause")).IsSuccess);
            var relationships = new RelationshipFramework(entities, new FixedRelationshipIdGenerator());
            var relationship = relationships.Create(characterId, ownerId, new RelationshipKindId("known-contact"), new WorldTimestamp(3), "fixture:event").Value!;
            var dimensions = reverseMetadataInsertion ? new[] { "zeta", "alpha" } : new[] { "alpha", "zeta" };
            Assert.True(relationships.SetDimension(relationship.Id, new RelationshipDimensionId(dimensions[0]),
                dimensions[0] == "alpha" ? 25 : 50, new WorldTimestamp(4)).IsSuccess);
            Assert.True(relationships.SetDimension(relationship.Id, new RelationshipDimensionId(dimensions[1]),
                dimensions[1] == "alpha" ? 25 : 50, new WorldTimestamp(5)).IsSuccess);
            var information = new InformationFramework(entities, new FixedInformationIdGenerator());
            var proposition = information.Create(new InformationTypeId("fixture-state"), characterId, regionId, metadata,
                new WorldTimestamp(3), "fixture:observation").Value!;
            Assert.True(information.DeclareFact(proposition.Id, new WorldTimestamp(3), "fixture:truth").IsSuccess);
            Assert.True(information.SetAwareness(characterId, proposition.Id, EpistemicStance.Known, 900,
                new WorldTimestamp(4), ownerId, "fixture:awareness").IsSuccess);
            return new Fixture(new(entities, clock, regions, characters, npcs, relationships, information), new(calendar, references, references), characterId, regionId);
        }

        private static EntitySnapshot Entity(EntityId id, string category, EntityId? parent, EntityId? region,
            EntityLifecycleState lifecycle = EntityLifecycleState.Active) =>
            new(id, new EntityCategory(category), lifecycle, [], parent, null, region, [], 0, null);

        private static IReadOnlyDictionary<string, string> Metadata(bool reverse) => reverse
            ? new Dictionary<string, string> { ["zeta"] = "last", ["alpha"] = "first" }
            : new Dictionary<string, string> { ["alpha"] = "first", ["zeta"] = "last" };
    }

    private sealed class References(bool valid) : ICharacterReferenceValidator, INpcReferenceProvider
    {
        public NpcPurposeId Purpose { get; } = new("neutral-participant");
        public NpcScheduleDefinition Schedule { get; } = new(new("neutral-cycle"),
            [new(new("state-a"), new(2)), new(new("state-b"), new(3))]);
        public bool IsKnownStatus(CharacterStatusId statusId) => valid && statusId == new CharacterStatusId("available");
        public bool IsKnownLifeStage(LifeStageId lifeStageId) => valid && lifeStageId == new LifeStageId("established");
        public bool IsKnownPurpose(NpcPurposeId purposeId) => valid && purposeId == Purpose;
        public NpcScheduleDefinition? FindSchedule(NpcScheduleId scheduleId) => valid && scheduleId == Schedule.Id ? Schedule : null;
    }

    private sealed class FixedRelationshipIdGenerator : IRelationshipIdGenerator
    {
        public RelationshipId Create() => new(Guid.Parse("00000000-0000-0000-0000-000000000007"));
    }

    private sealed class FixedInformationIdGenerator : IInformationIdGenerator
    {
        public InformationId CreateInformationId() => new(Guid.Parse("00000000-0000-0000-0000-000000000008"));
        public FactId CreateFactId() => new(Guid.Parse("00000000-0000-0000-0000-000000000009"));
    }

    private sealed class FailingWriteStorage(ISaveStorage inner, int failAtWrite) : ISaveStorage
    {
        public bool CommitCalled { get; private set; }
        public PersistenceResult<IReadOnlyDictionary<string, byte[]>> Read(string slotId) => inner.Read(slotId);
        public ISaveWriteTransaction BeginWrite(string slotId) => new Transaction(this, inner.BeginWrite(slotId), failAtWrite);

        private sealed class Transaction(FailingWriteStorage owner, ISaveWriteTransaction inner, int failAtWrite) : ISaveWriteTransaction
        {
            private int writeCount;
            public PersistenceResult Write(string partitionId, ReadOnlyMemory<byte> data) => ++writeCount == failAtWrite
                ? PersistenceResult.Failure(PersistenceErrorCodes.StorageFailure, "Injected staged-write failure.", partitionId)
                : inner.Write(partitionId, data);
            public PersistenceResult Commit()
            {
                owner.CommitCalled = true;
                return inner.Commit();
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
