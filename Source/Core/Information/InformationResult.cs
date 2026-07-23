namespace Mythos.Framework.Information;

public static class InformationErrorCodes
{
    public const string NotFound = "information.not_found";
    public const string DuplicateId = "information.duplicate_id";
    public const string DuplicateFact = "information.duplicate_fact";
    public const string InvalidIdentifier = "information.invalid_identifier";
    public const string InvalidReference = "information.invalid_reference";
    public const string InvalidRecord = "information.invalid_record";
    public const string InvalidAwareness = "information.invalid_awareness";
    public const string InvalidTimestamp = "information.invalid_timestamp";
    public const string InvalidSnapshot = "information.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "information.unsupported_snapshot_version";
    public const string EventPublicationFailed = "information.event_publication_failed";
}

public sealed record InformationError(string Code, string Message);
public readonly record struct InformationResult(InformationError? Error)
{
    public bool IsSuccess => Error is null;
    public static InformationResult Success() => new(null);
    public static InformationResult Failure(string code, string message) => new(new(code, message));
}
public readonly record struct InformationResult<T>(T? Value, InformationError? Error)
{
    public bool IsSuccess => Error is null;
    public static InformationResult<T> Success(T value) => new(value, null);
    public static InformationResult<T> Failure(string code, string message) => new(default, new(code, message));
}

