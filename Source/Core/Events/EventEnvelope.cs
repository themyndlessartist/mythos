using Mythos.Framework.Entities;

namespace Mythos.Framework.Events;

/// <summary>
/// Immutable routing and diagnostic metadata for one published domain event.
/// </summary>
public sealed record EventEnvelope(
    EventId Id,
    EventType Type,
    long WorldTimestamp,
    long PublicationSequence,
    EntityId? SourceEntityId,
    IReadOnlyList<EntityId> TargetEntityIds,
    EntityId? RegionEntityId,
    object Payload,
    int Priority,
    bool IsCancelable,
    EventId? CorrelationId,
    EventId? CausationId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record EventRequest(
    EventType Type,
    long WorldTimestamp,
    object Payload,
    EntityId? SourceEntityId = null,
    IReadOnlyList<EntityId>? TargetEntityIds = null,
    EntityId? RegionEntityId = null,
    int Priority = 0,
    bool IsCancelable = false,
    EventId? CorrelationId = null,
    EventId? CausationId = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    EventId? RequestedId = null);
