using System.Text.Json;
using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Npcs;
using Mythos.Framework.Persistence;
using Mythos.Framework.Regions;
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
        Assert.Equal(7, loaded.Value.Clock.Timestamp.Value);
        Assert.Single(loaded.Value.Clock.Scheduler.ExportSnapshots());
        Assert.Equal(fixture.RegionId, loaded.Value.Entities.Find(fixture.CharacterId).Value!.RegionId);
        Assert.Equal(fixture.World.Characters.ExportSnapshot().Profiles!, loaded.Value.Characters.ExportSnapshot().Profiles!);
        Assert.Equal(fixture.World.Npcs.ExportSnapshot().Profiles!, loaded.Value.Npcs.ExportSnapshot().Profiles!);
        Assert.True(loaded.Value.Regions.ValidateReferences().IsSuccess);
        Assert.True(loaded.Value.Npcs.ValidateReferences().IsSuccess);
    }

    [Fact]
    public void SameWorldProducesDeterministicPartitionBytes()
    {
        var fixture = Fixture.Create();
        var storage = new InMemorySaveStorage();
        var persistence = new WorldPersistence(storage);
        Assert.True(persistence.Save("a", "neutral-world", fixture.World).IsSuccess);
        Assert.True(persistence.Save("b", "neutral-world", fixture.World).IsSuccess);
        var first = storage.Read("a").Value!;
        var second = storage.Read("b").Value!;
        Assert.Equal(first.Keys.Order(), second.Keys.Order());
        foreach (var key in first.Keys) Assert.Equal(first[key], second[key]);
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

    private static void RewriteManifest(Dictionary<string, byte[]> data)
    {
        var manifest = JsonSerializer.Deserialize<SaveManifest>(data["manifest"], Options)!;
        var descriptors = manifest.Partitions!.Select(item => item.Id == "characters"
            ? item with { Sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data["characters"])) }
            : item).ToArray();
        data["manifest"] = JsonSerializer.SerializeToUtf8Bytes(manifest with { Partitions = descriptors }, Options);
    }

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private sealed record Fixture(PersistentWorldState World, PersistenceLoadContext Context, EntityId CharacterId, EntityId RegionId)
    {
        public static Fixture Create()
        {
            var entities = new EntityRegistry();
            var rootId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            var regionId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
            var characterId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000003"));
            Assert.True(entities.Register(Entity(rootId, "Region", null, null)).IsSuccess);
            Assert.True(entities.Register(Entity(regionId, "Region", rootId, null)).IsSuccess);
            Assert.True(entities.Register(Entity(characterId, "Character", null, regionId)).IsSuccess);

            var regions = new RegionFramework(entities);
            var regionSnapshot = new RegionFrameworkSnapshot(1, rootId,
                [new(rootId, new RegionCategory("WorldScope"), null, RegionSimulationState.Abstract, rootId, null, new Dictionary<string, string>()),
                 new(regionId, new RegionCategory("NeutralArea"), rootId, RegionSimulationState.Active, rootId, null, new Dictionary<string, string>())],
                [], [new(characterId, regionId)]);
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
            Assert.True(clock.Scheduler.ScheduleAbsolute(new ScheduleId("pending"), new WorldTimestamp(12), clock.Timestamp, "fixture").IsSuccess);
            return new Fixture(new(entities, clock, regions, characters, npcs), new(calendar, references, references), characterId, regionId);
        }

        private static EntitySnapshot Entity(EntityId id, string category, EntityId? parent, EntityId? region) =>
            new(id, new EntityCategory(category), EntityLifecycleState.Active, [], parent, null, region, [], 0, null);
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
}
