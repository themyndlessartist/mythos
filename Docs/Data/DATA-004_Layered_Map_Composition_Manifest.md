# DATA-004 — Layered Map Composition Manifest

- Document ID: DATA-004
- Title: Layered Map Composition Manifest
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## 1. Purpose and Boundary

Describe a Studio map composition made from one background, ordered visual layers, and generic authoring markers. It is a visual/content-layout document, not a Region Framework boundary, navigation map, spawn system, physics scene, runtime transform, or save-state partition.

## 2. Proposed Record

UTF-8 JSON object with `document_kind` = `mythos.layered-map`, `schema_version` = `1.0`, stable `map_manifest_id`, `display_name`, `background_asset`, `layers`, `markers`, and optional namespaced `extensions`.

### Coordinate space

All transforms and markers use finite values in a manifest-local, dimensionless 2D composition space. The background’s top-left is `(0, 0)` and its decoded pixel width/height define the preview extent for MVP editing only. This is a reversible Studio convention and explicitly does not select Mythos world coordinates, Region boundaries, map scale, axes, projection, units, or Godot coordinates. A future importer must use an approved mapping rather than assume equivalence.

### Visual layers

`background_asset` and every layer `asset` cross-reference DATA-001 raster entries. Each layer has immutable `layer_id`, `display_name`, integer `order`, `visible`, `locked`, and a `transform` containing finite `position` (`x`, `y`), `scale` (`x`, `y`, both non-zero), preview-only `rotation_degrees`, and optional `opacity` in `[0,1]`. Degrees are a reversible Studio preview representation, not a world-map or runtime convention; importer mapping remains deferred.

`visible` controls Studio preview/export intent; `locked` is editor-only protection. Neither is runtime state. Ordering is ascending `order`, then `layer_id`; reorder operations update `order` deterministically.

### Markers

Each marker has immutable `marker_id`, `kind` (`region`, `spawn`, or `reference`), `display_name`, finite composition-space `position`, optional plain-text `notes`, and optional `target` DATA-001 cross-reference.

- `region` is a reference point for future region association; it does not define containment, hierarchy, boundary, ownership, or a Region Entity.
- `spawn` is an authoring point for a future approved instantiation contract; it does not spawn anything or select an Entity ID.
- `reference` is a generic visual annotation or link.

Marker semantics beyond these statements require a future contract or namespaced extension.

## 3. Validation and Security

IDs follow DATA-001 rules and are unique in their scope. Validation rejects missing background, unresolved/wrong-kind assets or targets, duplicate IDs, unsupported media, escaping paths, non-finite values, zero scale, opacity outside range, unsupported marker kinds, unsafe text, and decoded-resource limits. Hidden/locked layers and off-background positions are valid but produce explicit warnings where they could surprise an author.

## 4. Extension Points

Future approved revisions may add geometric region annotations, grids, tiles, groups, parallax hints, importer coordinate profiles, navigation references, or additional marker kinds. They must not retroactively assign runtime meaning to MVP transforms or markers.

## 5. Acceptance Criteria

- The Studio can import a background, add/reorder/transform/hide/lock layers, and add/move/delete all three marker kinds.
- Save/reopen and export preserve stable child IDs and deterministic order.
- Independent validation resolves every declared asset and target.
- The document explicitly remains independent of Region, navigation, spawn, engine-scene, and save schemas.

## 6. Deferred Decisions

World coordinate system, runtime transform and angle conventions, origin/axes, projection, scale, map dimensions, region geometry, tile/grid model, runtime layer behavior, spawn semantics, navigation, streaming, and Godot mapping are deferred.

## 7. Related Documents

- [DATA-001 Content Package Manifest](DATA-001_Content_Package_Manifest.md)
- [TOOL-001 Mythos Content Studio MVP](../Tools/TOOL-001_Mythos_Content_Studio_MVP.md)
- [SYS-004 Region Framework](../Systems/SYS-004_Region_Framework.md)
