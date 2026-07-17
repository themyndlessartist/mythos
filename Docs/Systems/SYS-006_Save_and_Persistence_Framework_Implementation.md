# SYS-006-IMPL-M-001 — Save and Persistence Framework Implementation Notes

- Document ID: SYS-006-IMPL-M-001
- Title: Save and Persistence Framework Implementation Notes
- Related Specification: [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)
- Prototype Milestone: [M-001 Foundation Prototype](../Milestones/M-001_Foundation_Prototype.md)
- Implementation Version: 0.1
- Status: In Progress
- Owner: Mythos Executive Development
- Last Updated: July 2026
- Applies Through Commit: This document's containing implementation commit
- Approval/Decision References: [ADR-024](../Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md), [STD-001](../Architecture/STD-001_Technical_Architecture_Standards.md)

## Implemented Scope

The M-001 proof coordinates versioned Entity, Time, Region, Character, and NPC snapshots behind `WorldPersistence`. A versioned manifest declares the complete partition set, domain versions, stable world ID, framework prototype version, and SHA-256 integrity digest for each partition. The Event Framework has no approved durable queue or history state in M-001, so no Event partition is invented.

Save validates cross-domain references, serializes canonical domain projections in fixed order, stages every partition through `ISaveWriteTransaction`, and exposes the replacement only after commit. Load verifies the manifest, required partitions, versions, and checksums before constructing an entirely fresh candidate in Entity, Time, Region, Character, and NPC dependency order. The candidate is returned only after final cross-domain validation; callers retain their prior world on every failure.

Failures use stable `persistence.*` codes for corrupt data, missing partitions, unsupported versions, malformed or null data, unresolved references, and storage/commit failures. Persistent IDs are restored exactly; the loader never generates replacements.

## Prototype-Local Decisions

These reversible M-001 mechanisms do not select the final save format or storage technology:

- Compact UTF-8 JSON is produced by `System.Text.Json` with explicit converters for framework value types and deterministic domain ordering.
- `System.Security.Cryptography.SHA256` provides partition corruption detection. It is integrity metadata, not security or authentication.
- `InMemorySaveStorage` proves a narrow transaction boundary by copying staged bytes into a slot only on successful commit.
- Manifest version 1 and domain partition names are milestone-local contracts. Migration execution, backup rotation, slot presentation metadata, and production file layout remain deferred.
- The caller supplies the approved calendar and externally owned Character/NPC definition providers during load; content/configuration persistence is outside this slice.

## Boundaries and Failure Behavior

The proof adds no UI, file-system adapter, cloud synchronization, compression, encryption, mods, database, gameplay, or third-party runtime dependency. It does not persist transient Event dispatch diagnostics. Invalid manifests, missing or extra required declarations, checksum mismatches, malformed snapshots, incompatible versions, invalid identifiers, and broken cross-domain links fail without exposing a partial candidate or overwriting a prior committed save.

## Known Limitations and Risks

- The adapter is memory-only and process-local; crash recovery, backups, and production atomic file replacement remain future work.
- JSON and the partition layout are inspectable prototype formats, not approved long-term compatibility contracts.
- The proof gathers domain snapshots synchronously and assumes the caller establishes a consistent snapshot boundary; concurrent simulation coordination is deferred.
- Migration metadata is represented through versions, but no older version exists and no speculative migration is implemented.
- Hashes detect accidental corruption but do not protect against maliciously rewritten data and matching manifests.

## Verification

Automated tests cover complete-world round trip, stable IDs and references, deterministic bytes, manifest incompatibility, checksum corruption, missing/partial partitions, null data, unresolved references, failed-commit preservation, load atomicity, and smoke integration. Repository verification uses `./Scripts/build.sh`, including Release build, all unit tests, smoke tests, and both Godot headless checks.
