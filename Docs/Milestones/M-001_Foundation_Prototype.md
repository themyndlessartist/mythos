# M-001 — Foundation Prototype

- Document ID: M-001
- Title: Foundation Prototype
- Version: 1.0
- Status: Complete
- Owner: Mythos Executive Development
- Last Updated: July 2026

--------------------------------------------

# 1. Purpose

Validate that the approved Mythos foundation architecture can operate as a small, deterministic, persistent simulation using Godot and C#.

This milestone proves technical contracts and development workflow. It is not a gameplay vertical slice and does not select or represent the first Mythos title.

--------------------------------------------

# 2. Scope

M-001 includes:

- Godot 4.7 .NET project scaffolding
- Engine-independent C# foundation assemblies where practical
- Automated test projects
- Minimal Entity Framework implementation
- Deterministic Event Framework implementation
- Minimal Time Framework clock and scheduler
- Minimal Region Framework hierarchy and assignment
- Minimal Character and NPC records required for validation
- Save and load proof of concept
- One neutral test world, region hierarchy, and NPC fixture
- Developer diagnostics for foundation state
- Build and test scripts for macOS and Windows
- Technical documentation and milestone report

--------------------------------------------

# 3. Non-Goals

M-001 does not include:

- Combat
- Economy
- Quests
- Crafting
- Relationships or reputation
- Dialogue
- Navigation or production NPC AI
- Art, audio, lore, factions, items, professions, or setting content
- Final user interface
- Final save format or database selection
- Multiplayer, networking, cloud saves, mods, or console support
- Performance claims beyond measured prototype evidence

--------------------------------------------

# 4. Required Deliverables

## Project Foundation

- Godot project opens with the approved .NET editor.
- C# solution builds from the command line on the primary development machine.
- Framework, engine integration, and tests have explicit boundaries.
- Generated files, local caches, editor state, and secrets are ignored.

## Entity Validation

- Create, register, query, retire, and restore entities with stable IDs.
- Reject duplicate IDs and invalid lifecycle transitions.
- Preserve hierarchy, ownership, and region references used by the fixture.

## Event Validation

- Publish and subscribe to immutable events.
- Process events in deterministic order.
- Isolate and report handler failures.
- Provide disableable diagnostic tracing.

## Time Validation

- Advance one authoritative world clock deterministically.
- Schedule absolute, relative, and recurring work needed by the fixture.
- Advance over a large interval without requiring every elapsed minute to execute individually.

## Region Validation

- Create a root world and nested neutral test region.
- Assign and transfer entities through validated region operations.
- Query region hierarchy and assigned entities.

## Character and NPC Validation

- Associate one Character record and one NPC profile with an Entity ID.
- Execute a minimal data-defined schedule between neutral fixture states.
- Demonstrate active and abstract updates without implementing production AI.

## Persistence Validation

- Save the complete fixture state.
- Load it into a fresh runtime state.
- Preserve IDs, references, world time, pending schedules, region assignment, and NPC schedule state.
- Detect a deliberately invalid or incompatible save fixture.
- Preserve the original save when a write or validation step fails.

## Diagnostics

- Inspect entities, recent events, world time, pending schedules, regions, and NPC state.
- Report deterministic seed and framework versions.
- Produce useful structured diagnostics for validation failures.

--------------------------------------------

# 5. Test Requirements

The milestone requires automated:

- Unit tests for each implemented foundation domain
- Cross-domain integration tests
- Persistence round-trip tests
- Determinism tests
- Invalid-data and broken-reference tests
- Scheduler catch-up tests
- Active and abstract NPC fixture tests
- A headless or command-line smoke test suitable for future continuous integration

All tests must pass before M-001 can be accepted.

--------------------------------------------

# 6. Architecture Constraints

- Follow [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md).
- Implement only behavior approved by SYS-001 through SYS-007.
- Document reversible, milestone-local prototype choices under [ADR-024 M-001 Prototype Decision Governance and Test Tooling](../Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md).
- Keep authoritative framework logic independent of Godot APIs whenever practical.
- Use adapters for Godot lifecycle, input, rendering, and scene integration.
- Do not make runtime C# classes the permanent save contract by default.
- Do not introduce third-party runtime dependencies without executive approval.
- Do not add speculative gameplay systems.

--------------------------------------------

# 7. Acceptance Criteria

M-001 is complete when:

1. The project builds reproducibly using documented commands.
2. All required automated tests pass.
3. A neutral fixture advances deterministically through entity, event, time, region, character, and minimal NPC behavior.
4. The fixture survives a save/load round trip without changing persistent identity or approved state.
5. Invalid data and broken references are rejected with diagnostics.
6. Framework logic remains demonstrably separated from Godot presentation concerns.
7. No prohibited gameplay or setting content is present.
8. Documentation describes build, test, architecture, limitations, and known technical debt.
9. The implementation receives executive architecture review before the milestone is marked complete.

--------------------------------------------

# 8. Exit Conditions

Successful completion permits planning for Framework Alpha and additional world-simulation specifications.

Failure to meet deterministic persistence or engine-separation requirements requires correction before higher-level systems are implemented.

--------------------------------------------

# 9. Related Documents

- [SD-001 Project Charter](../Executive/SD-001_Project_Charter.md)
- [SD-002 Framework Overview](../Executive/SD-002_Framework_Overview.md)
- [SD-005 Development Roadmap](../Executive/SD-005_Development_Roadmap.md)
- [ADR-023 Godot and C# Technology Stack](../Architecture/ADR/ADR-023_Godot_and_CSharp_Technology_Stack.md)
- [ADR-024 M-001 Prototype Decision Governance and Test Tooling](../Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md)
- [SYS-001 Entity Framework](../Systems/SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](../Systems/SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](../Systems/SYS-003_Time_Framework.md)
- [SYS-004 Region Framework](../Systems/SYS-004_Region_Framework.md)
- [SYS-005 Character Framework](../Systems/SYS-005_Character_Framework.md)
- [SYS-006 Save and Persistence Framework](../Systems/SYS-006_Save_and_Persistence_Framework.md)
- [SYS-007 NPC Framework](../Systems/SYS-007_NPC_Framework.md)
- [M-001 Completion Report](M-001_Foundation_Prototype_Report.md)
- [SYS-006 Save and Persistence Framework](../Systems/SYS-006_Save_and_Persistence_Framework.md)
- [SYS-007 NPC Framework](../Systems/SYS-007_NPC_Framework.md)
