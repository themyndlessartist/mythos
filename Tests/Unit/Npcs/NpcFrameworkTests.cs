using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Npcs;
using Mythos.Framework.Regions;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Npcs;

public sealed class NpcFrameworkTests
{
    [Fact]
    public void RegisterRequiresOneActiveCharacterWithRegionAndValidReferences()
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        Assert.Equal(NpcErrorCodes.DuplicateProfile, fixture.Npcs.Register(fixture.Profile()).Error?.Code);

        var missing = fixture.Profile() with { CharacterEntityId = new EntityId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")) };
        Assert.Equal(NpcErrorCodes.InvalidLifecycle, Fixture.Create().Npcs.Register(missing).Error?.Code);
    }

    [Fact]
    public void RegisterRejectsMissingCharacterRegionPurposeScheduleAndMalformedState()
    {
        var fixture = Fixture.Create(assignRegion: false);
        Assert.Equal(NpcErrorCodes.InvalidReference, fixture.Npcs.Register(fixture.Profile()).Error?.Code);

        fixture = Fixture.Create();
        Assert.Equal(NpcErrorCodes.InvalidReference, fixture.Npcs.Register(fixture.Profile() with { PurposeId = new NpcPurposeId("missing") }).Error?.Code);
        Assert.Equal(NpcErrorCodes.InvalidSchedule, fixture.Npcs.Register(fixture.Profile() with { ScheduleId = new NpcScheduleId("missing") }).Error?.Code);
        Assert.Equal(NpcErrorCodes.InvalidState, fixture.Npcs.Register(fixture.Profile() with { CurrentScheduleEntryIndex = 1 }).Error?.Code);
        Assert.Equal(NpcErrorCodes.InvalidIdentifier, fixture.Npcs.Register(fixture.Profile() with { CurrentScheduleStateId = default }).Error?.Code);
        Assert.Equal(NpcErrorCodes.InvalidState, fixture.Npcs.Register(fixture.Profile() with { SimulationTier = (NpcSimulationTier)99 }).Error?.Code);
        Assert.Equal(NpcErrorCodes.InvalidState, fixture.Npcs.Register(fixture.Profile() with { CompletedTransitions = -1 }).Error?.Code);
    }

