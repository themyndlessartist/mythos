using Mythos.Framework.Characters;
using Mythos.Framework.Entities;

namespace Mythos.Framework.UnitTests.Characters;

public sealed class CharacterRegistryTests
{
    private static readonly CharacterStatusId Available = new("available");
    private static readonly LifeStageId Established = new("established");

    [Fact]
    public void RegisterAndFindPreserveMinimalProfile()
    {
        var (characters, entities, entityId) = CreateRegistry();
        var expected = Profile(entityId);

        var registered = characters.Register(expected);
        var found = characters.Find(entityId);

        Assert.True(registered.IsSuccess);
        Assert.True(found.IsSuccess);
        Assert.Equal(expected, found.Value);
        Assert.Equal(1, characters.Count);
        Assert.True(entities.IsActive(entityId));
    }

    [Fact]
    public void RegisterRejectsDuplicateProfile()
    {
        var (characters, _, entityId) = CreateRegistry();
        Assert.True(characters.Register(Profile(entityId)).IsSuccess);

        var duplicate = characters.Register(Profile(entityId));

        Assert.Equal(CharacterErrorCodes.DuplicateProfile, duplicate.Error?.Code);
        Assert.Equal(1, characters.Count);
    }

    [Fact]
    public void RegisterRejectsMissingWrongCategoryAndNonActiveEntities()
    {
        var entities = new EntityRegistry();
        var validator = new TestReferenceValidator();
        var characters = new CharacterRegistry(entities, validator);
        var missingId = new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var wrongCategory = entities.Create(new EntityCategory("Item"), 0).Value!.Id;
        var inactive = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var retired = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var destroyed = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        Assert.True(entities.ChangeLifecycle(inactive, EntityLifecycleState.Inactive, 1).IsSuccess);
        Assert.True(entities.Retire(retired, 1).IsSuccess);
        Assert.True(entities.Destroy(destroyed, 1).IsSuccess);

        Assert.Equal(CharacterErrorCodes.EntityNotFound, characters.Register(Profile(missingId)).Error?.Code);
        Assert.Equal(CharacterErrorCodes.WrongEntityCategory, characters.Register(Profile(wrongCategory)).Error?.Code);
        Assert.Equal(CharacterErrorCodes.EntityNotActive, characters.Register(Profile(inactive)).Error?.Code);
        Assert.Equal(CharacterErrorCodes.EntityNotActive, characters.Register(Profile(retired)).Error?.Code);
        Assert.Equal(CharacterErrorCodes.EntityNotActive, characters.Register(Profile(destroyed)).Error?.Code);
        Assert.Equal(0, characters.Count);
    }

    [Fact]
    public void RegisterRejectsMalformedIdentifiersAndBrokenReferences()
    {
        var (characters, entities, entityId) = CreateRegistry();
        var secondId = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var thirdId = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var fourthId = entities.Create(new EntityCategory("Character"), 0).Value!.Id;

        Assert.Equal(
            CharacterErrorCodes.InvalidIdentifier,
            characters.Register(new CharacterProfileSnapshot(entityId, default, Available, Established)).Error?.Code);
        Assert.Equal(
            CharacterErrorCodes.InvalidIdentifier,
            characters.Register(new CharacterProfileSnapshot(secondId, new CharacterIdentity("fixture-2"), default, Established)).Error?.Code);
        Assert.Equal(
            CharacterErrorCodes.BrokenReference,
            characters.Register(new CharacterProfileSnapshot(thirdId, new CharacterIdentity("fixture-3"), new CharacterStatusId("missing"), Established)).Error?.Code);
        Assert.Equal(
            CharacterErrorCodes.BrokenReference,
            characters.Register(new CharacterProfileSnapshot(fourthId, new CharacterIdentity("fixture-4"), Available, new LifeStageId("missing"))).Error?.Code);
        Assert.Equal(0, characters.Count);
    }

    [Fact]
    public void QueriesAndExportAreDeterministicByEntityId()
    {
        var entities = new EntityRegistry();
        var characters = new CharacterRegistry(entities, new TestReferenceValidator());
        var ids = new[]
        {
            new EntityId(Guid.Parse("30000000-0000-0000-0000-000000000000")),
            new EntityId(Guid.Parse("10000000-0000-0000-0000-000000000000")),
            new EntityId(Guid.Parse("20000000-0000-0000-0000-000000000000")),
        };
        foreach (var id in ids)
        {
            Assert.True(entities.Register(Entity(id)).IsSuccess);
            Assert.True(characters.Register(Profile(id)).IsSuccess);
        }

        var expected = ids.OrderBy(id => id.Value).ToArray();

        Assert.Equal(expected, characters.QueryAll().Select(profile => profile.EntityId));
        Assert.Equal(expected, characters.QueryByStatus(Available).Select(profile => profile.EntityId));
        Assert.Equal(expected, characters.QueryByLifeStage(Established).Select(profile => profile.EntityId));
        Assert.Equal(expected, characters.ExportSnapshot().Profiles!.Select(profile => profile.EntityId));
    }

    [Fact]
    public void SnapshotDefensivelyCopiesCallerCollection()
    {
        var (_, _, entityId) = CreateRegistry();
        var source = new List<CharacterProfileSnapshot> { Profile(entityId) };

        var snapshot = new CharacterRegistrySnapshot(CharacterRegistrySnapshot.CurrentVersion, source);
        source.Clear();

        Assert.Single(snapshot.Profiles!);
        Assert.IsAssignableFrom<IReadOnlyList<CharacterProfileSnapshot>>(snapshot.Profiles);
    }

