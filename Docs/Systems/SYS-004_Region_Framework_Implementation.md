# SYS-004-IMPL-M-001 — Region Framework Implementation Notes

- Document ID: SYS-004-IMPL-M-001
- Title: Region Framework Implementation Notes
- Related Specification: [SYS-004 Region Framework](SYS-004_Region_Framework.md)
- Prototype Milestone: [M-001 Foundation Prototype](../Milestones/M-001_Foundation_Prototype.md)
- Implementation Version: 0.1
- Status: In Progress
- Owner: Mythos Executive Development
- Last Updated: July 2026
- Applies Through Commit: `0d50852`
- Approval/Decision References: [ADR-024 M-001 Prototype Decision Governance and Test Tooling](../Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md)

## Implemented Scope

The M-001 prototype currently provides:

- Engine-independent Region records backed by Region-category Entity Framework entities
- One root world scope and configurable, setting-neutral nested Region categories
- Parent/child hierarchy integration with Entity hierarchy and cycle/orphan prevention
- Symmetric adjacency lookup with opaque, extensible transition metadata
- One validated primary Region assignment per non-terminal entity and explicit source-checked transfer
- Explicit active and abstract simulation state
- Containing-Region simulation ownership validation and resolution
- Deterministically ordered hierarchy, adjacency, containment, assignment, and diagnostic queries
- Opaque boundary references and non-domain-specific Region metadata
- Versioned, defensive snapshots with structured validation and atomic Region-domain restore
- Developer inspection summaries for hierarchy, adjacency, assignment, fidelity, and simulation ownership

## Public Contract and Data Ownership

`RegionFramework` owns Region category, hierarchy integration, adjacency, boundary references, metadata, simulation state, simulation owner, and validation of Entity Region assignment. `EntityRegistry` remains authoritative for entity identity, lifecycle, generic hierarchy, and the primary Region reference.

Region hierarchy is containment only. Adjacency is neighbor lookup only. Neither implies ownership, political control, permitted travel, reachability, exact coordinates, pathfinding, or rendering behavior. Those concerns are absent from the implementation.

## Implementation Decisions

The following decisions are reversible, prototype-local M-001 choices. They do not close or supersede deferred production decisions and remain replaceable under ADR-024.

- M-001 supports exactly one primary Region assignment per entity, matching the current Entity Framework contract. Overlapping and multi-Region assignment remain deferred.
- Region categories are trimmed string identifiers and no hierarchy level names are reserved by core code.
- Adjacency is symmetric and stores opaque string metadata. It does not authorize movement or define a route.
- Boundary representation is an optional opaque reference; coordinate and geometry formats remain deferred.
- New Regions begin abstract. Activation thresholds and policies remain external and deferred.
- Simulation ownership is represented by a containing Region Entity ID. New nested Regions initially use their parent as simulation owner; a root owns its own simulation scope.
- Snapshots are milestone-local coordination records, not a final save schema. Restore expects referenced Entity records to have already been restored through the Entity Framework.
- Restore prepares and validates all Region state before changing live state. Entity assignments are reapplied as a single validated phase with defensive rollback.
- Event publication and Time scheduling are not required by this isolated state-management slice, so no speculative event payloads or schedules were introduced.
- World-integrity callers invoke `ValidateReferences` at persistence boundaries; operational callers use `ValidateAssignment(EntityId)` to validate one Entity and its assigned Region without scanning or being blocked by unrelated Region state.

## Validation and Failure Behavior

Structured `region.*` errors report missing, wrong-category, terminal, self, cyclic, orphaned, malformed, incompatible, and conflicting references. Restore validates version, root uniqueness, parent closure, Entity/Region category and hierarchy agreement, cycles, adjacency, simulation ownership, unique assignments, and all assignment references before replacement.

## Known Limitations and Deferred Work

- The Region Framework is single-threaded and uses deterministic scans rather than query indexes.
- No Region retirement operation or Region event bridge is defined because lifecycle/event payload semantics are not yet approved for this slice.
- Snapshot migration, serialized file format, regional partitioning, streaming, coordinates, geometry, overlapping Regions, local time zones, and activation thresholds remain deferred.
- Entity records must be restored before Region snapshots; complete save orchestration belongs to SYS-006.
- Metadata is intentionally opaque and does not receive domain-specific interpretation.

## Verification

Automated xUnit coverage includes root/nested hierarchy, Entity hierarchy integration, cycles, self-parenting, orphan rejection, adjacency, assignment and source-checked transfer, wrong categories, missing and terminal references, active/abstract state, simulation ownership, deterministic queries, diagnostics, malformed snapshots, atomic failed restore, and round-trip restoration. The repository build script additionally runs the framework smoke executable and both Godot headless checks.