    [Theory]
    [InlineData(NpcSimulationTier.Active)]
    [InlineData(NpcSimulationTier.Abstract)]
    public void ActiveAndAbstractUpdatesAdvanceTheSameDeterministicSchedule(NpcSimulationTier tier)
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile(tier)).IsSuccess);

        var result = fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(25), 10);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ProcessedTransitions);
        Assert.False(result.Value.CatchUpLimitReached);
        Assert.Equal(new NpcScheduleStateId("state-a"), result.Value.Profile.CurrentScheduleStateId);
        Assert.Equal(30, result.Value.Profile.NextDueAt.Value);
        Assert.Equal(2, result.Value.Profile.CompletedTransitions);
        Assert.Equal(tier, result.Value.Profile.SimulationTier);
    }

    [Fact]
    public void CatchUpIsBoundedDeterministicAndResumable()
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);

        var first = fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(55), 2).Value!;
        var second = fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(55), 2).Value!;
        var third = fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(55), 2).Value!;

        Assert.True(first.CatchUpLimitReached);
        Assert.True(second.CatchUpLimitReached);
        Assert.False(third.CatchUpLimitReached);
        Assert.Equal([30L, 50L, 60L], [first.Profile.NextDueAt.Value, second.Profile.NextDueAt.Value, third.Profile.NextDueAt.Value]);
        Assert.Equal(5, third.Profile.CompletedTransitions);
    }

    [Fact]
    public void UpdateDoesNotOwnOrAdvanceWorldClock()
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        var calendar = CalendarModel.Create(new CalendarDefinition(new CalendarId("fixture"), 1, 1,
            [new CalendarPeriodDefinition("period", 1)], [])).Value!;
        var clock = new WorldClock(calendar);

        Assert.True(fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(20), 10).IsSuccess);

        Assert.Equal(0, clock.Timestamp.Value);
    }

    [Fact]
    public void ValidationDetectsEntityCharacterAndRegionDrift()
    {
        var entityDrift = Fixture.Create();
        Assert.True(entityDrift.Npcs.Register(entityDrift.Profile()).IsSuccess);
        Assert.True(entityDrift.Entities.Retire(entityDrift.CharacterId, 1).IsSuccess);
        Assert.Equal(NpcErrorCodes.InvalidReference, entityDrift.Npcs.ValidateReferences().Error?.Code);

        var regionDrift = Fixture.Create();
        Assert.True(regionDrift.Npcs.Register(regionDrift.Profile()).IsSuccess);
        Assert.True(regionDrift.Entities.AssignRegion(regionDrift.CharacterId, null).IsSuccess);
        Assert.Equal(NpcErrorCodes.InvalidReference, regionDrift.Npcs.ValidateReferences().Error?.Code);

        var characterDrift = Fixture.Create();
        Assert.True(characterDrift.Npcs.Register(characterDrift.Profile()).IsSuccess);
        Assert.True(characterDrift.Entities.ChangeLifecycle(characterDrift.CharacterId, EntityLifecycleState.Inactive, 1).IsSuccess);
        Assert.Equal(NpcErrorCodes.InvalidReference, characterDrift.Npcs.ValidateReferences().Error?.Code);
    }

    [Fact]
    public void OperationalValidationIgnoresUnrelatedCharacterAndRegionDrift()
    {
        var fixture = Fixture.Create();
        fixture.AddUnrelatedDrift();

        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        Assert.True(fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(25), 10).IsSuccess);
        Assert.True(fixture.Npcs.SetSimulationTier(fixture.CharacterId, NpcSimulationTier.Abstract).IsSuccess);
        Assert.Equal("valid", fixture.Npcs.Inspect(fixture.CharacterId).Value!.ReferenceStatus);
        Assert.Equal(NpcErrorCodes.InvalidReference, fixture.Npcs.ValidateReferences().Error?.Code);
    }

    [Theory]
    [InlineData(EntityLifecycleState.Retired)]
    [InlineData(EntityLifecycleState.Destroyed)]
    public void UpdateRejectsTerminalAssignedRegionAtomically(EntityLifecycleState terminalState)
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        var before = fixture.Npcs.Find(fixture.CharacterId).Value!;
        Assert.True(fixture.Entities.ChangeLifecycle(fixture.AssignedRegionId, terminalState, 1).IsSuccess);

        var result = fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(25), 10);

        Assert.Equal(NpcErrorCodes.InvalidReference, result.Error?.Code);
        Assert.Equal(before, fixture.Npcs.Find(fixture.CharacterId).Value);
    }

    [Fact]
    public void UpdateRejectsInvalidatedCharacterDefinitionsAtomically()
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        var before = fixture.Npcs.Find(fixture.CharacterId).Value!;
        fixture.InvalidateCharacterDefinitions();

        var result = fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(25), 10);

        Assert.Equal(NpcErrorCodes.InvalidReference, result.Error?.Code);
        Assert.Equal(before, fixture.Npcs.Find(fixture.CharacterId).Value);
    }

    [Fact]
    public void PurposeScheduleAndReturnedScheduleIdentityDriftSuspendUpdates()
    {
        var purpose = Fixture.Create();
        Assert.True(purpose.Npcs.Register(purpose.Profile()).IsSuccess);
        purpose.InvalidatePurpose();
        Assert.Equal(NpcErrorCodes.InvalidReference, purpose.Npcs.Update(purpose.CharacterId, new WorldTimestamp(25), 10).Error?.Code);

        var schedule = Fixture.Create();
        Assert.True(schedule.Npcs.Register(schedule.Profile()).IsSuccess);
        schedule.RemoveSchedule();
        Assert.Equal(NpcErrorCodes.InvalidSchedule, schedule.Npcs.Update(schedule.CharacterId, new WorldTimestamp(25), 10).Error?.Code);

        var mismatch = Fixture.Create();
        Assert.True(mismatch.Npcs.Register(mismatch.Profile()).IsSuccess);
        mismatch.ReturnMismatchedScheduleId();
        Assert.Equal(NpcErrorCodes.InvalidSchedule, mismatch.Npcs.Update(mismatch.CharacterId, new WorldTimestamp(25), 10).Error?.Code);
        Assert.Equal(0, mismatch.Npcs.Find(mismatch.CharacterId).Value!.CompletedTransitions);
    }

    [Fact]
    public void TierMutationValidatesDriftAndRemainsAtomic()
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        fixture.InvalidateCharacterDefinitions();

        var result = fixture.Npcs.SetSimulationTier(fixture.CharacterId, NpcSimulationTier.Abstract);

        Assert.Equal(NpcErrorCodes.InvalidReference, result.Error?.Code);
        Assert.Equal(NpcSimulationTier.Active, fixture.Npcs.Find(fixture.CharacterId).Value!.SimulationTier);
    }

    [Fact]
    public void SnapshotIsDefensiveVersionedAndRoundTrips()
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        Assert.True(fixture.Npcs.Update(fixture.CharacterId, new WorldTimestamp(25), 10).IsSuccess);
        var list = new List<NpcProfileSnapshot> { fixture.Npcs.Find(fixture.CharacterId).Value! };
        var defensive = new NpcFrameworkSnapshot(1, list);
        list.Clear();
        Assert.Single(defensive.Profiles!);

        var snapshot = fixture.Npcs.ExportSnapshot();
        var restored = fixture.NewNpcFramework();
        Assert.True(restored.RestoreSnapshot(snapshot).IsSuccess);
        Assert.Equal(snapshot.Profiles, restored.ExportSnapshot().Profiles);
    }

    [Fact]
    public void RestoreRejectsMalformedNullDuplicateAndIncompatibleSnapshotsAtomically()
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        var original = fixture.Npcs.ExportSnapshot();

        Assert.Equal(NpcErrorCodes.InvalidSnapshot, fixture.Npcs.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(NpcErrorCodes.InvalidSnapshot, fixture.Npcs.RestoreSnapshot(new NpcFrameworkSnapshot(1, null)).Error?.Code);
        Assert.Equal(NpcErrorCodes.InvalidSnapshot, fixture.Npcs.RestoreSnapshot(new NpcFrameworkSnapshot(1, [null!])).Error?.Code);
        Assert.Equal(NpcErrorCodes.UnsupportedSnapshotVersion, fixture.Npcs.RestoreSnapshot(new NpcFrameworkSnapshot(2, [])).Error?.Code);
        Assert.Equal(NpcErrorCodes.DuplicateProfile, fixture.Npcs.RestoreSnapshot(new NpcFrameworkSnapshot(1, [fixture.Profile(), fixture.Profile()])).Error?.Code);
        Assert.Equal(NpcErrorCodes.InvalidState, fixture.Npcs.RestoreSnapshot(new NpcFrameworkSnapshot(1, [fixture.Profile() with { CurrentScheduleEntryIndex = 99 }])).Error?.Code);
        Assert.Equal(original.Profiles, fixture.Npcs.ExportSnapshot().Profiles);
    }

    [Fact]
    public void DiagnosticsExposeFixtureStateAndReferenceFailure()
    {
        var fixture = Fixture.Create();
        Assert.True(fixture.Npcs.Register(fixture.Profile()).IsSuccess);
        var valid = fixture.Npcs.Inspect(fixture.CharacterId).Value!;
        Assert.Equal("valid", valid.ReferenceStatus);
        Assert.Equal(new WorldTimestamp(10), valid.NextDueAt);

        Assert.True(fixture.Entities.AssignRegion(fixture.CharacterId, null).IsSuccess);
        Assert.StartsWith(NpcErrorCodes.InvalidReference, fixture.Npcs.Inspect(fixture.CharacterId).Value!.ReferenceStatus);
    }

    [Fact]
    public void DiagnosticsUseCharacterAndRegionValidationBoundariesAfterDrift()
    {
        var character = Fixture.Create();
        Assert.True(character.Npcs.Register(character.Profile()).IsSuccess);
        character.InvalidateCharacterDefinitions();
        Assert.StartsWith(NpcErrorCodes.InvalidReference, character.Npcs.Inspect(character.CharacterId).Value!.ReferenceStatus);

        var region = Fixture.Create();
        Assert.True(region.Npcs.Register(region.Profile()).IsSuccess);
        Assert.True(region.Entities.Destroy(region.AssignedRegionId, 1).IsSuccess);
        Assert.StartsWith(NpcErrorCodes.InvalidReference, region.Npcs.Inspect(region.CharacterId).Value!.ReferenceStatus);
    }

    private sealed class Fixture
    {
        private static readonly CharacterStatusId Status = new("available");
        private static readonly LifeStageId Stage = new("established");
        private readonly References references;
        private readonly CharacterReferences characterReferences;

        private Fixture(EntityRegistry entities, RegionFramework regions, CharacterRegistry characters, EntityId characterId,
            EntityId assignedRegionId, References references, CharacterReferences characterReferences)
        {
            Entities = entities; Regions = regions; Characters = characters; CharacterId = characterId;
            AssignedRegionId = assignedRegionId;
            this.references = references;
            this.characterReferences = characterReferences;
            Npcs = NewNpcFramework();
        }

        public EntityRegistry Entities { get; }
        public RegionFramework Regions { get; }
        public CharacterRegistry Characters { get; }
        public EntityId CharacterId { get; }
        public EntityId AssignedRegionId { get; }
        public NpcFramework Npcs { get; }

        public static Fixture Create(bool assignRegion = true)
        {
            var entities = new EntityRegistry();
            var regions = new RegionFramework(entities);
            var root = regions.CreateRoot(new RegionCategory("world-scope"), 0).Value!;
            var area = regions.CreateRegion(new RegionCategory("neutral-area"), root.Id, 0).Value!;
            var characterId = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
            if (assignRegion) Assert.True(regions.AssignEntity(characterId, area.Id).IsSuccess);
            var characterReferences = new CharacterReferences();
            var characters = new CharacterRegistry(entities, characterReferences);
            Assert.True(characters.Register(new CharacterProfileSnapshot(characterId, new CharacterIdentity("fixture"), Status, Stage)).IsSuccess);
            return new Fixture(entities, regions, characters, characterId, area.Id, new References(), characterReferences);
        }

        public NpcProfileSnapshot Profile(NpcSimulationTier tier = NpcSimulationTier.Active) => new(
            CharacterId, references.Purpose, references.Schedule.Id, references.Schedule.Entries![0].StateId, 0,
            new WorldTimestamp(10), tier, 0);

        public NpcFramework NewNpcFramework() => new(Entities, Characters, Regions, references);

        public void InvalidateCharacterDefinitions() => characterReferences.AreDefinitionsValid = false;
        public void InvalidatePurpose() => references.IsPurposeAvailable = false;
        public void RemoveSchedule() => references.IsScheduleAvailable = false;
        public void ReturnMismatchedScheduleId() => references.UseMismatchedScheduleId = true;

        public void AddUnrelatedDrift()
        {
            var unrelatedCharacter = Entities.Create(new EntityCategory("Character"), 0).Value!.Id;
            Assert.True(Characters.Register(new CharacterProfileSnapshot(unrelatedCharacter,
                new CharacterIdentity("unrelated"), Status, Stage)).IsSuccess);
            Assert.True(Entities.Retire(unrelatedCharacter, 1).IsSuccess);

            var rootId = Regions.RootRegionId!.Value;
            var unrelatedRegion = Regions.CreateRegion(new RegionCategory("unrelated-area"), rootId, 0).Value!;
            Assert.True(Entities.Retire(unrelatedRegion.Id, 1).IsSuccess);
        }

        private sealed class CharacterReferences : ICharacterReferenceValidator
        {
            public bool AreDefinitionsValid { get; set; } = true;
            public bool IsKnownStatus(CharacterStatusId statusId) => AreDefinitionsValid && statusId == Status;
            public bool IsKnownLifeStage(LifeStageId lifeStageId) => AreDefinitionsValid && lifeStageId == Stage;
        }

        private sealed class References : INpcReferenceProvider
        {
            public NpcPurposeId Purpose { get; } = new("neutral-participant");
            public NpcScheduleDefinition Schedule { get; } = new(new NpcScheduleId("neutral-cycle"),
                [new NpcScheduleEntry(new NpcScheduleStateId("state-a"), new WorldDuration(10)),
                 new NpcScheduleEntry(new NpcScheduleStateId("state-b"), new WorldDuration(10))]);
            public bool IsPurposeAvailable { get; set; } = true;
            public bool IsScheduleAvailable { get; set; } = true;
            public bool UseMismatchedScheduleId { get; set; }
            public bool IsKnownPurpose(NpcPurposeId purposeId) => IsPurposeAvailable && purposeId == Purpose;
            public NpcScheduleDefinition? FindSchedule(NpcScheduleId scheduleId)
            {
                if (!IsScheduleAvailable || scheduleId != Schedule.Id) return null;
                return UseMismatchedScheduleId
                    ? new NpcScheduleDefinition(new NpcScheduleId("different-cycle"), Schedule.Entries)
                    : Schedule;
            }
        }
    }
}
