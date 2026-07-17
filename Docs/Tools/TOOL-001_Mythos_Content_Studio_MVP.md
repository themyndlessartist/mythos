# TOOL-001 — Mythos Content Studio MVP

- Document ID: TOOL-001
- Title: Mythos Content Studio MVP
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## 1. Purpose

Define the first implementation-ready architecture for a local-first web application that authors, validates, previews, and exports setting-independent Mythos content packages for later import through a Godot adapter.

The Studio is an authoring tool. It does not run the Mythos simulation and does not define runtime or save schemas.

## 2. Scope

The MVP must:

- create, edit, duplicate, delete, import, and validate [DATA-002 NPC Authoring Records](../Data/DATA-002_NPC_Authoring_Record.md);
- select data-defined visual options and associate [DATA-003 Sprite/Animation Asset Manifests](../Data/DATA-003_Sprite_Animation_Asset_Manifest.md);
- preview layered character assets without defining runtime rendering behavior;
- import one map background, add/reorder/transform/hide/lock visual layers, and place generic region, spawn, and reference markers through [DATA-004 Layered Map Composition Manifests](../Data/DATA-004_Layered_Map_Composition_Manifest.md);
- validate local records and cross-references continuously and before export; and
- export a versioned, engine-neutral package described by [DATA-001 Content Package Manifest](../Data/DATA-001_Content_Package_Manifest.md).

## 3. Non-Goals

The MVP does not:

- implement gameplay, NPC autonomy, schedules, goals, professions, classes, combat, navigation, map traversal, region containment, spawn rules, or title lore;
- create runtime Entity, Character, NPC, Region, event, or save-state records;
- define final sprite dimensions, art style, map units, origin, axes, projection, scale, or world-coordinate conversion;
- provide a Godot importer, engine preview, collaborative server, cloud storage, source-control client, asset editor, image rasterizer, or executable plug-in system;
- guarantee that authored content is semantically valid for a future title module whose rules do not yet exist.

## 4. Architecture and Responsibilities

The local-first application has replaceable boundaries:

1. **Presentation:** browser forms, asset pickers, layer controls, preview canvases, validation messages, and export actions.
2. **Application services:** commands, undo/redo, draft lifecycle, import/export orchestration, and deterministic package assembly.
3. **Authoring model:** in-memory records conforming to DATA-001 through DATA-004.
4. **Validation service:** structural, reference, asset, security, and export-readiness checks returning stable diagnostic codes and field paths.
5. **Workspace adapter:** browser-supported local persistence and explicit filesystem import/export where available. The adapter is replaceable and must not leak storage handles into records.
6. **Preview adapters:** non-authoritative character and map composition renderers. Preview transforms never imply engine transforms.

The Studio owns drafts, authoring preferences, preview state, and export assembly. Each data contract owns its record validation. A future Godot importer owns translation from an accepted package into engine resources. Runtime frameworks remain authoritative for live world state, and SYS-006 remains authoritative for saves.

No network service is required. The application must function after its static application resources are available locally. Network access, telemetry, and remote asset loading are disabled by default and require a future approved capability.

## 5. Workspace and File Organization

Recommended logical workspace:

```text
workspace/
  workspace.json                 # tool-local preferences; never exported
  drafts/                        # recoverable tool-local working copies
  packages/<package-id>/
    package.json                 # DATA-001
    records/npcs/*.json          # DATA-002
    manifests/sprites/*.json     # DATA-003
    manifests/maps/*.json        # DATA-004
    assets/characters/**
    assets/maps/**
```

Export contains only `package.json`, declared records/manifests, and declared assets under package-relative paths. Temporary files, previews, browser metadata, absolute paths, credentials, and workspace preferences are excluded.

## 6. Editing and Data Flow

1. The user opens or creates a local workspace and package.
2. Imported files are copied into an isolated package asset area after security checks; source paths are not retained in export data.
3. Commands update drafts and trigger incremental validation.
4. Preview adapters resolve only validated package-local references and display non-authoritative compositions.
5. Export performs a full validation pass, creates a deterministic inventory, computes integrity metadata, and writes to a new destination.
6. Export failure leaves the prior export unchanged and returns structured diagnostics.

