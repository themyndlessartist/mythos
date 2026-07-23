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

- The UI is a React/TypeScript/Vite static application over the approved `AuthoringWorkspace` and domain services. Versioned draft metadata uses a replaceable local-storage adapter; raster bytes use a replaceable IndexedDB adapter. Storage failures and malformed/unsupported drafts are surfaced without accepting partial state.
- Authoring contracts are typed separately from UI preview state. In particular, character-layer hidden/locked state is a workspace preference and is not added to DATA-003 exports.
- IDs use DATA-001 lowercase namespaced syntax. Helpers create new authoring IDs during duplication; no model contains or derives a runtime Entity ID.
- Canonical JSON recursively sorts object keys with a locale-independent ordinal comparator, preserves contract-significant arrays, sorts DATA-001 inventory by kind, ID, then path, and emits two-space indentation plus a trailing line feed. This is a reversible Studio-local profile.
- SHA-256 is the Studio-local integrity algorithm. Export assembly calculates media type, byte size, and digest from actual content bytes, enforces exact inventory/content correspondence, and completes in memory before a download is offered.
- The dependency-free download is explicitly `application/vnd.mythos.content-bundle+json`: a versioned bundle envelope containing `package.json`, every DATA-002 through DATA-004 document, and every raster as base64 content. It is a reversible container representation, not a DATA-001 manifest presented as a complete package.
- Previews use browser raster composition and are labeled non-authoritative. DATA-004 transforms are dimensionless composition values and do not imply runtime or engine coordinates.

## MVP security and limits

Imports accept PNG, JPEG, and WebP only and check MIME type plus file signature. SVG, HTML, archives, remote URLs, absolute/escaping/control-character paths, reserved names, duplicate paths, case-insensitive collisions, and media spoofs are rejected. Current reversible limits are 10 MiB per file, 8,192 pixels per side, 64 MiB decoded bytes per image, 250 files, and 250 MiB per package. File count and aggregate size apply to the complete proposed batch; a failure accepts no files. Imported text is rendered only through React text nodes and form values, never injected as HTML.

Object URLs are preview-only and are rebuilt from IndexedDB after reopening a draft. Effects revoke them on asset replacement/deletion, draft changes, decode errors, and unmount. Stable asset IDs and original bytes survive close/reopen.

## Current slice limits

- A compressed/archive container and Godot importer remain deferred; the documented dependency-free bundle envelope is the complete MVP delivery representation.
- JSON record import, animation and option-catalog editing, image metadata stripping, cryptographic signing, and dependency resolution are not part of this vertical slice.
- DATA-003 requires preview-compatible layer dimensions because the approved alignment-box shape is not yet specified.
