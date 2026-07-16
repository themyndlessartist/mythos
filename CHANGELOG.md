# Changelog

All notable changes to Mythos will be documented in this file.

## Unreleased

### Added

- Godot 4.7 .NET and C# foundation prototype scaffolding.
- Engine-independent Entity Framework prototype with stable IDs, lifecycle state, tags, component references, hierarchy, ownership, region assignment, and serializable snapshots.
- Deterministic Event Framework prototype with immutable envelopes, ordered subscriptions, filters, cancellation, failure isolation, recursion protection, and bounded diagnostics.
- Engine-independent Time Framework prototype with an authoritative clock, configurable calendars, rational time scaling, composable pause reasons, deterministic schedules and simulation layers, bounded catch-up, snapshots, and optional Event Framework publication.
- Automated Entity, Event, and Time unit tests plus cross-platform build verification scripts.
- Explicit approval and documentation of xUnit 3.2.0 as M-001 prototype test tooling.

### Changed

- Hardened Entity snapshot restoration against undefined lifecycle values, uninitialized identifiers, null collections, invalid timestamp/state combinations, and malformed references using structured failures.
- Made Entity snapshot collections defensive read-only projections.
- Changed Event reference handling to reject referenced events by default unless an explicit validator is supplied, with consistent source, target, Region-category, missing, and terminal-state validation.
- Added malformed Entity snapshot, Event reference, correlation/causation, and mixed-batch tests.
- Corrected development documentation to describe the complete test pipeline and distinguish Event Framework availability from deferred Entity event publication.

## [0.1.0] - 2026-07-11

### Added

- Initialized the Mythos repository documentation and source layout.
- Added repository guidance for future Codex implementation sessions.
- Added draft executive document placeholders awaiting approved content.
