# Mythos

Mythos is a long-term, multi-title living-world RPG franchise built around a reusable shared framework. Future titles will share core architecture and systems while supporting distinct settings such as medieval fantasy, 1920s gangster era, modern day, futuristic or space-opera, post-apocalyptic, Wild West, and other settings.

This repository is currently in pre-production. It is being initialized for planning, documentation, tooling, tests, and future implementation work. Gameplay systems must not be implemented until approved technical specifications are supplied by the primary Mythos development process.

The shared framework should remain setting-agnostic whenever practical. Framework code should use abstract concepts such as `Organization`, `TransportAsset`, `RangedWeapon`, `CraftingService`, `WorkOrder`, and `Settlement` rather than assumptions tied to any single title or setting. Setting-specific behavior should be introduced through modules, data definitions, configuration, or content packages after approval.

## Repository Layout

- `Docs/Executive/`: project charter, framework overview, ADR log, open questions, and development roadmap.
- `Docs/Architecture/`: approved architecture documentation and future Architecture Decision Records.
- `Docs/Systems/`: approved technical specifications for shared and title-specific systems.
- `Docs/Gameplay/`: approved gameplay design specifications and implementation notes.
- `Docs/World/`: world simulation, setting, location, and content planning documentation.
- `Docs/Writing/`: narrative, dialogue, terminology, and writing production documentation.
- `Docs/Art/`: visual direction, asset standards, and art pipeline documentation.
- `Docs/Audio/`: audio direction, implementation notes, and pipeline documentation.
- `Docs/QA/`: test plans, quality standards, bug triage processes, and release criteria.
- `Docs/Tools/`: documentation for developer tools, pipelines, and editor workflows.
- `Docs/Templates/`: reusable documentation templates.
- `Source/`: future source code for core framework, systems, modules, integration, and debug support.
- `Data/`: future schemas, definitions, settings, and test data.
- `Assets/`: source assets and approved asset placeholders when applicable.
- `Tools/`: project tooling source and utilities.
- `Tests/`: automated unit, integration, simulation, and fixture assets.
- `Build/`: build scripts, manifests, and generated build support files that are appropriate for source control.
- `Scripts/`: repository maintenance, automation, and developer workflow scripts.

## Implementation Policy

All future implementation must be driven by approved documentation. Contributors and Codex sessions should read relevant approved documentation before modifying code, identify ambiguities before implementation, and update tests and technical documentation when behavior, interfaces, dependencies, or data formats change.
