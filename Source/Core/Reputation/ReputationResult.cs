namespace Mythos.Framework.Reputation;

public static class ReputationErrorCodes
{
    public const string NotFound = "reputation.not_found";
    public const string DuplicateId = "reputation.duplicate_id";
    public const string DuplicateActiveKey = "reputation.duplicate_active_key";
    public const string InvalidIdentifier = "reputation.invalid_identifier";
    public const string InvalidReference = "reputation.invalid_reference";
    public const string InvalidValue = "reputation.invalid_value";
    public const string InvalidLifecycle = "reputation.invalid_lifecycle";
    public const string InvalidTimestamp = "reputation.invalid_timestamp";
    public const string InvalidSnapshot = "reputation.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "reputation.unsupported_snapshot_version";
    public const string EventPublicationFailed = "reputation.event_publication_failed";
}
public sealed record ReputationError(string Code, string Message);
public readonly record struct ReputationResult(ReputationError? Error)
{
    public bool IsSuccess => Error is null;
    public static ReputationResult Success() => new(null);
    public static ReputationResult Failure(string code, string message) => new(new(code, message));
}
public readonly record struct ReputationResult<T>(T? Value, ReputationError? Error)
{
    public bool IsSuccess => Error is null;
    public static ReputationResult<T> Success(T value) => new(value, null);
    public static ReputationResult<T> Failure(string code, string message) => new(default, new(code, message));
}

