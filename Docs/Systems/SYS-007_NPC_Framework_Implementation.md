# SYS-007-IMPL-M-001 — NPC Framework Implementation Notes

- Document ID: SYS-007-IMPL-M-001
- Title: NPC Framework Implementation Notes
- Related Specification: [SYS-007 NPC Framework](SYS-007_NPC_Framework.md)
- Prototype Milestone: [M-001 Foundation Prototype](../Milestones/M-001_Foundation_Prototype.md)
- Implementation Version: 0.1
- Status: In Progress
- Owner: Mythos Executive Development
- Last Updated: July 2026
- Applies Through Commit: This document's containing implementation commit
- Approval/Decision References: [ADR-024 M-001 Prototype Decision Governance and Test Tooling](../Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md)

## Implemented Scope

The M-001 fixture provides one engine-independent NPC profile composed one-to-one over an active Character Entity and Character profile. It stores a setting-neutral purpose reference, schedule-definition reference, explicit current schedule entry/state, next due world timestamp, simulation tier, and completed-transition count.

The framework accepts an authoritative `WorldTimestamp` from callers and deterministically advances a minimal cyclic, data-defined schedule. Active and abstract tiers use the same authoritative state transition contract. Catch-up is caller-bounded, reports remaining overdue work, and can be resumed without replaying completed transitions.

Registration, update, restore, and explicit boundary validation verify Entity identity/lifecycle, Character linkage, Region assignment through Region Framework records, purpose and schedule definitions, schedule execution state, and cross-domain drift. Registration, update, simulation-tier mutation, and inspection invoke Entity-scoped Character profile and Region assignment validation, so unrelated domain drift neither suspends a valid NPC nor adds whole-world scan cost to each operation. Failures suspend mutations without changing NPC state. Schedule providers must return a definition whose ID exactly matches the requested profile reference. Versioned snapshots defensively copy profile collections; restore builds and validates a complete candidate before atomically replacing live NPC state. Inspection uses the same scoped validation path and exposes fixture state with structured reference status.

## Prototype-Local Decisions

These choices are reversible M-001 mechanisms under ADR-024 and do not resolve SYS-007 deferred production decisions:

- The fixture uses exactly two public simulation-tier values, `Active` and `Abstract`, solely to prove compatible tier behavior. Final tier names, count, and policies remain deferred.
- A schedule is a non-empty cyclic list of unique opaque state identifiers with positive durations in abstract world units. Calendar-relative schedules and production schedule depth remain deferred.
- The duration of the entry being entered determines its next due timestamp. Schedule execution changes only NPC-owned coordination state and requests no movement, work, or gameplay outcome.
- Purpose and schedule definitions remain externally owned behind `INpcReferenceProvider`; the NPC Framework stores and validates references only.
- NPC catch-up is pull-based and bounded per call. The framework does not register Time tasks or own/advance the authoritative clock.
- Snapshot version 1 is a milestone-local coordination contract, not a final serialization format.
- Operational calls synchronously use scoped Character and Region validation because approved cross-domain lifecycle events do not yet exist. Explicit NPC `ValidateReferences` is the world-integrity boundary: it invokes each global domain validator once, then validates every NPC profile without repeating global scans.

## Boundaries and Failure Behavior

The slice implements no navigation, pathfinding, needs, emotions, goals, ambitions, planning AI, intentions, combat, economy, dialogue, relationships, professions, actions, or title-specific content. It publishes no speculative NPC events and does not mutate Character, Entity, Region, or Time state.

Failures return stable `npc.*` codes. Malformed identifiers, definitions, snapshots, enum values, counts, missing or mismatched references, invalid lifecycle state, duplicate Character links, cross-domain drift, and timestamp/count overflow are rejected. Failed registration, update, tier mutation, and restore do not partially mutate authoritative NPC state.

## Known Limitations and Risks

- Complete multi-domain load ordering and migration belong to SYS-006 and remain unimplemented.
- The framework is single-threaded and uses deterministic scans; indexes await profiling.
- Definition changes can invalidate saved schedule entry state and are reported as reference/state drift; migration policy is deferred.
- Active and abstract tiers intentionally produce identical fixture state. Production fidelity-specific outcome policies remain deferred and must preserve shared domain validation.
- Event publication, activation policies, scheduling integration, and population-scale performance evidence remain future work.

## Verification

Automated coverage includes active and abstract updates, bounded deterministic catch-up, clock non-ownership, one-to-one registration, unrelated Character/Region drift isolation, relevant retired/destroyed Region drift, invalidated Character definitions, purpose and schedule-definition drift, mismatched schedule-provider IDs, atomic failed updates and tier mutations, drift-aware diagnostics, missing and malformed references, lifecycle and enum validation, defensive/versioned snapshots, malformed and duplicate snapshot rejection, atomic failed restore, round trip, smoke integration, Release compilation, and both Godot headless checks.
