namespace Mythos.Framework.Properties;

public static class PropertyErrorCodes
{
    public const string NotFound = "property.not_found";
    public const string DuplicateProfile = "property.duplicate_profile";
    public const string InvalidIdentifier = "property.invalid_identifier";
    public const string InvalidReference = "property.invalid_reference";
    public const string InvalidLifecycle = "property.invalid_lifecycle";
    public const string InvalidTimestamp = "property.invalid_timestamp";
    public const string InvalidSnapshot = "property.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "property.unsupported_snapshot_version";
    public const string EventPublicationFailed = "property.event_publication_failed";
    public const string OwnershipRejected = "property.ownership_rejected";
}

public sealed record PropertyError(string Code, string Message);

public readonly record struct PropertyResult(PropertyError? Error)
{
    public bool IsSuccess => Error is null;
    public static PropertyResult Success() => new(null);
    public static PropertyResult Failure(string code, string message) => new(new(code, message));
}

public readonly record struct PropertyResult<T>(T? Value, PropertyError? Error)
{
    public bool IsSuccess => Error is null;
    public static PropertyResult<T> Success(T value) => new(value, null);
    public static PropertyResult<T> Failure(string code, string message) => new(default, new(code, message));
}
