namespace Mythos.Framework.Characters;

public static class CharacterErrorCodes
{
    public const string DuplicateProfile = "character.duplicate_profile";
    public const string EntityNotFound = "character.entity_not_found";
    public const string WrongEntityCategory = "character.wrong_entity_category";
    public const string EntityNotActive = "character.entity_not_active";
    public const string InvalidIdentifier = "character.invalid_identifier";
    public const string BrokenReference = "character.broken_reference";
    public const string InvalidSnapshot = "character.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "character.unsupported_snapshot_version";
    public const string ProfileNotFound = "character.profile_not_found";
}

public sealed record CharacterError(string Code, string Message);

public readonly record struct CharacterResult<T>(T? Value, CharacterError? Error)
{
    public bool IsSuccess => Error is null;

    public static CharacterResult<T> Success(T value) => new(value, null);

    public static CharacterResult<T> Failure(string code, string message) =>
        new(default, new CharacterError(code, message));
}

public readonly record struct CharacterResult(CharacterError? Error)
{
    public bool IsSuccess => Error is null;

    public static CharacterResult Success() => new(null);

    public static CharacterResult Failure(string code, string message) =>
        new(new CharacterError(code, message));
}
