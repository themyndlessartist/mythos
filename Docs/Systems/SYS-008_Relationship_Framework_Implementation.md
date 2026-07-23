# SYS-008-IMPL-M-002 Relationship Framework Implementation Notes

- Document ID: SYS-008-IMPL-M-002
- Related Specification: [SYS-008 Relationship Framework](SYS-008_Relationship_Framework.md)
- Milestone: [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- Implementation Version: 0.1
- Status: Implemented
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Implemented Scope

The engine-independent `RelationshipFramework` stores directed relationships between two distinct non-destroyed Entity IDs. It provides stable relationship identity, extensible normalized kind IDs, Active/Retired lifecycle, explicit integer dimensions bounded to the approved M-002 range, timestamps, optional provenance, deterministic indexed-style queries, diagnostics, and versioned atomic snapshot restoration.

Only one active relationship may exist for a source, target, and kind tuple. Reverse direction remains independent. Retired records remain queryable and historically referenceable but cannot mutate. A retired tuple may be replaced by a new active record with a new ID.

Mutations build replacement snapshots and publish through the optional `IRelationshipEventSink` before committing state. Event-adapter failure therefore leaves authoritative relationship state unchanged. The adapter is deliberately narrower than SYS-002 so the domain does not depend on a concrete dispatcher configuration.

## Persistence Integration

Complete-world persistence now includes a versioned, integrity-checked `relationships` partition. It restores after Entity identity and before domains that may later consume social state. JSON converters preserve stable IDs, string identifiers, timestamps, and ordinal dimension ordering. Missing, undeclared, malformed, oversized, incompatible, corrupt, duplicate, or broken-reference state fails before a candidate world is exposed.

The prototype framework save marker advances to `m-002.0`; this is an intentional prototype compatibility boundary rather than a production migration policy.

## Boundaries

The implementation does not provide reputation, emotions, memories, decay, dialogue, romance, family rules, organization membership, AI decisions, or setting-specific relationship semantics. It performs no per-frame work.

## Reversible Decisions

- Dimension values use inclusive `-1000..1000` bounds.
- Relationship IDs use UUIDv7 by default behind an injectable generator.
- Persistence uses the existing prototype JSON partition architecture.
- Queries use deterministic in-memory scans pending representative profiling.
- Entity lifecycle integration is validated synchronously; automatic retirement subscriptions remain deferred.

## Verification

Coverage includes directionality, tuple uniqueness, participant validation, bounded set/delta/remove operations, timestamps, retirement, replacement tuples, event-failure atomicity, deterministic queries, defensive collections, snapshot round trips, malformed and duplicate snapshots, atomic failed restore, reference drift, complete-world persistence, deterministic bytes, smoke integration, and existing corruption/atomicity protections.

