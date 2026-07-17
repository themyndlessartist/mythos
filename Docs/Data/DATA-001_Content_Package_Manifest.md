# DATA-001 — Content Package Manifest

- Document ID: DATA-001
- Title: Content Package Manifest
- Version: 0.1
- Status: Draft for Approval
- Owner: Mythos Executive Development
- Last Updated: July 2026

## 1. Purpose and Boundary

Define the engine-neutral manifest for one exported content package. A package is an immutable set of authoring records and assets intended for validation and later importer translation.

It is not a save manifest, runtime registry, mod executable, or engine resource bundle. Package IDs and record IDs are content identities; they are not SYS-001 Entity IDs. Loading a package must not create or mutate a saved world without an approved importer and runtime workflow.

## 2. Proposed Representation

`package.json` is UTF-8 JSON with a top-level object. JSON is a reversible TOOL-001 interchange choice under ADR-024 and does not resolve SYS-006 serialization decisions. Required members:

| Member | Type | Contract |
|---|---|---|
| `document_kind` | string | Exactly `mythos.content-package` |
| `schema_version` | string | DATA-001 major/minor version, initially `1.0` |
| `package_id` | string | Stable namespaced authoring ID |
| `package_version` | string | Three non-negative integers `major.minor.patch` |
| `display_name` | string | Non-empty author-facing label; not identity |
| `entries` | array | Declared record manifests and assets |
| `dependencies` | array | Required package ID plus compatible version range; empty in a standalone MVP package |
| `extensions` | object | Optional namespaced passive data |

Each `entries` item contains `kind` (`npc`, `sprite-animation`, `layered-map`, or `asset`), stable `id`, unique package-relative `path`, `media_type`, byte `size`, and `integrity` (`algorithm`, `digest`). Record IDs must match the ID inside the referenced document. Entries are ordered by `kind`, then ID, then path for deterministic export.

## 3. Stable IDs and Versions

Authoring IDs use lowercase namespaced segments separated by dots; each segment begins with a letter and contains letters, digits, or hyphens. Example shape: `studio-neutral.sample-record` (illustrative only). IDs are immutable after first export. Renaming uses `display_name`; replacing identity creates a new record and requires explicit reference updates.

`schema_version` versions this contract. An unsupported major version is incompatible; a newer minor version may be accepted only if all added members are defined as optional and unknown extensions can be safely preserved. `package_version` versions content and follows author-controlled compatibility policy: major for breaking identity/meaning changes, minor for backward-compatible additions, patch for compatible corrections.

## 4. References and Validation

All paths are normalized, relative, forward-slash paths inside the package and must resolve to exactly one entry. Cross-references use `{ "package_id": "...", "record_id": "..." }`; same-package references still state the package ID. Dependencies must be acyclic for MVP validation.

Validation rejects duplicate/case-colliding IDs or paths, missing files, undeclared files other than explicitly permitted container metadata, kind mismatches, broken references, unsupported versions/media, invalid dependency ranges, integrity mismatch, traversal, and content exceeding configured limits. Validation never fetches dependencies or remote URLs.

## 5. Organization and Import

The package root follows TOOL-001’s export layout. A future importer must validate the complete manifest and bytes before translation, resolve dependencies explicitly, map authoring IDs to its own import/runtime identities, and preserve provenance. It must not treat filenames, display names, array positions, or authoring IDs as live Entity IDs.

## 6. Extension and Migration

New entry kinds or fields require a DATA-001 revision or namespaced passive extension. Extensions cannot contain executable code or relax core validation. Schema migrations are explicit, ordered, non-destructive transformations that preserve the original package and report dropped or changed data.

## 7. Acceptance Criteria

- A package inventory is complete, deterministic, integrity-verifiable, and path-safe.
- Every DATA-002 through DATA-004 record and referenced asset is declared exactly once.
- Independent validators reach the same reference and version result.
- Runtime state and save data cannot be mistaken for accepted package entries.

## 8. Deferred Decisions

Packaging container, canonical JSON rules, integrity algorithm, signing, dependency range syntax, distribution, compression, import caching, and Godot resource mapping are deferred.

## 9. Related Documents

- [TOOL-001 Mythos Content Studio MVP](../Tools/TOOL-001_Mythos_Content_Studio_MVP.md)
- [SYS-001 Entity Framework](../Systems/SYS-001_Entity_Framework.md)
- [SYS-006 Save and Persistence Framework](../Systems/SYS-006_Save_and_Persistence_Framework.md)
