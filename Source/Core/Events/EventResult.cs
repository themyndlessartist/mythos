namespace Mythos.Framework.Events;

public static class EventErrorCodes
{
    public const string DuplicateEventId = "event.duplicate_id";
    public const string DuplicateEventType = "event.duplicate_type";
    public const string UnknownEventType = "event.unknown_type";
    public const string InvalidPayload = "event.invalid_payload";
    public const string InvalidTimestamp = "event.invalid_timestamp";
    public const string InvalidReference = "event.invalid_reference";
    public const string SubscriberConflict = "event.subscriber_conflict";
    public const string SubscriberNotFound = "event.subscriber_not_found";
    public const string HandlerFailed = "event.handler_failed";
    public const string IllegalCancellation = "event.illegal_cancellation";
    public const string RecursionLimit = "event.recursion_limit";
}

public sealed record EventError(string Code, string Message, SubscriberId? SubscriberId = null);

public readonly record struct EventOperationResult(EventError? Error)
{
    public bool IsSuccess => Error is null;

    public static EventOperationResult Success() => new(null);

    public static EventOperationResult Failure(string code, string message) =>
        new(new EventError(code, message));
}

public enum EventDispatchStatus
{
    Dispatched,
    Canceled,
    Rejected,
}

public sealed record EventDispatchResult(
    int RequestIndex,
    EventEnvelope? Event,
    EventDispatchStatus Status,
    IReadOnlyList<SubscriberId> InvokedSubscribers,
    IReadOnlyList<EventError> Errors)
{
    public bool IsSuccessful => Status == EventDispatchStatus.Dispatched && Errors.Count == 0;
}
