using Mythos.Framework.Events;

namespace Mythos.Framework.Time;

public sealed record TimeAdvancedEvent(WorldTimestamp PreviousTimestamp, WorldTimestamp CurrentTimestamp);
public sealed record ScheduledTaskDueEvent(DueScheduledTask Task);

/// <summary>
/// Optional adapter that publishes completed Time Framework outcomes through the approved Event Framework API.
/// The clock and scheduler do not depend on this adapter and never execute event handlers themselves.
/// </summary>
public sealed class TimeEventBridge
{
    public static readonly EventType TimeAdvancedType = new("TimeAdvanced");
    public static readonly EventType ScheduledTaskDueType = new("ScheduledTaskDue");

    private readonly EventBus eventBus;

    private TimeEventBridge(EventBus eventBus)
    {
        this.eventBus = eventBus;
    }

    public static TimeEventBridgeResult Create(EventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        var timeRegistered = eventBus.RegisterEventType<TimeAdvancedEvent>(TimeAdvancedType);
        if (!timeRegistered.IsSuccess)
        {
            return TimeEventBridgeResult.Failure(timeRegistered.Error!);
        }

        var dueRegistered = eventBus.RegisterEventType<ScheduledTaskDueEvent>(ScheduledTaskDueType);
        return dueRegistered.IsSuccess
            ? TimeEventBridgeResult.Success(new TimeEventBridge(eventBus))
            : TimeEventBridgeResult.Failure(dueRegistered.Error!);
    }

    public IReadOnlyList<EventDispatchResult> Publish(TimeAdvanceResult advance)
    {
        if (!advance.IsSuccess)
        {
            return [];
        }

        var requests = new List<EventRequest>
        {
            new(TimeAdvancedType, advance.CurrentTimestamp.Value, new TimeAdvancedEvent(advance.PreviousTimestamp, advance.CurrentTimestamp)),
        };
        requests.AddRange(advance.DueTasks.Select(task =>
            new EventRequest(ScheduledTaskDueType, advance.CurrentTimestamp.Value, new ScheduledTaskDueEvent(task))));
        return eventBus.PublishBatch(requests);
    }
}

public sealed record TimeEventBridgeResult(TimeEventBridge? Value, EventError? Error)
{
    public bool IsSuccess => Error is null;
    public static TimeEventBridgeResult Success(TimeEventBridge value) => new(value, null);
    public static TimeEventBridgeResult Failure(EventError error) => new(null, error);
}
