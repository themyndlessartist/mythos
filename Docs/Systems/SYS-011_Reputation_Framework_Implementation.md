# SYS-011-IMPL-M-002 Reputation Framework Implementation Notes

- Document ID: SYS-011-IMPL-M-002
- Related Specification: [SYS-011 Reputation Framework](SYS-011_Reputation_Framework.md)
- Milestone: [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- Implementation Version: 0.1
- Status: Implemented
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Implemented Scope

The engine-independent `ReputationFramework` stores audience-scoped assessments with stable IDs, a subject Entity, extensible audience type, optional audience Entity, extensible dimension, bounded value, lifecycle, timestamps, and optional provenance. An explicit value of zero is a persisted neutral assessment rather than absence.

Only one Active record may exist for a subject, audience type, optional audience, and dimension key. Active values may be set or changed by bounded deltas; retirement is terminal and permits a new Active replacement. Deterministic queries cover subject, audience type, audience Entity, dimension, and any involved Entity. Registered Retired or Destroyed Entities remain valid references for historical continuity.

An optional event sink publishes before mutation, so publication failure leaves state unchanged. Versioned restoration validates a complete candidate, including IDs, references, values, lifecycle, timestamps, and Active-key uniqueness, before replacing live state.

## Persistence

Complete-world persistence includes a required `reputation` partition restored after Entity identity. IDs, audience scope, dimensions, values, lifecycle, timestamps, and provenance round trip deterministically. The prototype framework marker advances to `m-002.3`.

## Boundaries and Reversible Decisions

The implementation does not derive Reputation from events, Relationships, facts, or history; propagate assessments between audiences; apply gameplay effects; select NPC behavior; or define title-specific dimensions. Those responsibilities remain with future domain integrations and content packages.

M-002 uses values from `-1000..1000`, UUIDv7 default IDs behind an injectable generator, immutable snapshots, and deterministic in-memory scans pending profiling. These choices remain reversible.

## Verification

Coverage includes scoped creation and queries, explicit neutral values, malformed and missing references, bounds, Active-key uniqueness, set and delta operations, terminal lifecycle, replacement, terminal Entity continuity, event-failure atomicity, defensive deterministic snapshots, atomic restoration, complete-world persistence, deterministic bytes, broken audience references, smoke integration, and existing persistence protections.
