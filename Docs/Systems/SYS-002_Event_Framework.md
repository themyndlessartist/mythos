# SYS-002 — Event Framework

- Document ID: SYS-002
- Title: Event Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

--------------------------------------------

# 1. Purpose

The Event Framework provides a centralized, deterministic, and decoupled communication mechanism for significant world-state changes.

The Event Framework transports information between systems. It does not implement gameplay behavior.

--------------------------------------------

# 2. Design Principles

1. Published events are immutable.
2. Significant world-state changes should use events whenever practical.
3. Low-level calculations and frame-by-frame implementation details do not require events.
4. Dispatch order must be deterministic.
5. Subscriber failure must not silently corrupt event processing.
6. Event type names must remain setting-agnostic.
7. The framework must distinguish temporary event processing from permanent World History storage.
8. Event payload ownership remains with the publishing domain.
9. Events must not become a substitute for every public interface.
10. The design must not require a specific engine or programming language.

--------------------------------------------

# 3. Responsibilities

The Event Framework owns:

- Event identity
- Event creation
- Publication
- Subscription
- Dispatch
- Routing
- Filtering
- Priority
- Deterministic ordering
- Dispatch status
- Short-term diagnostic event records
- Error isolation and reporting

--------------------------------------------

# 4. Non-Responsibilities

The Event Framework does not own:

- Gameplay logic
- Time progression
- Long-term world history
- Save reconstruction
- NPC decision-making
- Combat resolution
- Economy calculations
- Quest logic
- Rendering or animation messaging unless explicitly adapted through integration layers

Other systems may publish or consume events, but the Event Framework does not implement those systems.

--------------------------------------------

# 5. Public Concepts

## Event ID

A unique identifier for one event occurrence.

## Event Type

A stable, extensible identifier describing what occurred.

## Event Envelope

Conceptual fields:

- Event ID
- Event type
- World timestamp
- Publication sequence
- Source entity, optional
- Target entity or entities, optional
- Region entity, optional
- Payload reference or data
- Priority
- Cancelable flag
- Correlation ID, optional
- Causation ID, optional
- Metadata

Do not define a final language-specific structure or schema.

## Subscriber

A system or service registered to receive selected event types.

## Event Result

Diagnostic information describing dispatch success, cancellation, rejection, or failure.

--------------------------------------------

# 6. Conceptual Public Operations

The framework must eventually support operations equivalent to:

- Publish event
- Subscribe
- Unsubscribe
- Register event type
- Filter by event type
- Filter by source, target, or region
- Cancel an eligible event
- Inspect dispatch result
- Query recent diagnostic events
- Enable or disable diagnostic tracing

This document must not define language-specific method signatures.

--------------------------------------------

# 7. Events Published

The Event Framework may publish diagnostic events such as:

- EventPublished
- EventDispatched
- EventCanceled
- EventRejected
- EventHandlerFailed

Avoid infinite recursive diagnostic publication.

--------------------------------------------

# 8. Events Consumed

The Event Framework should consume only events necessary to maintain dispatch state, durable queues, diagnostics, or framework-level routing after those capabilities are approved.

It must not absorb gameplay responsibilities from other systems.

--------------------------------------------

# 9. Data Ownership

The Event Framework owns event envelopes, dispatch state, subscription metadata, and short-term diagnostic event records.

Event payload ownership remains with the publishing domain.

--------------------------------------------

# 10. Dependencies

Required conceptual dependencies:

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md) for entity references
- Time Framework for authoritative timestamps once available
- Save/Persistence Framework only if durable event queues are later approved
- History Framework for promotion of historically relevant events

Circular dependencies must be avoided.

--------------------------------------------

# 11. Save Requirements

Immediate events normally do not require permanent persistence.

Pending delayed or durable events may require persistence after scheduler and save architecture are approved.

If durable event queues are approved, save data must preserve enough event identity, ordering, routing, and payload-reference information to resume deterministic processing.

--------------------------------------------

# 12. Performance Requirements

The framework should support:

- High event volume
- Fast subscription lookup
- Batch dispatch
- Region filtering
- Priority ordering
- Diagnostic tracing that can be disabled
- Deterministic processing

Exact performance targets will be established after engine selection and prototyping.

--------------------------------------------

# 13. Validation Rules

The framework must detect or prevent:

- Unknown event types
- Invalid entity references
- Invalid region references
- Malformed payloads
- Duplicate event IDs
- Illegal cancellation
- Recursive dispatch loops
- Subscriber registration conflicts

--------------------------------------------

# 14. Extension Points

Future Mythos titles or framework modules may add:

- New event types
- New subscriber categories
- New event filters
- New diagnostic modes
- New routing policies
- New durable-event policies, if approved

Extensions must not require event type names or event payloads to become setting-specific.

--------------------------------------------

# 15. Debugging Requirements

Future implementation should provide developer tools to:

- Inspect recent events
- Inspect event envelopes
- View dispatch order
- View subscribers for an event type
- View dispatch results
- Detect failed handlers
- Detect recursive dispatch loops
- Enable or disable diagnostic tracing
- Export event summaries

--------------------------------------------

# 16. Risks

Primary risks include:

- Turning events into a substitute for every public interface
- Hiding domain ownership behind event payloads
- Creating nondeterministic dispatch order
- Allowing subscriber failures to corrupt processing
- Treating temporary events as permanent world history
- Creating recursive event loops
- Overcommitting to threading or durability before architecture approval

--------------------------------------------

# 17. Deferred Decisions

The following remain open:

- Synchronous versus queued dispatch
- Threading model
- Durable event storage
- Event replay
- Network replication
- Payload serialization
- Exact priority model
- Error recovery policy

--------------------------------------------

# 18. Acceptance Criteria

SYS-002 is complete when it clearly defines:

- What an event is
- What the Event Framework owns
- What it does not own
- Event identity requirements
- Event envelope boundaries
- Subscription and dispatch behavior
- Event publication expectations
- Diagnostic event behavior
- Save requirements
- Validation requirements
- Extension points
- Deferred implementation decisions

--------------------------------------------

# 19. Cross-References

- [SD-001 Project Charter](../Executive/SD-001_Project_Charter.md)
- [SD-002 Framework Overview](../Executive/SD-002_Framework_Overview.md)
- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [ADR-001 Living World](../Architecture/ADR/ADR-001_Living_World.md)
- [ADR-006 Player Independence](../Architecture/ADR/ADR-006_Player_Independence.md)
- [ADR-021 Event-Driven World State](../Architecture/ADR/ADR-021_Event_Driven_World_State.md)
- [ADR-022 Persistent World History](../Architecture/ADR/ADR-022_Persistent_World_History.md)
