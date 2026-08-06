# TOOL-001 - Content Bundle Importer Implementation

- Document ID: TOOL-001-IMPORT-M-003
- Version: 0.1
- Status: Implemented
- Owner: Mythos Executive Development
- Last Updated: August 2026
- Milestone: [M-003 Lakewood Vertical Slice](../Milestones/M-003_Lakewood_Vertical_Slice.md)

## Implemented Scope

`Mythos.Content` provides an engine-neutral reader for the deterministic `mythos.content-bundle` produced by Content Studio. It validates the bundle before exposing any file bytes to Godot or another adapter.

The importer verifies:

- supported bundle kind and version;
- namespaced package identity;
- normalized relative paths and case-insensitive path uniqueness;
- configured file-count, per-file, and aggregate byte limits;
- base64 encoding, declared byte length, and SHA-256 integrity;
- a required `package.json` with supported DATA-001 identity;
- exact correspondence between manifest entries and bundled content; and
- matching media type, size, digest, and package ID metadata.

Structured `content.*` failures are returned without exposing a partial bundle. The importer does not create runtime Entities, trust external paths, execute content, fetch dependencies, or interpret title extensions.

## Genesis Bootstrap

The source manifest at `Data/TitlePackages/Genesis/Lakewood/package.json` carries only approved title bootstrap facts in the passive `mythos.genesis` extension:

- title package identity;
- Lakewood as the starting area;
- Khaige as the playable test character; and
- settlement growth as the vertical-slice focus.

It intentionally declares no content records or assets yet. `Scripts/build_content_bundle.mjs` wraps validated source-package bytes into the deterministic bundle consumed from the Godot project. Both macOS and Windows build scripts regenerate the bundle before compilation and verification.

## Godot Boundary

`PrototypeRoot` loads the bundled artifact through `Mythos.Content`. Import failure stops the headless run with a structured diagnostic. Success reports the accepted package ID. No title rules or content parsing were added to `Mythos.Framework`.

## Verification

- Four importer tests cover valid import, path traversal, case collisions, tampering, inventory mismatch, and configured limits.
- The complete C# suite passes with 227 tests.
- Framework smoke verification passes.
- Godot headless editor import and runtime startup pass with `mythos-genesis.lakewood`.
- Content Studio passes 27 tests, TypeScript checking, and lint.

## Remaining M-003 Work

- Approve a shared Character authoring contract for Khaige rather than misuse DATA-002.
- Add Lakewood map, resources, construction sites, and provisional NPC records.
- Add approved raster assets and sprite/map manifests.
- Translate accepted authoring records into runtime Entity, Region, Character, and NPC state.
- Implement and persist the first resource-to-building progression loop.
