# SYS-011 Reputation Framework

- Document ID: SYS-011
- Title: Reputation Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Purpose

Represent persistent, audience-scoped assessments of world entities without conflating public reputation with direct Relationships, Information awareness, faction membership, or NPC emotion.

## Core Model

A Reputation Record identifies:

- stable Reputation Record ID
- subject Entity being assessed
- extensible audience-type ID
- optional audience Entity identifying a specific person, Region, Organization, or other approved scope
- extensible dimension ID
- bounded value in the inclusive M-002 range `-1000..1000`
- creation and last-change timestamps
- optional provenance reference

The unique active key is subject, audience type, optional audience Entity, and dimension. Missing reputation is distinct from an explicit neutral value of zero.

## Responsibilities

- Reputation identity and active lifecycle
- Audience-scoped typed dimensions and values
- Deterministic creation, set, bounded delta, retirement, lookup, and queries
- Stable timestamps and provenance
- Versioned atomic snapshots, complete-world persistence, reference validation, diagnostics, and optional events

## Non-Responsibilities

- Direct interpersonal Relationships
- Information truth, knowledge, belief, rumor, or propagation
- Automatic reputation derivation from History or actions
- NPC decisions, emotions, dialogue, reactions, or disposition
- Organization membership, political authority, law, crime, fame, titles, or social class
- Gameplay thresholds, rewards, penalties, decay, forgiveness, or balance
- Title-specific audience types or dimensions

## Operations

- Create Reputation Record
- Find by ID
- Find active record by unique key
- Set value
- Apply bounded delta
- Retire record
- Query by subject, audience type, audience Entity, dimension, or involved Entity
- Validate references
- Inspect diagnostics
- Export and atomically restore a versioned snapshot

Queries and snapshots use stable ordinal ordering.

## Invariants

- IDs and type/dimension/provenance identifiers are initialized and normalized.
- Subject and optional audience Entity remain registered; terminal lifecycle is allowed for historical continuity.
- Subject and audience may be the same only where a title-defined audience scope permits it; the core does not infer meaning.
- Values remain within `-1000..1000`.
- Creation time cannot follow last-change time.
- At most one Active record exists per unique key.
- Retired records remain queryable but immutable; a new record may replace their key.
- Failed publication, mutation, or restore leaves authoritative state unchanged.
- Null collections, unknown lifecycle, duplicate IDs/keys, broken references, and malformed values are rejected.

## Events

- ReputationCreated
- ReputationChanged
- ReputationRetired

Publication is optional through a narrow adapter and occurs before state commit. Automatic subscriptions or projections from History require a later approved integration specification.

## Dependencies

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

Relationship, Information, History, Organization, and NPC systems may integrate later through public contracts but do not own Reputation state.

## Persistence, Performance, and Diagnostics

Persist versioned records with stable IDs, keys, values, lifecycle, timestamps, and provenance. Restore after Entities and before higher systems that consume reputation. M-002 supports deterministic queries and no mandatory per-frame work; production indexes await profiling. Diagnostics expose defensive record projections and validation status.

## Deferred Decisions

- Data-defined audience constraints
- Automatic History/Information projections
- Propagation, visibility, secrecy, distortion, and decay
- Aggregation across Regions or Organizations
- Thresholds, titles, fame, infamy, law, and crime semantics
- Reputation effects on AI, dialogue, prices, politics, or access
- Content Studio authoring

## Tests

- Creation, lookup, direction/scope, mutation, retirement, and replacement tests
- Unique-key, range, timestamp, identifier, lifecycle, and Entity-reference tests
- Deterministic queries and defensive snapshot tests
- Optional event-failure atomicity tests
- Snapshot round-trip, malformed/duplicate input, atomic restore, and reference-drift tests
- Complete-world persistence, deterministic-byte, corruption, and smoke tests

## Acceptance Criteria

SYS-011 is complete when audience-scoped Reputation records, approved operations, persistence, diagnostics, and tests work without introducing automatic derivation, propagation, gameplay effects, NPC behavior, or title-specific semantics.

## Related Documents

- [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [SYS-008 Relationship Framework](SYS-008_Relationship_Framework.md)
- [SYS-009 Information and Knowledge Framework](SYS-009_Information_and_Knowledge_Framework.md)
- [SYS-010 World History Framework](SYS-010_World_History_Framework.md)

