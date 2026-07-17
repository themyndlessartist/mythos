# DATA-003 — Sprite/Animation Asset Manifest

- Document ID: DATA-003
- Title: Sprite/Animation Asset Manifest
- Version: 0.1
- Status: Draft for Approval
- Owner: Mythos Executive Development
- Last Updated: July 2026

## 1. Purpose and Boundary

Describe engine-neutral image layers, visual options, and ordered animation frames for Studio selection and preview. The manifest does not define art direction, final dimensions, renderer resources, world units, animation state machines, or gameplay state.

## 2. Proposed Record

UTF-8 JSON object with `document_kind` = `mythos.sprite-animation`, `schema_version` = `1.0`, stable `sprite_manifest_id`, `display_name`, `layers`, `options`, `animations`, and optional namespaced `extensions`.

### Layers

Each layer has a stable `layer_id`, `display_name`, integer `order`, `asset` cross-reference to a DATA-001 image entry, optional `option_condition`, and optional preview-only `offset` with finite `x` and `y` values. Ordering is ascending `order`, then `layer_id`; duplicate order values are permitted. Assets must use approved raster media types and share a preview-compatible canvas declared by their decoded image metadata. No fixed canvas dimensions are mandated.

### Visual options

Each option has stable `option_id`, `display_name`, one or more unique choices (`choice_id`, `display_name`), and an optional `default_choice_id`. Conditions compare one option to one declared choice; compound logic and procedural generation are deferred. IDs, not display text or array positions, carry identity.

### Animations

Each animation has stable `animation_id`, `display_name`, `loop` boolean, and a non-empty ordered `frames` array. A frame contains one or more layer asset references and a positive integer `duration_units`. Duration units are abstract relative preview ticks; real-time interpretation and engine timing are importer policy. Frame/layer assets may vary in dimensions only when the manifest explicitly declares a preview alignment box; final anchor and pivot semantics are deferred.

## 3. References and Validation

The manifest ID follows DATA-001 stable-ID rules. Child IDs are immutable within the manifest after first export. Validation rejects duplicate IDs, unresolved/wrong-kind assets, remote or escaping paths, unsupported media, invalid conditions/defaults, empty animations, non-positive durations, non-finite offsets, decoded-resource limit violations, and nondeterministic layer ordering.

Preview composes visible applicable layers in declared order using asset pixels and preview offsets. It must label this as an approximation and must not imply engine import fidelity.

## 4. Extension Points

Future revisions may add approved anchors, pivots, atlases, directional sets, blend modes, tint channels, collision-independent attachment points, and importer hints. Namespaced importer hints are advisory and cannot override validation or become authoritative gameplay data.

## 5. Acceptance Criteria

- DATA-002 selections can be validated entirely from the manifest and package inventory.
- Layer and frame ordering is deterministic.
- The Studio can preview permitted option combinations and animations without engine APIs.
- The contract imposes no art style, sprite size, direction count, or runtime animation architecture.

## 6. Deferred Decisions

Art style, dimensions, resolution policy, file formats beyond the MVP allowlist, anchor/pivot convention, atlas support, directional naming, tick duration, interpolation, blend modes, and Godot mapping are deferred.

## 7. Related Documents

- [DATA-001 Content Package Manifest](DATA-001_Content_Package_Manifest.md)
- [DATA-002 NPC Authoring Record](DATA-002_NPC_Authoring_Record.md)
- [TOOL-001 Mythos Content Studio MVP](../Tools/TOOL-001_Mythos_Content_Studio_MVP.md)
