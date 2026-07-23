namespace Mythos.Framework.History;

public static class HistoryErrorCodes
{
    public const string NotFound = "history.not_found";
    public const string DuplicateId = "history.duplicate_id";
    public const string DuplicateSource = "history.duplicate_source";
    public const string InvalidIdentifier = "history.invalid_identifier";
    public const string InvalidReference = "history.invalid_reference";
    public const string InvalidEntry = "history.invalid_entry";
    public const string InvalidSnapshot = "history.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "history.unsupported_snapshot_version";
    public const string EventPublicationFailed = "history.event_publication_failed";
}

public sealed record HistoryError(string Code, string Message);
public readonly record struct HistoryResult(HistoryError? Error)
{
    public bool IsSuccess => Error is null;
    public static HistoryResult Success() => new(null);
    public static HistoryResult Failure(string code, string message) => new(new(code, message));
}
public readonly record struct HistoryResult<T>(T? Value, HistoryError? Error)
{
    public bool IsSuccess => Error is null;
    public static HistoryResult<T> Success(T value) => new(value, null);
    public static HistoryResult<T> Failure(string code, string message) => new(default, new(code, message));
}

