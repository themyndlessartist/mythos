# DATA-006 - Settlement Project Authoring Record

- Document ID: DATA-006
- Title: Settlement Project Authoring Record
- Version: 0.1
- Status: Approved for M-003 Prototype
- Owner: Mythos Executive Development
- Last Updated: August 2026

## Purpose and Boundary

Define the minimal engine-neutral authoring data for one settlement construction project. This contract exists to prove the approved Lakewood resource-to-building loop without selecting a final construction, economy, crafting, or settlement-management architecture.

It is design-time content, not current stockpile state, construction progress, an Entity, a Property record, a save snapshot, or a final gameplay balance contract.

## Record

The UTF-8 JSON object contains:

| Member | Contract |
|---|---|
| `document_kind` | Exactly `mythos.settlement-project-authoring` |
| `schema_version` | Initially `1.0` |
| `project_record_id` | Stable namespaced authoring identity |
| `display_name` | Non-empty author-facing name |
| `site_marker_id` | DATA-004 marker identity used by the title adapter |
| `resource_requirements` | Non-empty sorted unique resource IDs with positive integer amounts |
| `labor_required` | Positive integer prototype labor requirement |
| `completion_asset` | DATA-001 raster asset reference |
| `completion_state_id` | Namespaced result identifier interpreted by the title adapter |
| `notes` | Optional safe author-only plain text |
| `extensions` | Optional namespaced passive title data |

Resource IDs and completion-state IDs are content identities, not runtime Entity IDs. Labor is an abstract prototype unit and does not define jobs, time, stamina, wages, or NPC AI.

## Package Integration

DATA-001 schema `1.2` adds the `settlement-project` entry kind. Records use deterministic paths under `records/settlement-projects/`. Earlier package versions remain valid but cannot declare this kind.

## Validation

Validation rejects malformed identity, missing or duplicate resources, non-positive amounts, invalid site identity, unresolved or wrong-kind completion assets, invalid completion state, unsafe notes, and unnamespaced extensions.

## M-003 Approved Instance

The Lakewood Storehouse prototype requires 20 timber, 10 stone, and 8 labor. Completion produces the `mythos-genesis.storage-expanded` title state. These values are explicitly prototype balance and may be replaced without changing the shared framework.

## Deferred Decisions

Resource definitions, gathering rates, inventories, labor simulation, construction phases, cancellation, refunds, ownership, storage quantities, workforce scheduling, economy integration, NPC planning, and final settlement effects remain deferred.

## Related Documents

- [DATA-001 Content Package Manifest](DATA-001_Content_Package_Manifest.md)
- [DATA-004 Layered Map Composition Manifest](DATA-004_Layered_Map_Composition_Manifest.md)
- [M-003 Lakewood Vertical Slice](../Milestones/M-003_Lakewood_Vertical_Slice.md)