    [Theory]
    [InlineData(0, CharacterErrorCodes.UnsupportedSnapshotVersion)]
    [InlineData(2, CharacterErrorCodes.UnsupportedSnapshotVersion)]
    public void RestoreRejectsUnsupportedSnapshotVersions(int version, string expectedCode)
    {
        var (characters, _, _) = CreateRegistry();

        var result = characters.RestoreSnapshot(new CharacterRegistrySnapshot(version, []));

        Assert.Equal(expectedCode, result.Error?.Code);
    }

    [Fact]
    public void RestoreRejectsNullSnapshotAndNullProfiles()
    {
        var (characters, _, _) = CreateRegistry();

        Assert.Equal(CharacterErrorCodes.InvalidSnapshot, characters.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(
            CharacterErrorCodes.InvalidSnapshot,
            characters.RestoreSnapshot(new CharacterRegistrySnapshot(CharacterRegistrySnapshot.CurrentVersion, null)).Error?.Code);
    }

    [Fact]
    public void RestoreRejectsDuplicateAndMalformedProfiles()
    {
        var (characters, _, entityId) = CreateRegistry();
        var duplicate = new CharacterRegistrySnapshot(1, [Profile(entityId), Profile(entityId)]);
        var malformed = new CharacterRegistrySnapshot(
            1,
            [new CharacterProfileSnapshot(entityId, new CharacterIdentity("fixture"), default, Established)]);

        Assert.Equal(CharacterErrorCodes.DuplicateProfile, characters.RestoreSnapshot(duplicate).Error?.Code);
        Assert.Equal(CharacterErrorCodes.InvalidIdentifier, characters.RestoreSnapshot(malformed).Error?.Code);
    }

    [Fact]
    public void RestoreRejectsNullProfileEntry()
    {
        var (characters, _, _) = CreateRegistry();
        var snapshot = new CharacterRegistrySnapshot(1, [null!]);

        var result = characters.RestoreSnapshot(snapshot);

        Assert.Equal(CharacterErrorCodes.InvalidSnapshot, result.Error?.Code);
        Assert.Equal(0, characters.Count);
    }

    [Fact]
    public void FailedRestoreIsAtomic()
    {
        var (characters, entities, originalId) = CreateRegistry();
        Assert.True(characters.Register(Profile(originalId)).IsSuccess);
        var validNewId = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var missingId = new EntityId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var invalid = new CharacterRegistrySnapshot(1, [Profile(validNewId), Profile(missingId)]);

        var result = characters.RestoreSnapshot(invalid);

        Assert.Equal(CharacterErrorCodes.EntityNotFound, result.Error?.Code);
        Assert.Equal([originalId], characters.QueryAll().Select(profile => profile.EntityId));
    }

    [Fact]
    public void SnapshotRoundTripRestoresEquivalentDeterministicState()
    {
        var entities = new EntityRegistry();
        var validator = new TestReferenceValidator();
        var source = new CharacterRegistry(entities, validator);
        var first = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var second = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        Assert.True(source.Register(Profile(first)).IsSuccess);
        Assert.True(source.Register(Profile(second)).IsSuccess);
        var snapshot = source.ExportSnapshot();
        var restored = new CharacterRegistry(entities, validator);

        var result = restored.RestoreSnapshot(snapshot);

        Assert.True(result.IsSuccess);
        Assert.Equal(snapshot.Version, restored.ExportSnapshot().Version);
        Assert.Equal(snapshot.Profiles, restored.ExportSnapshot().Profiles);
        Assert.Equal(source.QueryAll(), restored.QueryAll());
    }

    [Fact]
    public void ValidateReferencesDetectsEntityThatBecameTerminal()
    {
        var (characters, entities, entityId) = CreateRegistry();
        Assert.True(characters.Register(Profile(entityId)).IsSuccess);
        Assert.True(entities.Retire(entityId, 1).IsSuccess);

        var result = characters.ValidateReferences();

        Assert.Equal(CharacterErrorCodes.EntityNotActive, result.Error?.Code);
    }

    [Fact]
    public void IdentifierConstructorsRejectBlankValuesAndNormalizeWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new CharacterIdentity(" "));
        Assert.Throws<ArgumentException>(() => new CharacterStatusId(" "));
        Assert.Throws<ArgumentException>(() => new LifeStageId(" "));
        Assert.Equal("fixture", new CharacterIdentity(" fixture ").Value);
        Assert.Equal("available", new CharacterStatusId(" available ").Value);
        Assert.Equal("established", new LifeStageId(" established ").Value);
    }

    private static (CharacterRegistry Characters, EntityRegistry Entities, EntityId EntityId) CreateRegistry()
    {
        var entities = new EntityRegistry();
        var entityId = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        return (new CharacterRegistry(entities, new TestReferenceValidator()), entities, entityId);
    }

    private static CharacterProfileSnapshot Profile(EntityId entityId) =>
        new(entityId, new CharacterIdentity($"fixture-{entityId}"), Available, Established);

    private static EntitySnapshot Entity(EntityId entityId) =>
        new(entityId, new EntityCategory("Character"), EntityLifecycleState.Active, [], null, null, null, [], 0, null);

    private sealed class TestReferenceValidator : ICharacterReferenceValidator
    {
        public bool IsKnownStatus(CharacterStatusId statusId) => statusId == Available;

        public bool IsKnownLifeStage(LifeStageId lifeStageId) => lifeStageId == Established;
    }
}
