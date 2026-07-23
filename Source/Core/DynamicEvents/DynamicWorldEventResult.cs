namespace Mythos.Framework.DynamicEvents;

public static class DynamicWorldEventErrorCodes
{
    public const string NotFound = "dynamic_event.not_found";
    public const string DuplicateId = "dynamic_event.duplicate_id";
    public const string InvalidIdentifier = "dynamic_event.invalid_identifier";
    public const string InvalidReference = "dynamic_event.invalid_reference";
    public const string InvalidLifecycle = "dynamic_event.invalid_lifecycle";
    public const string InvalidTimestamp = "dynamic_event.invalid_timestamp";
    public const string InvalidSnapshot = "dynamic_event.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "dynamic_event.unsupported_snapshot_version";
    public const string EventPublicationFailed = "dynamic_event.publication_failed";
}
public sealed record DynamicWorldEventError(string Code, string Message);
public readonly record struct DynamicWorldEventResult(DynamicWorldEventError? Error)
{
    public bool IsSuccess => Error is null;
    public static DynamicWorldEventResult Success() => new(null);
    public static DynamicWorldEventResult Failure(string code, string message) => new(new(code, message));
}
public readonly record struct DynamicWorldEventResult<T>(T? Value, DynamicWorldEventError? Error)
{
    public bool IsSuccess => Error is null;
    public static DynamicWorldEventResult<T> Success(T value) => new(value, null);
    public static DynamicWorldEventResult<T> Failure(string code, string message) => new(default, new(code, message));
}
