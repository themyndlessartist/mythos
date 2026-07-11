# Mythos Agent Instructions

These instructions apply to all future Codex sessions and automated implementation work in the Mythos repository.

## Core Rules

1. Read all relevant approved documentation before modifying code.
2. Do not invent or redesign gameplay systems.
3. Identify ambiguities and architectural conflicts before implementation.
4. Prefer modular, reusable, data-driven solutions.
5. Avoid hard-coded setting assumptions in shared framework code.
6. Keep systems loosely coupled.
7. Prefer composition over deep inheritance.
8. Add or update tests for every implementation change.
9. Update technical documentation when interfaces, data formats, dependencies, or behavior change.
10. Do not silently change approved naming, architecture, or data contracts.
11. Do not implement speculative features merely because directories or backlog entries exist.
12. Preserve compatibility with future Mythos titles whenever reasonable.
13. Treat player and NPC systems as shared systems whenever the approved design permits.
14. Optimize for maintainability and clarity before cleverness.
15. Clearly report incomplete work, assumptions, limitations, and technical debt.

## Architecture Priorities

Future implementation must prioritize:

- Modularity
- Maintainability
- Reusability
- Data-driven configuration
- Loose coupling
- Testability
- Scalability
- Save compatibility
- Clear extension points
- Setting independence in the shared framework
- Support for hybrid world simulation
- Support for autonomous NPC and world systems
- Minimal technical debt

Framework code should use abstract concepts rather than setting-specific assumptions. For example, prefer names such as `Organization`, `TransportAsset`, `RangedWeapon`, `CraftingService`, `WorkOrder`, and `Settlement` over names tied to a specific setting. Setting-specific implementations should be introduced through modules, data definitions, configuration, or content packages only when approved.

## Current Boundaries

The project is in pre-production. Do not implement gameplay systems until an approved technical specification is provided.

Do not:

- Implement combat.
- Implement NPC AI.
- Implement the economy.
- Implement quests.
- Implement crafting.
- Select a final engine or programming language.
- Add third-party dependencies.
- Create speculative schemas for unapproved systems.
- Invent lore, factions, kingdoms, classes, professions, items, or settings.
- Assume the first Mythos title has been selected.
- Commit generated binaries, caches, secrets, credentials, or machine-specific configuration.

## Documentation Standards

Use Markdown for project documentation unless another format is explicitly requested.

Every future system specification should eventually document:

- Purpose
- Scope
- Non-goals
- Dependencies
- Public interfaces
- Data model
- Data flow
- Simulation behavior
- Save requirements
- Extension points
- Error handling
- Performance considerations
- Test requirements
- Known limitations
- Acceptance criteria

Architecture Decision Records should eventually include:

- ADR number
- Title
- Status
- Context
- Decision
- Alternatives considered
- Consequences
- Related systems
- Date
- Superseded-by reference, when applicable
