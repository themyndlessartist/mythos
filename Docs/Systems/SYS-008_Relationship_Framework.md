# SYS-008 Relationship Framework

- Document ID: SYS-008
- Title: Relationship Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Purpose

Represent persistent, queryable relationships between world entities without owning reputation, emotion, dialogue, AI decisions, or setting-specific social rules.

## Design Principles

1. Relationships use stable Entity IDs and the shared world rules.
2. Direction is explicit: a relationship from A to B is not automatically a relationship from B to A.
3. Relationship dimensions are extensible identifiers rather than hard-coded setting concepts.
4. Authoritative changes are deterministic, validated, and event-visible.
5. Missing relationships are distinct from relationships with neutral values.
6. Relationship state is persistable and compatible with active and abstract simulation.
7. The framework stores social state; it does not decide behavior.

## Responsibilities

- Relationship identity and lifecycle
- Source and target Entity references
- Extensible relationship-kind references
- Extensible typed dimensions with bounded integer values
- Optional provenance references for changes
- Deterministic creation, update, retirement, lookup, and queries
- Versioned snapshots and atomic restoration
- Reference validation and diagnostics
- Relationship-domain events

## Non-Responsibilities

- Reputation or public opinion
- Character traits, tendencies, needs, memories, or emotions
- NPC planning and decisions
- Dialogue, romance, family, marriage, or inheritance rules
- Organization membership or political alignment
- Ownership, contracts, quests, combat, or economy
- Information truth, knowledge, belief, or rumor propagation
- Setting-specific relationship kinds, thresholds, or gameplay effects

## Public Concepts

### Relationship ID

An immutable, stable identifier for one relationship record. IDs are never silently replaced during restoration.

### Relationship Record

A directed relationship containing:

- Relationship ID
- Source Entity ID
- Target Entity ID
- Relationship-kind ID
- Lifecycle state
- Dimension values
- Creation timestamp
- Last-change timestamp
- Optional last provenance reference

Source and target must be distinct valid entities. Relationship identity is independent of display names.

### Relationship Kind

An extensible normalized identifier classifying the relationship. Kind definitions and title-specific meanings are supplied through approved data or adapters.

### Dimension

An extensible normalized identifier mapped to an integer in the inclusive range `-1000` through `1000` for the M-002 prototype. Zero is neutral but remains explicit state.

The range is a reversible prototype contract, not final gameplay balance.

### Provenance Reference

An optional normalized reference identifying the event, history entry, decision, or external operation that caused the latest change. The Relationship Framework does not own the referenced domain.

### Lifecycle

M-002 supports Active and Retired states. Retired relationships remain historically referenceable but cannot be mutated or used as active social state.

## Required Operations

- Create relationship
- Find by Relationship ID
- Find active relationship by source, target, and kind
- Query relationships from an entity
- Query relationships toward an entity
- Query relationships involving an entity
- Query by kind
- Set a dimension value
- Apply a bounded dimension delta
- Remove an explicit dimension
- Retire relationship
- Validate one record or all references
- Inspect diagnostics
- Export and atomically restore a versioned snapshot

Queries and snapshots must use stable ordinal ordering.

## Invariants

- Relationship IDs are initialized and unique.
- Source and target IDs are initialized, distinct, and resolve to valid non-destroyed entities.
- Kind and dimension identifiers are normalized, non-empty values.
- At most one active relationship may exist for a source, target, and kind tuple.
- Dimension values remain inside the approved bounds.
- Creation time cannot be after last-change time.
- Retired relationships cannot be changed or retired again.
- Snapshot restoration either succeeds completely or leaves existing state unchanged.
- Null collections, unknown lifecycle values, duplicate dimensions, duplicate IDs, and duplicate active tuples are rejected.

## Events Published

- RelationshipCreated
- RelationshipDimensionChanged
- RelationshipDimensionRemoved
- RelationshipRetired
- RelationshipRestoreRejected

Event payloads identify the relationship and affected entities. Final envelopes and dispatch behavior belong to SYS-002.

## Events Consumed

M-002 does not require automatic subscriptions. Other domains request relationship changes through the public contract. Future lifecycle-event integration requires an approved specification update.

## Data Ownership

The Relationship Framework owns relationship records and their dimension values. It stores Entity references but does not own Entity, Character, NPC, Information, History, Reputation, or Organization state.

## Dependencies

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md) for authoritative timestamps
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

The Entity Framework must not depend on Relationships. Event publication must be optional so the domain remains independently testable.

## Persistence

Persist:

- Snapshot version
- Relationship IDs
- Source and target Entity IDs
- Kind IDs
- Lifecycle states
- Canonically ordered dimensions
- Creation and last-change timestamps
- Provenance references

The complete-world persistence flow restores Relationships after Entities and before higher domains that reference relationship state. Missing or terminal Entity references are rejected without mutating the current world.

## Performance

M-002 must support indexed lookup by ID, active tuple, source, target, participant, and kind. Inactive worlds must not require per-frame relationship processing. Exact population targets remain deferred until representative profiling fixtures exist.

## Diagnostics

Inspection must expose identity, participants, kind, lifecycle, dimensions, timestamps, provenance, and validation status. Debug output must be deterministic and must not expose mutable internal collections.

## Extension Points

- Data-defined relationship kinds and dimensions
- Title-specific interpretation policies
- Information/history provenance adapters
- Reputation projections derived by the Reputation Framework
- Family or organization adapters owned by their respective domains
- Bulk abstract-simulation operations after profiling

## Risks

- Treating relationships as reputation, emotion, or AI behavior
- Encoding title-specific social semantics in shared code
- Allowing duplicate active tuples or silent reverse-direction assumptions
- Using tags instead of structured dimensions
- Mutating collections through snapshots or diagnostics
- Creating circular dependencies with Character, NPC, or Reputation domains

## Deferred Decisions

- Final dimension ranges and balance
- Relationship decay or time-based change
- Symmetric/bidirectional convenience operations
- Family, marriage, romance, and inheritance rules
- Group relationships
- Emotion and memory integration
- Automatic Entity-retirement handling
- Content Studio relationship authoring

## Test Requirements

- Creation, query, mutation, removal, and retirement tests
- Directionality and duplicate-active-tuple tests
- Invalid ID, reference, lifecycle, timestamp, kind, dimension, and range tests
- Deterministic ordering and defensive collection tests
- Snapshot round-trip, malformed snapshot, and atomic restore tests
- Event publication and failure-isolation tests when an Event adapter is supplied
- Complete-world persistence round-trip and broken-reference tests

## Acceptance Criteria

SYS-008 is complete when the approved operations, invariants, persistence integration, diagnostics, and tests are implemented without introducing reputation, emotional simulation, AI decisions, or title-specific mechanics.

## Related Documents

- [SD-001 Project Charter](../Executive/SD-001_Project_Charter.md)
- [SD-002 Framework Overview](../Executive/SD-002_Framework_Overview.md)
- [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [ADR-019 Unified Entity Model](../Architecture/ADR/ADR-019_Unified_Entity_Model.md)
- [ADR-021 Event-Driven World State](../Architecture/ADR/ADR-021_Event_Driven_World_State.md)
- [ADR-022 Persistent World History](../Architecture/ADR/ADR-022_Persistent_World_History.md)
