# ADR-023 — Godot and C# Technology Stack

ADR number: ADR-023

Title: Godot and C# Technology Stack

Version: 0.1

Status: Approved

Owner: Mythos Executive Development

Date: July 2026

## Context

Mythos requires an implementation stack for its foundation prototype. The project needs cross-platform development on macOS and Windows, strong support for custom simulation systems and development tools, and a path from a foundation prototype to multiple commercial titles.

RPG Maker MZ was considered because it can accelerate production of conventional top-down RPG content. Its plugin-oriented architecture would require the Mythos simulation framework to work around assumptions designed for a narrower RPG structure. This creates unacceptable long-term coupling and migration risk for the approved living-world architecture.

Godot provides a general-purpose, cross-platform engine with source availability and a permissive license. C# provides static typing, mature tooling, automated testing support, and clear boundaries for a long-lived commercial codebase.

## Decision

Mythos will use the .NET edition of Godot with C# for the foundation prototype and planned framework implementation.

The initial baseline is Godot 4.7 stable. The compatible .NET SDK target will be validated when the project is scaffolded and recorded in build configuration.

Authoritative framework logic must remain separate from Godot scenes, nodes, rendering, input, animation, physics, and other presentation concerns. Godot integrations will use adapters around framework contracts. Engine-independent logic must remain testable without launching the Godot editor whenever practical.

GDScript may be used only when a future approved specification identifies a narrow engine-integration need. It is not the primary framework language.

## Alternatives considered

- RPG Maker MZ with JavaScript plugins
- Godot with GDScript as the primary language
- Unity with C#
- Unreal Engine with C++ or Blueprints
- A custom engine

## Consequences

- Development can proceed on both macOS and Windows from the same repository.
- The project requires the Godot .NET editor and a compatible .NET SDK.
- Framework assemblies and tests should minimize direct Godot dependencies.
- Engine adapters, scenes, assets, and presentation code must not own authoritative simulation state.
- Godot version upgrades require explicit compatibility review, tests, and documentation.
- Web export is not a prototype requirement; current Godot C# web-export limitations are accepted.
- Console support is not implied by this decision and requires separate future approval and platform planning.
- RPG Maker MZ remains available for disposable design experiments but is not an authoritative Mythos runtime.

## Related systems

All foundation frameworks, build tooling, automated tests, engine integration, and Foundation Prototype M-001
