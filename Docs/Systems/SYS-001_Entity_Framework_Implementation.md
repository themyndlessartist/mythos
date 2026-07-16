# SYS-001 — Entity Framework Implementation Notes

- Related Specification: [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- Prototype Milestone: [M-001 Foundation Prototype](../Milestones/M-001_Foundation_Prototype.md)
- Implementation Version: 0.1
- Status: In Progress
- Last Updated: July 2026

## Implemented Scope

The M-001 prototype currently provides:

- Typed, serializable Entity IDs backed by UUIDv7 through an injectable generator
- Entity creation, registration, lookup, and terminal lifecycle retention
- Active, inactive, retired, and destroyed lifecycle states
- Extensible category, tag, and component-reference identifiers
- Generic hierarchy and ownership assignments with cycle rejection
- Validated region assignment to Region-category entities
- Immutable public snapshots with defensively copied, read-only collections for persistence coordination
- Deterministically ordered snapshot export
- Structured operation errors
- Automated unit tests independent of Godot

## Boundaries

- The Entity Framework does not reference Godot APIs.
- The Event Framework is available, but Entity event publication remains a separate, deferred integration task.
- Region assignment validation currently uses Entity category metadata; richer Region Framework validation awaits SYS-004 implementation.
- Save orchestration and multi-entity restore ordering await SYS-006 implementation.
- Query indexes are intentionally deferred until prototype profiling demonstrates a need.

## Implementation Decisions

- UUIDv7 is the default prototype generator, isolated behind `IEntityIdGenerator` so the final ID strategy remains replaceable.
- Public state is exposed through snapshots rather than mutable registry records.
- Retired and destroyed records remain registered and queryable.
- Active and inactive entities cannot carry retirement timestamps.
- Hierarchy and ownership are modeled and validated independently.
- Snapshot restoration validates lifecycle enum values, identifiers, collections, timestamps, and references before constructing registry state.

## Known Limitations

- Snapshot registration requires referenced entities to already exist in the registry.
- Query operations currently scan registered entities and sort results for deterministic output.
- Entity events, batch operations, debug export formats, and persistence migrations are not implemented.
- Ownership cycles are rejected by default, as required by SYS-001.
- xUnit 3.2.0 is explicitly approved as M-001 prototype test tooling and is not a framework runtime dependency.

## Verification

The repository build script verifies:

- Release compilation with warnings treated as errors
- xUnit Entity unit tests, including malformed snapshot and lifecycle/reference validation
- Framework smoke test
- Godot project import and C# entry-scene execution
