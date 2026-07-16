# SYS-005-IMPL-M-001 — Character Framework Implementation Notes

- Document ID: SYS-005-IMPL-M-001
- Title: Character Framework Implementation Notes
- Related Specification: [SYS-005 Character Framework](SYS-005_Character_Framework.md)
- Prototype Milestone: [M-001 Foundation Prototype](../Milestones/M-001_Foundation_Prototype.md)
- Implementation Version: 0.1
- Status: In Progress
- Owner: Mythos Executive Development
- Last Updated: July 2026
- Applies Through Commit: This document's containing implementation commit
- Approval/Decision References: [ADR-024 M-001 Prototype Decision Governance and Test Tooling](../Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md)

## Implemented Scope

The M-001 prototype currently provides:

- One engine-independent Character profile per Entity ID
- Registration restricted to existing, active, Character-category entities
- Minimal setting-neutral identity data plus data-defined status and life-stage references
- Deterministic lookup, query, and export ordered by Entity ID
- A versioned, defensively copied Character registry snapshot
- Atomic structured restoration into a fresh candidate state
- Explicit duplicate, identifier, Entity lifecycle/category, reference, version, and malformed-snapshot failures
- Post-registration reference validation for cross-domain lifecycle changes
- Automated xUnit and smoke coverage independent of Godot

## Boundaries

- The Character Framework does not reference Godot APIs.
- The Entity Framework remains authoritative for Entity identity, category, and lifecycle.
- Status and life-stage definitions remain external data owned by title configuration or a later approved definition system. The Character Framework stores only validated references.
- Character identity is a minimal opaque identity value for the neutral M-001 fixture, not a final naming schema.
- Events, mutation operations, and Save Framework orchestration remain deferred.
- Traits, tendencies, skills, professions, classes, aging, needs, memory, knowledge, relationships, reputation, health, inventory, AI, and gameplay are not implemented.

## Implementation Decisions

The following decisions are reversible, prototype-local M-001 choices. They do not close or supersede deferred production decisions and remain replaceable under ADR-024.

- Character profiles are composed over Entity IDs in a separate registry rather than extending Entity records.
- Identity, status, and life-stage values use trimmed opaque string identifiers. Their title-specific semantics are not interpreted by the shared framework.
- An injected `ICharacterReferenceValidator` validates status and life-stage references without transferring ownership of their definitions to the Character Framework.
- Only Active entities may register or restore Character profiles for this fixture. Inactive, Retired, and Destroyed entities remain Entity records but cannot back the active M-001 Character fixture.
- Snapshot version 1 is a milestone-local coordination contract, not the final save format.
- Restore validates the complete candidate snapshot and all external references before replacing live Character state.
- Queries scan and sort profiles by stable Entity ID because M-001 does not justify permanent indexes.

## Known Limitations

- No Character mutation interface is implemented; M-001 profiles are registered and restored as whole records.
- The Character Registry does not subscribe to Entity lifecycle changes. Callers use `ValidateReferences` before persistence or simulation boundaries to diagnose profiles whose Entity became non-active.
- Identity uniqueness is one profile per Entity ID; cross-profile naming uniqueness is neither specified nor enforced.
- Snapshot migrations and multi-domain restore ordering await SYS-006 implementation.
- Status and life-stage definition loading is outside this slice; callers must provide a validator backed by approved data.
- xUnit 3.2.0 is explicitly approved as M-001 prototype test tooling and is not a framework runtime dependency.

## Verification

Automated tests cover registration, lookup, duplicate profiles, missing and wrong-category entities, inactive and terminal entities, malformed identifiers, broken status and life-stage references, deterministic queries and export, defensive snapshot collections, unsupported and malformed snapshots, atomic failed restore, post-registration broken references, and persistence round trip. The repository build script additionally runs Release compilation, the framework smoke executable, Godot headless import, and Godot headless entry-scene execution.
