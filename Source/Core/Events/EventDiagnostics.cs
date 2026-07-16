namespace Mythos.Framework.Events;

public enum EventDiagnosticKind
{
    Published,
    Dispatched,
    Canceled,
    Rejected,
    HandlerFailed,
}

public sealed record EventDiagnosticRecord(
    EventDiagnosticKind Kind,
    EventId? EventId,
    EventType? EventType,
    long PublicationSequence,
    SubscriberId? SubscriberId,
    string? Message);
