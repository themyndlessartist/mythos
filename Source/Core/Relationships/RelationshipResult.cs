namespace Mythos.Framework.Relationships;

public static class RelationshipErrorCodes
{
    public const string NotFound = "relationship.not_found";
    public const string DuplicateId = "relationship.duplicate_id";
    public const string DuplicateActiveTuple = "relationship.duplicate_active_tuple";
    public const string InvalidIdentifier = "relationship.invalid_identifier";
    public const string InvalidReference = "relationship.invalid_reference";
    public const string InvalidLifecycle = "relationship.invalid_lifecycle";
    public const string InvalidTimestamp = "relationship.invalid_timestamp";
    public const string InvalidDimension = "relationship.invalid_dimension";
    public const string InvalidSnapshot = "relationship.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "relationship.unsupported_snapshot_version";
    public const string EventPublicationFailed = "relationship.event_publication_failed";
}

public sealed record RelationshipError(string Code, string Message);

public readonly record struct RelationshipResult(RelationshipError? Error)
{
    public bool IsSuccess => Error is null;
    public static RelationshipResult Success() => new(null);
    public static RelationshipResult Failure(string code, string message) => new(new(code, message));
}

public readonly record struct RelationshipResult<T>(T? Value, RelationshipError? Error)
{
    public bool IsSuccess => Error is null;
    public static RelationshipResult<T> Success(T value) => new(value, null);
    public static RelationshipResult<T> Failure(string code, string message) => new(default, new(code, message));
}