Undo/redo applies to authoring commands. Hidden and locked layer flags affect Studio editing/preview only unless a future importer explicitly maps them.

## 7. Validation and Diagnostics

Diagnostics contain `code`, `severity`, `document_id`, `path`, and a human-readable message. Codes are stable within a TOOL-001 contract version. Validation must detect:

- unsupported or malformed schema versions and document kinds;
- duplicate stable IDs, invalid ID syntax, and unresolved or wrong-kind cross-references;
- undeclared, missing, duplicate, case-colliding, absolute, or escaping paths;
- non-finite numbers and contract-specific bounds;
- unsupported media types, files exceeding configured limits, and inconsistent declared integrity metadata;
- export inventory mismatches and orphaned declared records.

Errors block export. Warnings do not block export unless a package policy promotes their code. The Studio must never silently repair IDs, references, paths, or unknown fields; explicit user-approved fix actions are allowed.

## 8. Uploaded-File Security

All imported files are untrusted. The MVP must:

- use an allowlist of supported image and JSON media types, verify file signatures where practical, and reject executable or active content including SVG, HTML, scripts, archives, and engine resources;
- enforce configurable per-file, pixel-dimension, decoded-memory, file-count, and package-size limits;
- decode images in an isolated browser capability, discard embedded metadata for generated previews where practical, and never execute embedded content;
- normalize package-relative paths, reject traversal, absolute paths, control characters, reserved names, symlinks, and case-folding collisions;
- avoid remote URL resolution and never interpolate imported text as HTML;
- calculate export integrity hashes over bytes and require a future importer to repeat validation rather than trust Studio output.

## 9. Performance, Reliability, and Testing

The MVP should keep ordinary edits responsive, move expensive decoding and validation off the primary UI path where browser facilities permit, release unused image resources, and surface configured limits before export.

Required automated tests include contract-valid and invalid fixtures, cross-reference graphs, deterministic export inventory/order, import/export round trips, path attacks, spoofed media, resource limits, atomic export failure, undo/redo, and preview composition ordering. Visual smoke tests supplement but do not replace contract tests.

## 10. Extension Points

Future approved work may add record kinds, marker kinds, visual option catalogs, validators, preview adapters, package profiles, localization, migrations, and engine importers. Extensions use namespaced identifiers and declared schema versions. Unknown extensions are preserved only when the receiving contract explicitly permits them; they are never executed.

## 11. Acceptance Criteria

TOOL-001 is implementation-ready when an MVP can:

1. complete every operation in Scope without a server;
2. reopen local drafts without changing stable authoring IDs;
3. show field-addressed validation and block invalid export;
4. safely import permitted assets and reject the prohibited cases in Section 8;
5. preview character and map layers in declared order without claiming runtime equivalence;
6. export the same validated workspace inputs as the same canonical inventory and content bytes, excluding export timestamps;
7. produce a package accepted by independent DATA-001 through DATA-004 validators; and
8. demonstrate through tests that authoring, runtime, and save representations remain separate.

## 12. Deferred Decisions

Deferred: web framework, browser storage technology, packaging container, canonical JSON profile, hash algorithm, maximum limits, visual option taxonomy, art style, sprite dimensions, map conventions, localization workflow, collaboration, source-control integration, Godot importer mapping, and all title-specific content choices.

JSON is the proposed interoperable authoring/export representation because it is inspectable and reversible. Under the reversible-choice governance established by [ADR-024](../Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md), this is a TOOL-001-local choice, not a final runtime or persistence decision.

## 13. Related Documents

- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [SYS-005 Character Framework](../Systems/SYS-005_Character_Framework.md)
- [SYS-006 Save and Persistence Framework](../Systems/SYS-006_Save_and_Persistence_Framework.md)
- [SYS-007 NPC Framework](../Systems/SYS-007_NPC_Framework.md)
