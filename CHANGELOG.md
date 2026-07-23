# Changelog

All notable changes to Mythos will be documented in this file.

## Unreleased

### Added

- Content Studio MVP vertical slice under `Tools/ContentStudio`: local-first React/TypeScript/Vite authoring workspaces for NPC records, character assets, layered maps, and deterministic package readiness/export.
- DATA-001 through DATA-004 TypeScript models, structured validators, path/media security checks, canonical export helpers, replaceable draft persistence, undo/redo history, and contract tests.
- Draft TOOL-001 Mythos Content Studio MVP specification and DATA-001 through DATA-004 engine-neutral authoring/export contracts.
- Data-contract and tool-specification document indexes plus a parallel Content Studio roadmap track that does not displace M-001.
- Godot 4.7 .NET and C# foundation prototype scaffolding.
- Engine-independent Entity Framework prototype with stable IDs, lifecycle state, tags, component references, hierarchy, ownership, region assignment, and serializable snapshots.
- Deterministic Event Framework prototype with immutable envelopes, ordered subscriptions, filters, cancellation, failure isolation, recursion protection, and bounded diagnostics.
- Engine-independent Time Framework prototype with an authoritative clock, configurable calendars, rational time scaling, composable pause reasons, deterministic schedules and simulation layers, bounded catch-up, snapshots, and optional Event Framework publication.
- Engine-independent Region Framework prototype with Entity-backed Region identity, configurable hierarchy, adjacency metadata, validated assignment and transfer, simulation fidelity and ownership, deterministic queries, diagnostics, and atomic snapshot restore.
- Engine-independent Character Framework prototype with one-to-one active Character Entity profiles, minimal identity/status/life-stage references, deterministic queries, and atomic versioned snapshot restoration.
- Minimal NPC Framework fixture with validated Character and Region composition, deterministic active/abstract schedule catch-up, diagnostics, and versioned atomic snapshot restoration.
- Engine-independent M-001 persistence proof with versioned deterministic partitions, SHA-256 integrity metadata, transactional in-memory storage, dependency-ordered atomic restore, and complete neutral-world round trips.
- Automated Entity, Event, Time, Region, and Character unit tests plus cross-platform build verification scripts.
- Explicit approval and documentation of xUnit 3.2.0 as M-001 prototype test tooling.
- Approved SD-001 through SD-005 executive documents, ADR-001 through ADR-024, STD-001, SYS-001 through SYS-007, and M-001 milestone documentation.
- Established milestone-scoped implementation-note identity and prototype-decision governance through ADR-024.

### Changed

- Hardened M-001 persistence with ordinal-canonical metadata serialization, strict JSON and partition rejection, pre-deserialization byte limits, full-state round-trip verification, and staged-write atomicity coverage.
- Scoped NPC operational validation to the relevant Character profile and Region assignment while retaining single-pass global validation at explicit world-integrity boundaries.
- Hardened Entity snapshot restoration against undefined lifecycle values, uninitialized identifiers, null collections, invalid timestamp/state combinations, and malformed references using structured failures.
- Made Entity snapshot collections defensive read-only projections.
- Changed Event reference handling to reject referenced events by default unless an explicit validator is supplied, with consistent source, target, Region-category, missing, and terminal-state validation.
- Added malformed Entity snapshot, Event reference, correlation/causation, and mixed-batch tests.
- Corrected development documentation to describe the complete test pipeline and distinguish Event Framework availability from deferred Entity event publication.
- Made Time scheduler, simulation-layer, and clock restoration atomic with structured rejection of malformed and sequence-exhausted state.
- Hardened Entity tag/component mutation identifiers and Calendar/pause restoration validation.
- Added original request indices to Event dispatch results so mixed batches remain correlatable without changing priority dispatch order.
- Made Time snapshot collections defensive and read-only, and made Time Event bridge registration rollback atomic.

## [0.1.0] - 2026-07-11

### Added

- Initialized the Mythos repository documentation and source layout.
- Added repository guidance for future Codex implementation sessions.
- Added draft executive document placeholders awaiting approved content.
