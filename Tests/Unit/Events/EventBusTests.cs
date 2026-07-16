using Mythos.Framework.Entities;
using Mythos.Framework.Events;

namespace Mythos.Framework.UnitTests.Events;

public sealed class EventBusTests
{
    private static readonly EventType TestEvent = new("TestOccurred");

    [Fact]
    public void PublishRejectsUnknownType()
    {
        var result = new EventBus().Publish(Request(new TestPayload(1)));

        Assert.Equal(EventDispatchStatus.Rejected, result.Status);
        Assert.Equal(EventErrorCodes.UnknownEventType, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void PublishRejectsWrongPayloadType()
    {
        var bus = RegisteredBus();

        var result = bus.Publish(Request("wrong"));

        Assert.Equal(EventDispatchStatus.Rejected, result.Status);
        Assert.Equal(EventErrorCodes.InvalidPayload, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void SubscribersRunByOrderThenRegistrationSequence()
    {
        var bus = RegisteredBus();
        var invoked = new List<string>();
        bus.Subscribe(TestEvent, new SubscriberId("second"), _ => invoked.Add("second"), order: 10);
        bus.Subscribe(TestEvent, new SubscriberId("first-a"), _ => invoked.Add("first-a"), order: 0);
        bus.Subscribe(TestEvent, new SubscriberId("first-b"), _ => invoked.Add("first-b"), order: 0);

        var result = bus.Publish(Request(new TestPayload(1)));

        Assert.True(result.IsSuccessful);
        Assert.Equal(["first-a", "first-b", "second"], invoked);
    }

    [Fact]
    public void BatchRunsHigherPriorityFirstAndPreservesSequenceForTies()
    {
        var bus = RegisteredBus();
        var values = new List<int>();
        bus.Subscribe(TestEvent, new SubscriberId("collector"), context =>
            values.Add(((TestPayload)context.Event.Payload).Value));

        var results = bus.PublishBatch([
            Request(new TestPayload(1), priority: 0),
            Request(new TestPayload(2), priority: 5),
            Request(new TestPayload(3), priority: 5),
        ]);

        Assert.All(results, result => Assert.True(result.IsSuccessful));
        Assert.Equal([1, 2, 0], results.Select(result => result.RequestIndex));
        Assert.Equal([2, 3, 1], values);
    }

    [Fact]
    public void FilterMatchesSourceTargetAndRegion()
    {
        var source = EntityIdFrom(1);
        var target = EntityIdFrom(2);
        var region = EntityIdFrom(3);
        var bus = RegisteredBus(new AllowingEventReferenceValidator());
        var calls = 0;
        bus.Subscribe(
            TestEvent,
            new SubscriberId("filtered"),
            _ => calls++,
            new EventFilter(source, target, region));

        bus.Publish(Request(new TestPayload(1), source, [target], region));
        bus.Publish(Request(new TestPayload(2), source, [], region));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void CancelableEventStopsRemainingSubscribers()
    {
        var bus = RegisteredBus();
        var laterCalled = false;
        bus.Subscribe(TestEvent, new SubscriberId("cancel"), context => Assert.True(context.Cancel()));
        bus.Subscribe(TestEvent, new SubscriberId("later"), _ => laterCalled = true, order: 10);

        var result = bus.Publish(Request(new TestPayload(1), cancelable: true));

        Assert.Equal(EventDispatchStatus.Canceled, result.Status);
        Assert.False(laterCalled);
        Assert.Single(result.InvokedSubscribers);
    }

    [Fact]
    public void IllegalCancellationIsReportedWithoutStoppingDispatch()
    {
        var bus = RegisteredBus();
        var laterCalled = false;
        bus.Subscribe(TestEvent, new SubscriberId("cancel"), context => Assert.False(context.Cancel()));
        bus.Subscribe(TestEvent, new SubscriberId("later"), _ => laterCalled = true, order: 10);

        var result = bus.Publish(Request(new TestPayload(1)));

        Assert.Equal(EventDispatchStatus.Dispatched, result.Status);
        Assert.True(laterCalled);
        Assert.Equal(EventErrorCodes.IllegalCancellation, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void FailedHandlerIsIsolatedAndLaterSubscriberRuns()
    {
        var bus = RegisteredBus();
        var laterCalled = false;
        bus.Subscribe(TestEvent, new SubscriberId("failing"), _ => throw new InvalidOperationException("expected"));
        bus.Subscribe(TestEvent, new SubscriberId("later"), _ => laterCalled = true, order: 10);

        var result = bus.Publish(Request(new TestPayload(1)));

        Assert.Equal(EventDispatchStatus.Dispatched, result.Status);
        Assert.False(result.IsSuccessful);
        Assert.True(laterCalled);
        Assert.Equal(EventErrorCodes.HandlerFailed, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void DuplicateRequestedIdIsRejected()
    {
        var eventId = new EventId(Guid.CreateVersion7());
        var bus = RegisteredBus();
        Assert.True(bus.Publish(Request(new TestPayload(1), requestedId: eventId)).IsSuccessful);

        var duplicate = bus.Publish(Request(new TestPayload(2), requestedId: eventId));

        Assert.Equal(EventDispatchStatus.Rejected, duplicate.Status);
        Assert.Equal(EventErrorCodes.DuplicateEventId, Assert.Single(duplicate.Errors).Code);
    }

    [Fact]
    public void EntityRegistryValidatorRejectsUnknownReferences()
    {
        var registry = new EntityRegistry(new FixedEntityIdGenerator(EntityIdFrom(1)));
        var known = registry.Create(new EntityCategory("Character"), 0).Value!.Id;
        var bus = RegisteredBus(new EntityRegistryEventReferenceValidator(registry));

        Assert.True(bus.Publish(Request(new TestPayload(1), source: known)).IsSuccessful);
        var invalid = bus.Publish(Request(new TestPayload(2), source: EntityIdFrom(99)));

        Assert.Equal(EventDispatchStatus.Rejected, invalid.Status);
        Assert.Equal(EventErrorCodes.InvalidReference, Assert.Single(invalid.Errors).Code);
    }

    [Fact]
    public void DefaultValidatorRejectsReferencedEvents()
    {
        var result = RegisteredBus().Publish(Request(new TestPayload(1), source: EntityIdFrom(1)));

        Assert.Equal(EventDispatchStatus.Rejected, result.Status);
        Assert.Equal(EventErrorCodes.InvalidReference, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void EntityRegistryValidatorChecksEveryTargetAndRegionCategory()
    {
        var source = EntityIdFrom(1);
        var target = EntityIdFrom(2);
        var wrongRegion = EntityIdFrom(3);
        var region = EntityIdFrom(4);
        var registry = new EntityRegistry(new SequenceEntityIdGenerator(source, target, wrongRegion, region));
        Assert.True(registry.Create(new EntityCategory("Character"), 0).IsSuccess);
        Assert.True(registry.Create(new EntityCategory("Character"), 0).IsSuccess);
        Assert.True(registry.Create(new EntityCategory("Character"), 0).IsSuccess);
        Assert.True(registry.Create(new EntityCategory("Region"), 0).IsSuccess);
        var bus = RegisteredBus(new EntityRegistryEventReferenceValidator(registry));

        var missingTarget = bus.Publish(Request(new TestPayload(1), source, [target, EntityIdFrom(99)], region));
        var wrongCategory = bus.Publish(Request(new TestPayload(2), source, [target], wrongRegion));
        var valid = bus.Publish(Request(new TestPayload(3), source, [target], region));

        Assert.Equal(EventDispatchStatus.Rejected, missingTarget.Status);
        Assert.Equal(EventDispatchStatus.Rejected, wrongCategory.Status);
        Assert.True(valid.IsSuccessful);
    }

    [Theory]
    [InlineData(EntityLifecycleState.Retired)]
    [InlineData(EntityLifecycleState.Destroyed)]
    public void EntityRegistryValidatorRejectsTerminalReferences(EntityLifecycleState terminalState)
    {
        var id = EntityIdFrom(1);
        var registry = new EntityRegistry(new FixedEntityIdGenerator(id));
        Assert.True(registry.Create(new EntityCategory("Character"), 0).IsSuccess);
        var transition = terminalState == EntityLifecycleState.Retired
            ? registry.Retire(id, 1)
            : registry.Destroy(id, 1);
        Assert.True(transition.IsSuccess);
        var bus = RegisteredBus(new EntityRegistryEventReferenceValidator(registry));

        var result = bus.Publish(Request(new TestPayload(1), source: id));

        Assert.Equal(EventDispatchStatus.Rejected, result.Status);
        Assert.Equal(EventErrorCodes.InvalidReference, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void EntityRegistryValidatorRejectsTerminalTargetAndRegionReferences()
    {
        var target = EntityIdFrom(1);
        var region = EntityIdFrom(2);
        var registry = new EntityRegistry(new SequenceEntityIdGenerator(target, region));
        Assert.True(registry.Create(new EntityCategory("Character"), 0).IsSuccess);
        Assert.True(registry.Create(new EntityCategory("Region"), 0).IsSuccess);
        Assert.True(registry.Retire(target, 1).IsSuccess);
        Assert.True(registry.Destroy(region, 1).IsSuccess);
        var bus = RegisteredBus(new EntityRegistryEventReferenceValidator(registry));

        var terminalTarget = bus.Publish(Request(new TestPayload(1), targets: [target]));
        var terminalRegion = bus.Publish(Request(new TestPayload(2), region: region));

        Assert.Equal(EventDispatchStatus.Rejected, terminalTarget.Status);
        Assert.Equal(EventDispatchStatus.Rejected, terminalRegion.Status);
    }

    [Fact]
    public void EmptyCorrelationOrCausationIdIsRejected()
    {
        var bus = RegisteredBus();
        var empty = new EventId(Guid.Empty);

        var correlation = bus.Publish(Request(new TestPayload(1), correlationId: empty));
        var causation = bus.Publish(Request(new TestPayload(2), causationId: empty));

        Assert.Equal(EventDispatchStatus.Rejected, correlation.Status);
        Assert.Equal(EventDispatchStatus.Rejected, causation.Status);
        Assert.All(correlation.Errors.Concat(causation.Errors), error => Assert.Equal(EventErrorCodes.InvalidReference, error.Code));
    }

    [Fact]
    public void CorrelationAndCausationArePreserved()
    {
        var correlation = new EventId(Guid.CreateVersion7());
        var causation = new EventId(Guid.CreateVersion7());

        var result = RegisteredBus().Publish(Request(
            new TestPayload(1),
            correlationId: correlation,
            causationId: causation));

        Assert.True(result.IsSuccessful);
        Assert.Equal(correlation, result.Event!.CorrelationId);
        Assert.Equal(causation, result.Event.CausationId);
    }

    [Fact]
    public void BatchCorrelatesValidBeforeInvalidRequests()
    {
        var values = new List<int>();
        var bus = RegisteredBus();
        bus.Subscribe(TestEvent, new SubscriberId("collector"), context => values.Add(((TestPayload)context.Event.Payload).Value));

        var results = bus.PublishBatch([
            Request(new TestPayload(1), correlationId: new EventId(Guid.CreateVersion7())),
            Request(new TestPayload(2), correlationId: new EventId(Guid.Empty)),
        ]);

        Assert.Equal(2, results.Count);
        Assert.Equal(EventDispatchStatus.Rejected, results[0].Status);
        Assert.Equal(1, results[0].RequestIndex);
        Assert.True(results[1].IsSuccessful);
        Assert.Equal(0, results[1].RequestIndex);
        Assert.Equal([1], values);
    }

    [Fact]
    public void BatchCorrelatesMultipleMixedFailures()
    {
        var bus = RegisteredBus();

        var results = bus.PublishBatch([
            Request(new TestPayload(1), priority: 1),
            Request(new TestPayload(2), correlationId: new EventId(Guid.Empty)),
            Request("wrong"),
            Request(new TestPayload(3), priority: 5),
        ]);

        Assert.Equal([1, 2, 3, 0], results.Select(result => result.RequestIndex));
        Assert.Equal(
            [EventDispatchStatus.Rejected, EventDispatchStatus.Rejected, EventDispatchStatus.Dispatched, EventDispatchStatus.Dispatched],
            results.Select(result => result.Status));
    }

    [Fact]
    public void DiagnosticsAreBoundedAndCanBeDisabled()
    {
        var bus = RegisteredBus(diagnosticCapacity: 2);
        bus.IsDiagnosticTracingEnabled = true;
        bus.Publish(Request(new TestPayload(1)));
        bus.Publish(Request(new TestPayload(2)));

        Assert.Equal(2, bus.GetRecentDiagnostics().Count);

        bus.IsDiagnosticTracingEnabled = false;
        bus.Publish(Request(new TestPayload(3)));
        Assert.Equal(2, bus.GetRecentDiagnostics().Count);
    }

    [Fact]
    public void NestedPublicationIsBounded()
    {
        var bus = RegisteredBus();
        EventDispatchResult? deepest = null;
        bus.Subscribe(TestEvent, new SubscriberId("recursive"), _ =>
        {
            var nested = bus.Publish(Request(new TestPayload(1)));
            if (nested.Status == EventDispatchStatus.Rejected)
            {
                deepest = nested;
            }
        });

        bus.Publish(Request(new TestPayload(0)));

        Assert.NotNull(deepest);
        Assert.Equal(EventErrorCodes.RecursionLimit, Assert.Single(deepest.Errors).Code);
    }

    [Fact]
    public void SubscriptionIdentityCannotConflictForOneType()
    {
        var bus = RegisteredBus();
        var subscriber = new SubscriberId("duplicate");
        Assert.True(bus.Subscribe(TestEvent, subscriber, _ => { }).IsSuccess);

        var conflict = bus.Subscribe(TestEvent, subscriber, _ => { });

        Assert.False(conflict.IsSuccess);
        Assert.Equal(EventErrorCodes.SubscriberConflict, conflict.Error!.Code);
    }

    [Fact]
    public void UnsubscribeRemovesOnlyRequestedSubscriber()
    {
        var bus = RegisteredBus();
        var calls = 0;
        var subscriber = new SubscriberId("temporary");
        Assert.True(bus.Subscribe(TestEvent, subscriber, _ => calls++).IsSuccess);

        Assert.True(bus.Unsubscribe(TestEvent, subscriber).IsSuccess);
        bus.Publish(Request(new TestPayload(1)));

        Assert.Equal(0, calls);
        Assert.Empty(bus.GetSubscribers(TestEvent));
    }

    [Fact]
    public void EnvelopeCopiesCallerOwnedRoutingCollections()
    {
        var target = EntityIdFrom(1);
        var targets = new List<EntityId> { target };
        var metadata = new Dictionary<string, string> { ["key"] = "original" };
        var bus = RegisteredBus(new AllowingEventReferenceValidator());
        EventEnvelope? captured = null;
        bus.Subscribe(TestEvent, new SubscriberId("capture"), context => captured = context.Event);

        bus.Publish(new EventRequest(TestEvent, 10, new TestPayload(1), TargetEntityIds: targets, Metadata: metadata));
        targets.Clear();
        metadata["key"] = "changed";

        Assert.NotNull(captured);
        Assert.Equal(target, Assert.Single(captured.TargetEntityIds));
        Assert.Equal("original", captured.Metadata["key"]);
    }

    private static EventBus RegisteredBus(
        IEventReferenceValidator? validator = null,
        int diagnosticCapacity = 256)
    {
        var bus = new EventBus(validator, diagnosticCapacity: diagnosticCapacity);
        Assert.True(bus.RegisterEventType<TestPayload>(TestEvent).IsSuccess);
        return bus;
    }

    private static EventRequest Request(
        object payload,
        EntityId? source = null,
        IReadOnlyList<EntityId>? targets = null,
        EntityId? region = null,
        int priority = 0,
        bool cancelable = false,
        EventId? requestedId = null,
        EventId? correlationId = null,
        EventId? causationId = null) =>
        new(
            TestEvent,
            10,
            payload,
            source,
            targets,
            region,
            priority,
            cancelable,
            correlationId,
            causationId,
            RequestedId: requestedId);

    private static EntityId EntityIdFrom(int value) => new(new Guid(value, 0, 0, new byte[8]));

    private sealed record TestPayload(int Value);

    private sealed class FixedEntityIdGenerator(EntityId id) : IEntityIdGenerator
    {
        public EntityId Create() => id;
    }

    private sealed class SequenceEntityIdGenerator(params EntityId[] ids) : IEntityIdGenerator
    {
        private readonly Queue<EntityId> ids = new(ids);

        public EntityId Create() => ids.Dequeue();
    }

    private sealed class AllowingEventReferenceValidator : IEventReferenceValidator
    {
        public bool IsValidEntity(EntityId id) => id.Value != Guid.Empty;

        public bool IsValidRegion(EntityId id) => id.Value != Guid.Empty;
    }
}
