# SYS-009-IMPL-M-002 Information and Knowledge Framework Implementation Notes

- Document ID: SYS-009-IMPL-M-002
- Related Specification: [SYS-009 Information and Knowledge Framework](SYS-009_Information_and_Knowledge_Framework.md)
- Milestone: [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- Implementation Version: 0.1
- Status: Implemented
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Implemented Scope

The engine-independent `InformationFramework` implements the approved three-layer model:

- immutable Information propositions with stable identity, extensible type, optional Entity subject/object, canonical attributes, timestamp, and provenance;
- immutable Fact declarations identifying authoritative propositions;
- per-Entity Awareness records with Known, Believed, or Disbelieved stance, bounded confidence, acquisition/update timestamps, optional source Entity, and provenance.

Known Awareness requires an authoritative Fact. Believed and Disbelieved Awareness may reference unverified Information, allowing false or distorted beliefs without modifying objective truth. Forgetting removes Awareness only; Information and Facts remain intact.

All mutations validate first and publish through an optional narrow event sink before committing state. Snapshots and diagnostics expose defensive collections. Restore constructs and validates complete candidate dictionaries before atomically replacing live state.

## Persistence

Complete-world persistence includes a required versioned `information` partition restored after Entity identity and before future History, Reputation, or NPC consumers. Stable Information/Fact IDs and normalized type IDs use strict JSON converters; proposition attributes remain ordinal-canonical. The prototype framework marker advances to `m-002.1`.

## Boundaries and Reversible Decisions

The implementation does not propagate rumors, distort claims, combine confidence, detect contradictions, decay memories, simulate emotions, generate dialogue, derive reputation, or interpret title-specific proposition semantics.

M-002 uses confidence bounds `0..1000`, UUIDv7 default IDs behind an injectable generator, deterministic in-memory scans pending profiling, and synchronous Entity reference validation. These are reversible implementation choices.

## Verification

Coverage includes authoritative facts, unverified beliefs, Known-without-Fact rejection, immutable propositions, malformed references and attributes, duplicate Facts, stance/confidence/timestamp rules, awareness updates and forgetting, deterministic queries, defensive snapshots, event-failure atomicity, atomic restore, reference drift, complete-world round trips, deterministic bytes, corrupt references, smoke integration, and the existing persistence integrity suite.

