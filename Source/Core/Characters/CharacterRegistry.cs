using Mythos.Framework.Entities;

namespace Mythos.Framework.Characters;

/// <summary>
/// Owns the minimal engine-independent Character profiles required by M-001.
/// </summary>
public sealed class CharacterRegistry
{
    private static readonly EntityCategory CharacterCategory = new("Character");
    private readonly EntityRegistry entityRegistry;
    private readonly ICharacterReferenceValidator referenceValidator;
    private Dictionary<EntityId, CharacterProfileSnapshot> profiles = [];

    public CharacterRegistry(EntityRegistry entityRegistry, ICharacterReferenceValidator referenceValidator)
    {
        this.entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        this.referenceValidator = referenceValidator ?? throw new ArgumentNullException(nameof(referenceValidator));
    }

    public int Count => profiles.Count;

    public CharacterResult<CharacterProfileSnapshot> Register(CharacterProfileSnapshot profile)
    {
        if (profiles.ContainsKey(profile.EntityId))
        {
            return CharacterResult<CharacterProfileSnapshot>.Failure(
                CharacterErrorCodes.DuplicateProfile,
                $"Character profile for entity '{profile.EntityId}' is already registered.");
        }

        var validation = ValidateProfile(profile);
        if (!validation.IsSuccess)
        {
            return CharacterResult<CharacterProfileSnapshot>.Failure(validation.Error!.Code, validation.Error.Message);
        }

        profiles.Add(profile.EntityId, profile);
        return CharacterResult<CharacterProfileSnapshot>.Success(profile);
    }

    public CharacterResult<CharacterProfileSnapshot> Find(EntityId entityId) =>
        profiles.TryGetValue(entityId, out var profile)
            ? CharacterResult<CharacterProfileSnapshot>.Success(profile)
            : CharacterResult<CharacterProfileSnapshot>.Failure(
                CharacterErrorCodes.ProfileNotFound,
                $"Character profile for entity '{entityId}' was not found.");

    public IReadOnlyList<CharacterProfileSnapshot> QueryAll() => OrderedProfiles(profiles.Values);

    public IReadOnlyList<CharacterProfileSnapshot> QueryByStatus(CharacterStatusId statusId) =>
        IsInitialized(statusId.Value)
            ? OrderedProfiles(profiles.Values.Where(profile => profile.StatusId == statusId))
            : [];

    public IReadOnlyList<CharacterProfileSnapshot> QueryByLifeStage(LifeStageId lifeStageId) =>
        IsInitialized(lifeStageId.Value)
            ? OrderedProfiles(profiles.Values.Where(profile => profile.LifeStageId == lifeStageId))
            : [];

    public CharacterRegistrySnapshot ExportSnapshot() =>
        new(CharacterRegistrySnapshot.CurrentVersion, QueryAll());

    public CharacterResult RestoreSnapshot(CharacterRegistrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return CharacterResult.Failure(CharacterErrorCodes.InvalidSnapshot, "Character snapshot cannot be null.");
        }

        if (snapshot.Version != CharacterRegistrySnapshot.CurrentVersion)
        {
            return CharacterResult.Failure(
                CharacterErrorCodes.UnsupportedSnapshotVersion,
                $"Character snapshot version '{snapshot.Version}' is not supported.");
        }

        if (snapshot.Profiles is null)
        {
            return CharacterResult.Failure(CharacterErrorCodes.InvalidSnapshot, "Character snapshot profiles cannot be null.");
        }

        var restored = new Dictionary<EntityId, CharacterProfileSnapshot>();
        foreach (var profile in snapshot.Profiles)
        {
            if (profile is null)
            {
                return CharacterResult.Failure(
                    CharacterErrorCodes.InvalidSnapshot,
                    "Character snapshot profiles cannot contain null entries.");
            }

            if (!restored.TryAdd(profile.EntityId, profile))
            {
                return CharacterResult.Failure(
                    CharacterErrorCodes.DuplicateProfile,
                    $"Character snapshot contains duplicate profile '{profile.EntityId}'.");
            }

            var validation = ValidateProfile(profile);
            if (!validation.IsSuccess)
            {
                return validation;
            }
        }

        profiles = restored;
        return CharacterResult.Success();
    }

    public CharacterResult ValidateReferences()
    {
        foreach (var profile in profiles.Values)
        {
            var validation = ValidateProfile(profile);
            if (!validation.IsSuccess)
            {
                return validation;
            }
        }

        return CharacterResult.Success();
    }

    private CharacterResult ValidateProfile(CharacterProfileSnapshot profile)
    {
        if (profile.EntityId.Value == Guid.Empty)
        {
            return CharacterResult.Failure(CharacterErrorCodes.InvalidIdentifier, "Character Entity ID cannot be empty.");
        }

        if (!IsInitialized(profile.Identity.Value) ||
            !IsInitialized(profile.StatusId.Value) ||
            !IsInitialized(profile.LifeStageId.Value))
        {
            return CharacterResult.Failure(
                CharacterErrorCodes.InvalidIdentifier,
                "Character identity, status, and life-stage identifiers must be initialized.");
        }

        var entity = entityRegistry.Find(profile.EntityId);
        if (!entity.IsSuccess)
        {
            return CharacterResult.Failure(
                CharacterErrorCodes.EntityNotFound,
                $"Character entity '{profile.EntityId}' was not found.");
        }

        if (entity.Value!.Category != CharacterCategory)
        {
            return CharacterResult.Failure(
                CharacterErrorCodes.WrongEntityCategory,
                $"Entity '{profile.EntityId}' is not categorized as Character.");
        }

        if (entity.Value.LifecycleState != EntityLifecycleState.Active)
        {
            return CharacterResult.Failure(
                CharacterErrorCodes.EntityNotActive,
                $"Character entity '{profile.EntityId}' must be active.");
        }

        if (!referenceValidator.IsKnownStatus(profile.StatusId))
        {
            return CharacterResult.Failure(
                CharacterErrorCodes.BrokenReference,
                $"Character status reference '{profile.StatusId}' was not found.");
        }

        if (!referenceValidator.IsKnownLifeStage(profile.LifeStageId))
        {
            return CharacterResult.Failure(
                CharacterErrorCodes.BrokenReference,
                $"Character life-stage reference '{profile.LifeStageId}' was not found.");
        }

        return CharacterResult.Success();
    }

    private static IReadOnlyList<CharacterProfileSnapshot> OrderedProfiles(IEnumerable<CharacterProfileSnapshot> source) =>
        source.OrderBy(profile => profile.EntityId.Value).ToArray();

    private static bool IsInitialized(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
}
