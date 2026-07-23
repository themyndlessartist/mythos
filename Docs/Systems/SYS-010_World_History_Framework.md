# SYS-010 World History Framework

- Document ID: SYS-010
- Title: World History Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Purpose

Persist meaningful, immutable records of world changes so later systems can query what happened without treating transient Event dispatch as permanent history.

## Responsibilities

- Stable History Entry identity
- Append-only, setting-agnostic historical records
- Extensible history-type IDs
- Authoritative occurrence timestamps
- Canonically ordered participant Entity references
- Optional Region reference
- Bounded importance and canonical metadata
- Optional source-event and provenance references
- Deterministic recording, lookup, timeline, and filtered queries
- Versioned atomic snapshots, complete-world persistence, validation, diagnostics, and optional domain events

## Non-Responsibilities

- Transient event dispatch or durable event queues
- Deciding automatically which events are historically important
- Narrative prose, lore interpretation, journals, quests, or UI
- Information awareness, belief, rumor, or memory
- Reputation calculations
- Entity lifecycle, Region ownership, or gameplay outcomes
- Retention, pruning, archival compression, or analytics policy

## History Entry

An immutable entry contains:

- History Entry ID
- History-type ID
- Occurred-at World timestamp
- Zero or more unique participant Entity IDs
- Optional Region Entity ID validated by the Region Framework
- Importance in the inclusive M-002 range `0..1000`
- Canonically ordered string metadata
- Optional normalized source-event reference
- Optional normalized provenance reference

At least one participant, Region, or metadata field is required. Historical participants need only remain registered in the Entity Framework; Retired and Destroyed entities are valid historical references.

## Operations

- Record History Entry
- Find by ID
- Query chronological timeline
- Query by type
- Query by participant
- Query by Region
- Query inclusive timestamp range
- Query minimum importance
- Query source-event reference
- Validate references
- Inspect diagnostics
- Export and atomically restore a versioned snapshot

Records cannot be edited or deleted in M-002. Corrections are represented by later entries and future approved semantics.

## Invariants

- Entry IDs and type identifiers are initialized and unique.
- Participant IDs are initialized, unique, canonically ordered, and resolve to registered Entities.
- Optional Region resolves through the Region Framework.
- At least one participant, Region, or metadata field exists.
- Importance remains in `0..1000`.
- Metadata keys and values and optional references are normalized.
- A non-null source-event reference is unique to one History Entry.
- Timeline ordering is occurred-at timestamp followed by History Entry ID.
- Failed recording or restoration leaves current state unchanged.
- Null collections, duplicate IDs, duplicate source references, malformed metadata, and broken references are rejected.

## Events

The framework may publish `HistoryRecorded` through an optional narrow adapter before committing a record. It does not subscribe automatically to every Event Framework event. Promotion policy must be explicit in the owning domain or a future approved integration service.

## Dependencies

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-004 Region Framework](SYS-004_Region_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

World History does not own or depend on Information truth, Relationship values, Reputation calculations, or NPC behavior.

## Persistence

Persist the versioned append-only entry collection with stable IDs and references. Complete-world restoration occurs after Entity and Region state. Unknown, malformed, oversized, corrupt, duplicate, or unresolved state fails atomically.

## Performance and Diagnostics

M-002 supports deterministic queries by ID, type, participant, Region, time range, importance, and source reference. Implementation may use in-memory scans until representative profiling justifies indexes. No mandatory per-frame work is permitted. Diagnostics expose defensive immutable projections and validation status.

## Extension Points

- Explicit Event-to-History promotion policies
- Information and Reputation provenance
- Genealogy and ownership-history projections
- Archival partitions and retention tiers
- Analytics and timeline UI adapters
- Corrections and supersession semantics
- Content Studio inspection tooling

## Deferred Decisions

- Automatic significance scoring
- Retention, pruning, aggregation, and archival
- Narrative text generation and localization
- Corrections, supersession, and disputed history
- Cross-world or multiplayer history
- Grouped campaigns, eras, and chronologies
- Production-scale indexing and partitioning

## Tests

- Record, lookup, timeline, and filtered-query tests
- Retired/Destroyed participant and Region validation tests
- Importance, metadata, normalized identifier, duplicate participant/source, and empty-entry tests
- Deterministic ordering and defensive collection tests
- Optional event-failure atomicity tests
- Snapshot round-trip, malformed snapshot, duplicate, broken-reference, and atomic restore tests
- Complete-world persistence, deterministic-byte, corruption, and smoke tests

## Acceptance Criteria

SYS-010 is complete when immutable historical records, validation, deterministic queries, persistence, diagnostics, and tests are implemented without adding automatic promotion, narrative content, retention policy, reputation, or gameplay logic.

## Related Documents

- [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [ADR-021 Event-Driven World State](../Architecture/ADR/ADR-021_Event_Driven_World_State.md)
- [ADR-022 Persistent World History](../Architecture/ADR/ADR-022_Persistent_World_History.md)

