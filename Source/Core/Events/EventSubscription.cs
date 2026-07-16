using Mythos.Framework.Entities;

namespace Mythos.Framework.Events;

public sealed record EventFilter(
    EntityId? SourceEntityId = null,
    EntityId? TargetEntityId = null,
    EntityId? RegionEntityId = null)
{
    internal bool Matches(EventEnvelope envelope) =>
        (SourceEntityId is null || envelope.SourceEntityId == SourceEntityId) &&
        (TargetEntityId is null || envelope.TargetEntityIds.Contains(TargetEntityId.Value)) &&
        (RegionEntityId is null || envelope.RegionEntityId == RegionEntityId);
}

public sealed class EventContext
{
    private readonly bool canCancel;

    internal EventContext(EventEnvelope envelope)
    {
        Event = envelope;
        canCancel = envelope.IsCancelable;
    }

    public EventEnvelope Event { get; }
    public bool IsCanceled { get; private set; }
    public bool IllegalCancellationAttempted { get; private set; }

    public bool Cancel()
    {
        if (!canCancel)
        {
            IllegalCancellationAttempted = true;
            return false;
        }

        IsCanceled = true;
        return true;
    }

    internal bool ConsumeIllegalCancellationAttempt()
    {
        var attempted = IllegalCancellationAttempted;
        IllegalCancellationAttempted = false;
        return attempted;
    }
}

public delegate void EventHandler(EventContext context);
