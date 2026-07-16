using Mythos.Framework.Entities;

namespace Mythos.Framework.Characters;

/// <summary>
/// Minimal M-001 persistent projection of one Character profile.
/// </summary>
public sealed record CharacterProfileSnapshot(
    EntityId EntityId,
    CharacterIdentity Identity,
    CharacterStatusId StatusId,
    LifeStageId LifeStageId);

/// <summary>
/// Versioned M-001 Character-domain snapshot. This is not the final save schema.
/// </summary>
public sealed record CharacterRegistrySnapshot
{
    public const int CurrentVersion = 1;

    public CharacterRegistrySnapshot(int version, IReadOnlyList<CharacterProfileSnapshot>? profiles)
    {
        Version = version;
        Profiles = profiles is null ? null : Array.AsReadOnly(profiles.ToArray());
    }

    public int Version { get; }

    public IReadOnlyList<CharacterProfileSnapshot>? Profiles { get; }
}
