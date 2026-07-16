using Mythos.Framework.Entities;
using System.Collections.ObjectModel;

namespace Mythos.Framework.Events;

/// <summary>
/// Deterministic synchronous event dispatcher for the foundation prototype.
/// </summary>
public sealed class EventBus
{
    private const int MaximumDispatchDepth = 32;

    private readonly Dictionary<EventType, EventTypeRegistration> eventTypes = [];
    private readonly Dictionary<EventType, List<Subscription>> subscriptions = [];
    private readonly HashSet<EventId> publishedEventIds = [];
    private readonly Queue<EventDiagnosticRecord> diagnostics = [];
    private readonly IEventIdGenerator idGenerator;
    private readonly IEventReferenceValidator referenceValidator;
    private readonly int diagnosticCapacity;
    private long publicationSequence;
    private long subscriptionSequence;
    private int dispatchDepth;

    public EventBus(
        IEventReferenceValidator? referenceValidator = null,
        IEventIdGenerator? idGenerator = null,
        int diagnosticCapacity = 256)
    {
        if (diagnosticCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(diagnosticCapacity));
        }

        this.referenceValidator = referenceValidator ?? new RejectingEventReferenceValidator();
        this.idGenerator = idGenerator ?? new Version7EventIdGenerator();
        this.diagnosticCapacity = diagnosticCapacity;
    }

    public bool IsDiagnosticTracingEnabled { get; set; }

    public EventOperationResult RegisterEventType<TPayload>(EventType eventType)
    {
        if (eventTypes.ContainsKey(eventType))
        {
            return EventOperationResult.Failure(
                EventErrorCodes.DuplicateEventType,
                $"Event type '{eventType}' is already registered.");
        }

        eventTypes.Add(eventType, new EventTypeRegistration(typeof(TPayload)));
        return EventOperationResult.Success();
    }

    public EventOperationResult Subscribe(
        EventType eventType,
        SubscriberId subscriberId,
        EventHandler handler,
        EventFilter? filter = null,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!eventTypes.ContainsKey(eventType))
        {
            return EventOperationResult.Failure(EventErrorCodes.UnknownEventType, $"Event type '{eventType}' is not registered.");
        }

        var handlers = subscriptions.GetValueOrDefault(eventType);
        if (handlers is null)
        {
            handlers = [];
            subscriptions.Add(eventType, handlers);
        }

        if (handlers.Any(subscription => subscription.SubscriberId == subscriberId))
        {
            return EventOperationResult.Failure(
                EventErrorCodes.SubscriberConflict,
                $"Subscriber '{subscriberId}' is already registered for '{eventType}'.");
        }

        handlers.Add(new Subscription(subscriberId, handler, filter, order, subscriptionSequence++));
        return EventOperationResult.Success();
    }

    public EventOperationResult Unsubscribe(EventType eventType, SubscriberId subscriberId)
    {
        if (!subscriptions.TryGetValue(eventType, out var handlers))
        {
            return EventOperationResult.Failure(EventErrorCodes.SubscriberNotFound, $"Subscriber '{subscriberId}' was not found.");
        }

        var removed = handlers.RemoveAll(subscription => subscription.SubscriberId == subscriberId);
        return removed > 0
            ? EventOperationResult.Success()
            : EventOperationResult.Failure(EventErrorCodes.SubscriberNotFound, $"Subscriber '{subscriberId}' was not found.");
    }

    public EventDispatchResult Publish(EventRequest request)
    {
        var created = CreateEnvelope(request);
        return created.Error is not null
            ? Reject(null, created.Error)
            : Dispatch(created.Envelope!);
    }

    public IReadOnlyList<EventDispatchResult> PublishBatch(IEnumerable<EventRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var prepared = requests
            .Select(CreateEnvelope)
            .ToArray();

        var rejected = prepared
            .Where(item => item.Error is not null)
            .Select(item => Reject(null, item.Error!));

        var dispatchable = prepared
            .Where(item => item.Envelope is not null)
            .Select(item => item.Envelope!)
            .OrderByDescending(envelope => envelope.Priority)
            .ThenBy(envelope => envelope.PublicationSequence)
            .Select(Dispatch);

        return rejected.Concat(dispatchable).ToArray();
    }

    public IReadOnlyList<EventDiagnosticRecord> GetRecentDiagnostics() => diagnostics.ToArray();

    public IReadOnlyList<SubscriberId> GetSubscribers(EventType eventType) =>
        subscriptions.TryGetValue(eventType, out var handlers)
            ? handlers
                .OrderBy(subscription => subscription.Order)
                .ThenBy(subscription => subscription.RegistrationSequence)
                .Select(subscription => subscription.SubscriberId)
                .ToArray()
            : [];

    private PreparedEvent CreateEnvelope(EventRequest request)
    {
        if (!eventTypes.TryGetValue(request.Type, out var registration))
        {
            return PreparedEvent.Failure(EventErrorCodes.UnknownEventType, $"Event type '{request.Type}' is not registered.");
        }

        if (request.WorldTimestamp < 0)
        {
            return PreparedEvent.Failure(EventErrorCodes.InvalidTimestamp, "World timestamp cannot be negative.");
        }

        if (request.Payload is null || !registration.PayloadType.IsInstanceOfType(request.Payload))
        {
            return PreparedEvent.Failure(
                EventErrorCodes.InvalidPayload,
                $"Event '{request.Type}' requires payload type '{registration.PayloadType.FullName}'.");
        }

        if (request.CorrelationId is { Value: var correlationValue } && correlationValue == Guid.Empty ||
            request.CausationId is { Value: var causationValue } && causationValue == Guid.Empty)
        {
            return PreparedEvent.Failure(
                EventErrorCodes.InvalidReference,
                "Correlation and causation IDs cannot be empty.");
        }

        var id = request.RequestedId ?? idGenerator.Create();
        if (id.Value == Guid.Empty || !publishedEventIds.Add(id))
        {
            return PreparedEvent.Failure(EventErrorCodes.DuplicateEventId, $"Event ID '{id}' is invalid or already published.");
        }

        IReadOnlyList<EntityId> targets = Array.AsReadOnly(request.TargetEntityIds?.Distinct().ToArray() ?? []);
        if (request.SourceEntityId is { } source && !referenceValidator.IsValidEntity(source) ||
            targets.Any(target => !referenceValidator.IsValidEntity(target)) ||
            request.RegionEntityId is { } region && !referenceValidator.IsValidRegion(region))
        {
            publishedEventIds.Remove(id);
            return PreparedEvent.Failure(EventErrorCodes.InvalidReference, "Event contains an invalid entity or region reference.");
        }

        IReadOnlyDictionary<string, string> metadata = new ReadOnlyDictionary<string, string>(
            request.Metadata is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(request.Metadata, StringComparer.Ordinal));

        var envelope = new EventEnvelope(
            id,
            request.Type,
            request.WorldTimestamp,
            publicationSequence++,
            request.SourceEntityId,
            targets,
            request.RegionEntityId,
            request.Payload,
            request.Priority,
            request.IsCancelable,
            request.CorrelationId,
            request.CausationId,
            metadata);

        Trace(EventDiagnosticKind.Published, envelope, null, null);
        return PreparedEvent.Success(envelope);
    }

    private EventDispatchResult Dispatch(EventEnvelope envelope)
    {
        if (dispatchDepth >= MaximumDispatchDepth)
        {
            return Reject(envelope, new EventError(EventErrorCodes.RecursionLimit, "Maximum nested event dispatch depth was exceeded."));
        }

        dispatchDepth++;
        try
        {
            var invoked = new List<SubscriberId>();
            var errors = new List<EventError>();
            var context = new EventContext(envelope);

            if (subscriptions.TryGetValue(envelope.Type, out var handlers))
            {
                foreach (var subscription in handlers
                    .OrderBy(item => item.Order)
                    .ThenBy(item => item.RegistrationSequence))
                {
                    if (subscription.Filter is not null && !subscription.Filter.Matches(envelope))
                    {
                        continue;
                    }

                    invoked.Add(subscription.SubscriberId);
                    try
                    {
                        subscription.Handler(context);
                    }
                    catch (Exception exception)
                    {
                        var error = new EventError(
                            EventErrorCodes.HandlerFailed,
                            $"Subscriber '{subscription.SubscriberId}' failed: {exception.Message}",
                            subscription.SubscriberId);
                        errors.Add(error);
                        Trace(EventDiagnosticKind.HandlerFailed, envelope, subscription.SubscriberId, error.Message);
                    }

                    if (context.ConsumeIllegalCancellationAttempt())
                    {
                        errors.Add(new EventError(
                            EventErrorCodes.IllegalCancellation,
                            $"Subscriber '{subscription.SubscriberId}' attempted to cancel a non-cancelable event.",
                            subscription.SubscriberId));
                    }

                    if (context.IsCanceled)
                    {
                        Trace(EventDiagnosticKind.Canceled, envelope, subscription.SubscriberId, null);
                        return new EventDispatchResult(envelope, EventDispatchStatus.Canceled, invoked, errors);
                    }
                }
            }

            Trace(EventDiagnosticKind.Dispatched, envelope, null, null);
            return new EventDispatchResult(envelope, EventDispatchStatus.Dispatched, invoked, errors);
        }
        finally
        {
            dispatchDepth--;
        }
    }

    private EventDispatchResult Reject(EventEnvelope? envelope, EventError error)
    {
        Trace(EventDiagnosticKind.Rejected, envelope, error.SubscriberId, error.Message);
        return new EventDispatchResult(envelope, EventDispatchStatus.Rejected, [], [error]);
    }

    private void Trace(
        EventDiagnosticKind kind,
        EventEnvelope? envelope,
        SubscriberId? subscriberId,
        string? message)
    {
        if (!IsDiagnosticTracingEnabled || diagnosticCapacity == 0)
        {
            return;
        }

        while (diagnostics.Count >= diagnosticCapacity)
        {
            diagnostics.Dequeue();
        }

        diagnostics.Enqueue(new EventDiagnosticRecord(
            kind,
            envelope?.Id,
            envelope?.Type,
            envelope?.PublicationSequence ?? -1,
            subscriberId,
            message));
    }

    private sealed record EventTypeRegistration(Type PayloadType);

    private sealed record Subscription(
        SubscriberId SubscriberId,
        EventHandler Handler,
        EventFilter? Filter,
        int Order,
        long RegistrationSequence);

    private sealed record PreparedEvent(EventEnvelope? Envelope, EventError? Error)
    {
        public static PreparedEvent Success(EventEnvelope envelope) => new(envelope, null);

        public static PreparedEvent Failure(string code, string message) =>
            new(null, new EventError(code, message));
    }
}
