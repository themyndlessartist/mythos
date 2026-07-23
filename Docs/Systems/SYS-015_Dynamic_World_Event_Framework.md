# SYS-015 Dynamic World Event Framework

- Document ID: SYS-015
- Title: Dynamic World Event Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Purpose

Represent persistent world situations that may begin, remain active, and reach an explicit outcome without conflating them with transient Event Framework messages, immutable World History, quests, or title-specific story content.

## Core Model

A Dynamic World Event has a stable ID, extensible type ID, lifecycle, creation timestamp, optional scheduled start, optional actual start, optional conclusion timestamp, optional Region, canonical participant Entity set, canonical string attributes, optional outcome ID, optional source reference, and optional provenance.

Lifecycle states are Scheduled, Active, Resolved, Expired, and Cancelled.

## Responsibilities

- Persistent Dynamic World Event identity and lifecycle
- Region, participant, timing, attributes, outcome, source, and provenance references
- Deterministic creation, activation, resolution, expiration, cancellation, lookup, and queries
- Versioned atomic snapshots, complete-world persistence, diagnostics, and optional transient notifications

## Non-Responsibilities

- Transient event dispatch, scheduling execution, automatic generation, trigger evaluation, probability, AI, simulation effects, quest creation, narrative text, dialogue, rewards, balance, or title-specific event types/outcomes
- Automatic mutation of Economy, Property, Organizations, Relationships, Reputation, Information, NPCs, Regions, or Characters
- Automatic promotion to World History or Information
- Time progression or scheduled callback ownership

## Operations

- Create Scheduled or immediately Active Dynamic World Event
- Find by ID
- Activate Scheduled event
- Resolve Active event with outcome
- Expire Scheduled or Active event
- Cancel Scheduled event
- Query by type, lifecycle, participant, Region, outcome, source, or inclusive time range
- Validate references, inspect diagnostics, export and atomically restore snapshot

## Invariants

- IDs and type/outcome/source/provenance identifiers are initialized and normalized.
- Participant Entities are registered, unique, and canonical; terminal participants remain valid for continuity.
- Optional Region is registered through the Region Framework.
- Creation cannot follow scheduled start, actual start, or conclusion when those values exist.
- Scheduled events have no actual start, conclusion, or outcome.
- Active events require actual start and have no conclusion or outcome.
- Resolved events require actual start, conclusion, and outcome.
- Expired events require conclusion and no outcome; actual start is optional.
- Cancelled events require conclusion, no actual start, and no outcome.
- Terminal lifecycle is immutable.
- Failed publication, transition, or restore leaves authoritative state unchanged.

## Events

- DynamicWorldEventCreated
- DynamicWorldEventActivated
- DynamicWorldEventResolved
- DynamicWorldEventExpired
- DynamicWorldEventCancelled

These are optional transient notifications through a narrow adapter. Persistent Dynamic World Events are not Event Framework envelopes.

## Dependencies

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-004 Region Framework](SYS-004_Region_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

World History and Information may consume explicit outcomes later through approved adapters.

## Persistence, Performance, and Diagnostics

Persist complete versioned event state with canonical participants and attributes. Restore after Entities and Regions. M-002 uses deterministic scans and no mandatory per-frame work; Time scheduling and generation engines remain deferred.

## Deferred Decisions

- Event definitions, trigger rules, probability, eligibility, cooldowns, recurrence, and generation
- Automatic Time scheduler integration and catch-up behavior
- Effects, transactions, rollback, compensation, and cross-domain orchestration
- Multi-stage event graphs, branching outcomes, escalation, and dependencies
- Visibility, secrecy, awareness, rumors, and Information integration
- Automatic History promotion, Reputation changes, and quest generation
- Narrative presentation, choices, rewards, and title content
- Content Studio authoring

## Tests

- Creation, lifecycle transitions, outcomes, lookup, and all query paths
- State-shape, timestamp, identifier, participant, Region, and terminal-lifecycle validation
- Event-failure atomicity, defensive canonical collections, deterministic snapshots, and atomic restoration
- Complete-world persistence, deterministic-byte, corruption, and smoke tests

## Acceptance Criteria

SYS-015 is complete when persistent world situations and their explicit lifecycle/outcomes persist and validate deterministically without introducing automatic generation, effects, quests, narrative, AI, or title-specific behavior.

## Related Documents

- [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [ADR-006 Player Independence](../Architecture/ADR/ADR-006_Player_Independence.md)
- [ADR-008 Hybrid World Simulation](../Architecture/ADR/ADR-008_Hybrid_World_Simulation.md)
- [ADR-021 Event-Driven World State](../Architecture/ADR/ADR-021_Event_Driven_World_State.md)
- [SYS-010 World History Framework](SYS-010_World_History_Framework.md)

