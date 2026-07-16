# SYS-002 — Event Framework Implementation Notes

- Related Specification: [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- Prototype Milestone: [M-001 Foundation Prototype](../Milestones/M-001_Foundation_Prototype.md)
- Implementation Version: 0.1
- Status: In Progress
- Last Updated: July 2026

## Implemented Scope

The M-001 prototype currently provides:

- Typed Event IDs backed by UUIDv7 through an injectable generator
- Extensible Event Type and Subscriber identifiers
- Event type registration with payload-type validation
- Immutable envelope metadata and copied read-only routing collections
- Synchronous publication and deterministic dispatch
- Batch publication ordered by priority and publication sequence
- Subscription ordering and source, target, and region filters
- Explicit subscribe and unsubscribe operations
- Cancelable events with remaining-handler suppression
- Illegal-cancellation diagnostics for non-cancelable events
- Handler-failure isolation and structured dispatch errors
- Duplicate ID and reference validation
- Correlation and causation metadata
- Bounded, disableable diagnostic tracing
- Nested-dispatch depth protection

## Boundaries

- The Event Framework does not reference Godot APIs.
- Payload content remains owned by the publishing domain.
- Envelope immutability is shallow with respect to the domain-owned payload object.
- Diagnostics are transient and are not World History.
- Immediate events are not persisted.
- Entity and Region references are validated through an injected boundary.

## Implementation Decisions

- M-001 uses synchronous, in-process dispatch.
- Subscriber order is defined by explicit order followed by registration sequence.
- Batch event priority uses an integer where higher values dispatch first.
- Handler exceptions are captured as structured errors and do not prevent later handlers from running.
- A canceled event stops subsequent subscribers after the canceling handler completes.
- Nested dispatch is limited to 32 active levels.

These decisions are contained within the Event Framework and may be replaced by later approved queued, threaded, or durable behavior.

## Known Limitations

- Dispatch is not thread-safe and has no asynchronous mode.
- Event payloads are not serialized or deep-copied.
- Durable queues, replay, networking, and World History promotion are not implemented.
- Published Event IDs are retained for the runtime lifetime to prevent reuse.
- The priority model has not been profiled under high event volume.

## Verification

Automated tests cover:

- Type and payload validation
- Deterministic subscriber and batch ordering
- Routing filters
- Cancellation and illegal cancellation
- Handler-failure isolation
- Duplicate IDs and invalid references
- Bounded diagnostics
- Nested-dispatch protection
- Subscription conflicts and removal
- Caller-owned collection isolation
