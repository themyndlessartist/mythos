# Mythos Content Studio

Content Studio is the local-first, static authoring workspace specified by TOOL-001 and ADR-025. The MVP vertical slice supports NPC records, layered character and map previews, validation, draft recovery, and deterministic package assembly. It does not create runtime entities, save records, gameplay behavior, engine transforms, or Godot resources.

## Run locally

Requires Node.js 22 or newer and npm.

```sh
npm install
npm run dev
```

Open the URL printed by Vite (normally `http://localhost:5173`). All dependencies and the lockfile remain inside this directory.

Verification commands:

```sh
npm run format
npm run lint
npm run typecheck
npm test
npm run build
```

## Implementation decisions

- The UI is a React/TypeScript/Vite static application. Browser local storage is accessed through a replaceable draft adapter in the domain layer; no network request, telemetry, or remote asset loading is used.
- Authoring contracts are typed separately from UI preview state. In particular, character-layer hidden/locked state is a workspace preference and is not added to DATA-003 exports.
- IDs use DATA-001 lowercase namespaced syntax. Helpers create new authoring IDs during duplication; no model contains or derives a runtime Entity ID.
- Canonical JSON recursively sorts object keys, preserves contract-significant arrays, sorts DATA-001 inventory by kind, ID, then path, and emits two-space indentation plus a trailing line feed. This is a reversible Studio-local profile.
- SHA-256 is the Studio-local integrity algorithm. Export assembly completes and validates before a download is offered.
- Previews use browser raster composition and are labeled non-authoritative. DATA-004 transforms are dimensionless composition values and do not imply runtime or engine coordinates.

## MVP security and limits

Imports accept PNG, JPEG, and WebP only and check MIME type plus file signature. SVG, HTML, archives, remote URLs, absolute/escaping/control-character paths, reserved names, and media spoofs are rejected. Current reversible limits are 10 MiB per file, 4,096 pixels per side, 64 MiB decoded bytes per image, 200 files, and 250 MiB per package. Imported text is rendered only through React text nodes and form values, never injected as HTML.

Object URLs are preview-only and are not persisted or exported. Browser refresh therefore preserves authoring records and asset metadata but requires local image bytes to be reselected for previews. A future filesystem/IndexedDB adapter can replace that limitation without changing authoring contracts.

## Current slice limits

- Export currently provides deterministic manifest JSON assembly; a multi-file packaging container and Godot importer remain deferred.
- JSON record import, animations, option-catalog editing, image metadata stripping, cryptographic signing, dependency resolution, and true filesystem-atomic replacement are not part of this first vertical slice.
- DATA-003 requires preview-compatible layer dimensions because the approved alignment-box shape is not yet specified.
