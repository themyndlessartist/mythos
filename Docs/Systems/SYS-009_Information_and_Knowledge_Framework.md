# SYS-009 Information and Knowledge Framework

- Document ID: SYS-009
- Title: Information and Knowledge Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Purpose

Represent objective world facts separately from information that entities know, believe, or disbelieve. The framework provides persistent epistemic state without deciding NPC behavior, dialogue, rumor propagation, or narrative meaning.

## Core Model

The M-002 model has three layers:

1. **Information** is an immutable proposition that may be true, false, incomplete, or unverified.
2. **Fact** declares that one Information record is authoritative world truth.
3. **Awareness** records one entity's stance toward one Information record.

False or distorted beliefs are represented by awareness of Information that has no authoritative Fact declaration. The framework never treats confidence as proof of truth.

## Responsibilities

- Stable Information and Fact identity
- Immutable, setting-agnostic proposition records
- Optional subject and object Entity references
- Canonically ordered proposition attributes
- Authoritative Fact declarations
- Per-entity awareness with Known, Believed, or Disbelieved stance
- Bounded confidence, timestamps, and optional source/provenance references
- Deterministic creation, declaration, awareness updates, forgetting, lookup, and queries
- Versioned atomic snapshots, persistence, validation, diagnostics, and domain events

## Non-Responsibilities

- NPC decisions, goals, dialogue, or action selection
- Memory capacity, emotional response, or cognitive simulation
- Automatic discovery, observation, sharing, rumor, distortion, or decay
- World History retention policy
- Reputation derivation
- Quest state or narrative interpretation
- Secrecy, access control, encryption, or player-facing journal UI
- Title-specific proposition types or attribute semantics

## Public Concepts

### Information Record

An immutable proposition containing:

- Information ID
- Information-type ID
- Optional subject Entity ID
- Optional object Entity ID
- Canonically ordered string attributes
- Creation timestamp
- Optional provenance reference

At least one subject, object, or attribute is required. Subject and object may be the same where a title-defined proposition permits it. Referenced Entities must exist and not be Destroyed.

### Fact Declaration

An immutable declaration that one Information record is authoritative truth from an effective timestamp. A Fact has its own stable ID and optional provenance. One active Fact declaration is permitted per Information record in M-002.

Fact retirement and changing truth over time belong to World History and later specifications. A new time-scoped proposition should be created instead of mutating an existing one.

### Awareness Record

A mutable record keyed by Knower Entity ID and Information ID:

- Epistemic stance: Known, Believed, or Disbelieved
- Confidence from 0 through 1000
- Acquired timestamp
- Last-updated timestamp
- Optional source Entity ID
- Optional provenance reference

Known stance requires an existing Fact declaration for the Information record. Believed and Disbelieved do not imply objective truth or falsehood.

### Forgetting

Forgetting removes an Awareness record. It does not delete Information or Facts and does not erase World History.

## Operations

- Create Information
- Find Information by ID
- Query by type, subject, object, or involved Entity
- Declare Fact
- Find Fact by ID or Information ID
- Query authoritative Facts
- Set or update Awareness
- Find Awareness by knower and Information
- Query Awareness by knower, Information, stance, or source
- Forget Awareness
- Determine whether Information is authoritative
- Validate references
- Inspect Information, Fact, and Awareness diagnostics
- Export and atomically restore a versioned snapshot

All queries and snapshots use stable ordinal ordering.

## Invariants

- IDs and normalized type/provenance identifiers are initialized.
- Information IDs and Fact IDs are unique and never reused.
- Information propositions are immutable after creation.
- Referenced Entities resolve and are not Destroyed.
- Attributes are normalized unique keys and normalized values.
- At least one subject, object, or attribute is present.
- A Fact references existing Information and is unique per Information record.
- Awareness knower and optional source resolve to non-destroyed Entities.
- One Awareness record exists per knower/information tuple.
- Known Awareness requires a Fact.
- Confidence remains within `0..1000`.
- Acquired time cannot follow last-updated time.
- Failed mutation or restore leaves current state unchanged.
- Null collections, duplicate IDs/tuples, unknown enum values, malformed references, and mutable collection leaks are rejected.

## Events Published

- InformationCreated
- FactDeclared
- AwarenessCreated
- AwarenessChanged
- AwarenessForgotten
- InformationRestoreRejected

Event publication is optional through a narrow adapter and must fail before authoritative state is committed.

## Data Ownership and Dependencies

The framework owns Information, Fact declarations, and Awareness. It references but does not own Entity, Event, Time, Character, NPC, Relationship, History, or Reputation data.

Dependencies:

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

## Persistence

Persist versioned Information, Fact, and Awareness collections with canonical attributes and stable references. Complete-world restoration occurs after Entities and before History, Reputation, or NPC systems that may consume epistemic state. Invalid input fails without exposing a partial candidate.

## Performance and Diagnostics

M-002 requires deterministic lookup/query paths by ID, type, subject, object, knower, Information, stance, and source. The framework performs no mandatory per-frame processing. Diagnostics expose records and validation status through defensive projections. Production indexes and population targets await representative profiling.

## Extension Points

- Observation and discovery adapters
- Data-defined proposition types
- Rumor/share/distortion policies
- Memory retention and forgetting policies
- History provenance links
- Secrecy and access-control domains
- Reputation projections
- Content Studio authoring after approved workflow design

## Deferred Decisions

- Automatic information propagation and distortion
- Confidence-combination rules
- Contradiction detection between propositions
- Fact retirement, correction, and temporal truth
- Memory capacity, decay, and emotional salience
- Secrets, evidence, witnesses, and credibility models
- Player-facing journals and disclosure UI
- Group or organization awareness

## Tests

- Information creation, immutability, reference, attribute, and deterministic-query tests
- Fact uniqueness and broken-reference tests
- Awareness stance, confidence, source, timestamp, update, and forgetting tests
- Known-without-Fact rejection and false-belief representation tests
- Event-failure atomicity tests
- Defensive collection, malformed snapshot, duplicate, atomic restore, and round-trip tests
- Complete-world persistence, deterministic-byte, corruption, and unresolved-reference tests
- Smoke integration without gameplay or propagation logic

## Acceptance Criteria

SYS-009 is complete when the three-layer model, approved operations, persistence, diagnostics, and tests work without introducing narrative content, automated cognition, propagation, reputation, or title-specific semantics.

## Related Documents

- [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [ADR-018 Information and Knowledge](../Architecture/ADR/ADR-018_Information_and_Knowledge.md)
- [ADR-019 Unified Entity Model](../Architecture/ADR/ADR-019_Unified_Entity_Model.md)
- [ADR-021 Event-Driven World State](../Architecture/ADR/ADR-021_Event_Driven_World_State.md)
- [ADR-022 Persistent World History](../Architecture/ADR/ADR-022_Persistent_World_History.md)

