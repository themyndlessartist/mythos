# ADR-025 — Content Studio MVP Technology Stack

ADR number: ADR-025

Title: Content Studio MVP Technology Stack

Version: 0.1

Status: Approved

Owner: Mythos Executive Development

Date: July 2026

## Context

TOOL-001 requires a local-first browser application with structured forms, layered previews, deterministic validation, import/export, and automated tests. The tool must remain separate from authoritative framework code, runtime state, and SYS-006 save data.

## Decision

The Content Studio MVP will use React with TypeScript and Vite under `Tools/ContentStudio`. Vitest may be used for tool-local unit and integration tests. Browser APIs will provide local file import, preview rendering, and export behind replaceable adapters.

Dependencies and lockfiles are confined to the tool directory and do not become Mythos framework runtime dependencies. Exact package versions are selected and locked when the scaffold is created. The Studio will remain a static, serverless application for the MVP.

This is a reversible TOOL-001 implementation choice. It does not select a technology for the Godot runtime, persistence framework, future hosted services, or other studio tools.

## Alternatives considered

- Plain browser JavaScript without a component framework
- A server-rendered web framework
- A Godot editor plug-in
- A native desktop application
- Delaying implementation until the game runtime is complete

## Consequences

- The Studio can be developed and tested independently from the game runtime.
- Typed authoring contracts can mirror DATA-001 through DATA-004 without becoming runtime schemas.
- Layered character and map previews can use browser rendering while remaining explicitly non-authoritative.
- Tool dependencies require their own security review, updates, and lockfile maintenance.
- A future desktop wrapper or hosted deployment may replace the delivery adapter without changing authoring contracts.

## Related systems

- [TOOL-001 Mythos Content Studio MVP](../../Tools/TOOL-001_Mythos_Content_Studio_MVP.md)
- [DATA-001 Content Package Manifest](../../Data/DATA-001_Content_Package_Manifest.md)
- [DATA-002 NPC Authoring Record](../../Data/DATA-002_NPC_Authoring_Record.md)
- [DATA-003 Sprite/Animation Asset Manifest](../../Data/DATA-003_Sprite_Animation_Asset_Manifest.md)
- [DATA-004 Layered Map Composition Manifest](../../Data/DATA-004_Layered_Map_Composition_Manifest.md)
- [STD-001 Technical Architecture Standards](../STD-001_Technical_Architecture_Standards.md)
