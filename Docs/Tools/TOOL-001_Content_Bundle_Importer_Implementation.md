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

The DATA-005 reader independently locates declared `character` entries and validates identity, schema version, display text, tags, notes, passive extension namespaces, and optional visual-reference shape. It returns immutable authoring data and deliberately does not assign a controller, Entity ID, life stage, status, or current world state.

## Genesis Bootstrap

The source manifest at `Data/TitlePackages/Genesis/Lakewood/package.json` carries only approved title bootstrap facts in the passive `mythos.genesis` extension:

- title package identity;
- Lakewood as the starting area;
- Khaige as the playable test character; and
- settlement growth as the vertical-slice focus.

It now includes Khaige's control-neutral DATA-005 Character authoring record. Background, profession, and skills remain unassigned; the selected visual points only to an explicitly non-canon prototype manifest. The package also contains the Lakewood map composition, provisional NPC records, and DATA-006 Storehouse project. `Scripts/build_content_bundle.mjs` wraps validated source-package bytes into the deterministic bundle consumed from the Godot project. Both macOS and Windows build scripts regenerate the bundle before compilation and verification.

## Godot Boundary

`PrototypeRoot` loads the bundled artifact through `Mythos.Content`, then locates and validates Khaige's Character record. Import failure stops the headless run with a structured diagnostic. Success reports the accepted package and character names. No title rules or content parsing were added to `Mythos.Framework`.

The Genesis Godot adapter creates a runtime Character Entity from the accepted record. Required status and life-stage references that have not yet been approved use `mythos-test.*` identifiers local to the adapter; these are explicit prototype placeholders, not exported content or canon. `Mythos.Genesis` owns the prototype Storehouse definition and shared contribution state, while Godot owns display, controls, image decoding, and the local prototype save file.

## Verification

- Importer tests cover valid schema 1.0 through 1.2 packages, versioned entry kinds, path traversal, case collisions, tampering, inventory mismatch, and configured limits.
- The complete C# suite passes with 234 tests.
- Framework smoke verification passes.
- Godot headless editor import and runtime startup pass with `mythos-genesis.lakewood`.
- Content Studio passes 30 tests, TypeScript checking, lint, and formatting verification.

## Deferred Production Work

- Promote provisional NPC presentation into full shared-framework NPC runtime profiles when canonical schedules and character references are approved.
- Replace generated prototype artwork as final art direction delivers canonical assets.
