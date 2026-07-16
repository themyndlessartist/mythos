# Mythos

Mythos is a long-term, multi-title living-world RPG franchise built around a reusable shared framework. Future titles will share core architecture and systems while supporting distinct settings such as medieval fantasy, 1920s gangster era, modern day, futuristic or space-opera, post-apocalyptic, Wild West, and other settings.

This repository is currently in pre-production. It is being initialized for planning, documentation, tooling, tests, and future implementation work. Gameplay systems must not be implemented until approved technical specifications are supplied by the primary Mythos development process.

The shared framework should remain setting-agnostic whenever practical. Framework code should use abstract concepts such as `Organization`, `TransportAsset`, `RangedWeapon`, `CraftingService`, `WorkOrder`, and `Settlement` rather than assumptions tied to any single title or setting. Setting-specific behavior should be introduced through modules, data definitions, configuration, or content packages after approval.

## Repository Layout

- `Docs/Executive/`: project charter, framework overview, ADR log, open questions, and development roadmap.
- `Docs/Architecture/`: approved architecture documentation and future Architecture Decision Records.
- `Docs/Systems/`: approved technical specifications and milestone-scoped implementation notes for shared and title-specific systems.
- `Docs/Milestones/`: approved milestone scope, deliverables, acceptance criteria, and status documentation.
- `Docs/Gameplay/`: approved gameplay design specifications and implementation notes.
- `Docs/World/`: world simulation, setting, location, and content planning documentation.
- `Docs/Writing/`: narrative, dialogue, terminology, and writing production documentation.
- `Docs/Art/`: visual direction, asset standards, and art pipeline documentation.
- `Docs/Audio/`: audio direction, implementation notes, and pipeline documentation.
- `Docs/QA/`: test plans, quality standards, bug triage processes, and release criteria.
- `Docs/Tools/`: documentation for developer tools, pipelines, and editor workflows.
- `Docs/Templates/`: reusable documentation templates.
- `Source/`: active source code for the core framework, systems, modules, integration, and debug support.
- `Data/`: future schemas, definitions, settings, and test data.
- `Assets/`: source assets and approved asset placeholders when applicable.
- `Tools/`: project tooling source and utilities.
- `Tests/`: automated unit, integration, simulation, and fixture assets.
- `Build/`: build scripts, manifests, and generated build support files that are appropriate for source control.
- `Scripts/`: repository maintenance, automation, and developer workflow scripts.

## Implementation Policy

All future implementation must be driven by approved documentation. Contributors and Codex sessions should read relevant approved documentation before modifying code, identify ambiguities before implementation, and update tests and technical documentation when behavior, interfaces, dependencies, or data formats change.

## Development Toolchain

The approved foundation prototype uses:

- Godot 4.7 .NET edition
- C# targeting .NET 10
- The .NET 10 SDK
- xUnit 3.2.0 for prototype unit-test tooling

xUnit is explicitly approved for M-001 test tooling by [ADR-024](Docs/Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md). It is not a runtime dependency and does not select a permanent testing stack for later milestones.

Godot integration is located in `Source/Integration/Godot`. Authoritative framework code belongs in `Source/Core` and must remain independent of Godot APIs whenever practical.

Run the complete macOS build and smoke verification with:

```bash
./Scripts/build.sh
```

On Windows PowerShell, set `GODOT_BIN` when the Godot executable is not available under the default script name, then run:

```powershell
./Scripts/build.ps1
```

The scripts build the solution in Release mode, run the xUnit unit-test executable, run the dependency-free framework smoke test, import the Godot project headlessly, and execute the entry scene headlessly.
