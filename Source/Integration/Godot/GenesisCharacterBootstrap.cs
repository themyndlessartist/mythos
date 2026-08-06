using Mythos.Content;
using Mythos.Framework.Characters;
using Mythos.Framework.Entities;

namespace Mythos.GodotIntegration;

/// <summary>Title adapter that maps accepted Genesis authoring data into the shared runtime foundations.</summary>
internal static class GenesisCharacterBootstrap
{
    private static readonly CharacterStatusId TestStatus = new("mythos-test.available");
    private static readonly LifeStageId TestLifeStage = new("mythos-test.unspecified-life-stage");

    internal static GenesisCharacterBootstrapResult Create(
        ImportedCharacterAuthoringRecord record,
        EntityRegistry entities,
        long worldTimestamp)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(entities);

        var entity = entities.Create(new EntityCategory("Character"), worldTimestamp);
        if (!entity.IsSuccess)
        {
            return GenesisCharacterBootstrapResult.Failure(entity.Error!.Code, entity.Error.Message);
        }

        var characters = new CharacterRegistry(entities, TestCharacterReferenceValidator.Instance);
        var profile = characters.Register(new CharacterProfileSnapshot(
            entity.Value!.Id,
            new CharacterIdentity(record.DisplayName),
            TestStatus,
            TestLifeStage));
        if (!profile.IsSuccess)
        {
            return GenesisCharacterBootstrapResult.Failure(profile.Error!.Code, profile.Error.Message);
        }

        return GenesisCharacterBootstrapResult.Success(entity.Value, profile.Value!, characters);
    }

    private sealed class TestCharacterReferenceValidator : ICharacterReferenceValidator
    {
        internal static readonly TestCharacterReferenceValidator Instance = new();

        public bool IsKnownStatus(CharacterStatusId statusId) => statusId == TestStatus;

        public bool IsKnownLifeStage(LifeStageId lifeStageId) => lifeStageId == TestLifeStage;
    }
}

internal sealed record GenesisCharacterBootstrapResult(
    EntitySnapshot? Entity,
    CharacterProfileSnapshot? Character,
    CharacterRegistry? Characters,
    string? ErrorCode,
    string? ErrorMessage)
{
    internal bool IsSuccess => ErrorCode is null;

    internal static GenesisCharacterBootstrapResult Success(
        EntitySnapshot entity,
        CharacterProfileSnapshot character,
        CharacterRegistry characters) => new(entity, character, characters, null, null);

    internal static GenesisCharacterBootstrapResult Failure(string code, string message) =>
        new(null, null, null, code, message);
}
