# DATA-002 — NPC Authoring Record

- Document ID: DATA-002
- Title: NPC Authoring Record
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## 1. Purpose and Boundary

Define the minimal setting-independent record used to author a named NPC content definition and its visual associations.

This record is design-time input, not a SYS-001 Entity Record, SYS-005 Character Profile, SYS-007 autonomy state, spawned instance, or SYS-006 save snapshot. It contains no current region, runtime schedule progress, goals, intentions, timestamps, random state, or other mutable world state. A future importer may use one authoring record to create zero, one, or many runtime instances according to separately approved rules.

## 2. Proposed Record

UTF-8 JSON object:

| Member | Type | Required contract |
|---|---|---|
| `document_kind` | string | Exactly `mythos.npc-authoring` |
| `schema_version` | string | Initially `1.0` |
| `npc_record_id` | string | Stable namespaced authoring ID |
| `display_name` | string | Non-empty author-facing identity text; not globally unique |
| `visual` | object | `sprite_manifest` cross-reference plus selected option values |
| `tags` | array | Optional sorted unique namespaced authoring tags with no implied gameplay behavior |
| `notes` | string | Optional author-only plain text; importer may discard |
| `extensions` | object | Optional namespaced passive title/module data |

`visual.sprite_manifest` uses the DATA-001 cross-reference form and must target DATA-003. `visual.options` is an object keyed by option IDs declared by that manifest; values must be among the option’s declared choices. The record does not embed image paths.

## 3. Identity, References, and Validation

`npc_record_id` follows DATA-001 ID rules, is unique within its package/dependency graph, and remains stable across edits and exports. It must never be copied into a live Entity ID field. Display-name edits do not change identity.

Validation rejects unsupported versions, duplicate IDs, blank display names, malformed tags, unknown or unavailable visual options, wrong-kind or unresolved manifest references, unsafe markup in notes, unknown core members, and extension keys without a namespace. A missing optional visual selection uses the DATA-003 declared default; if no default exists, selection is required.

## 4. Ownership and Extension

DATA-002 owns only authoring identity, author-visible label, optional organization metadata, and visual selection. Character traits, skills, professions, classes, body/species rules, NPC purpose, schedules, goals, ambitions, behavior, combat, inventory, relationships, and lore require future approved data contracts or namespaced extensions. The Studio may preserve such extension data but cannot interpret it without an approved module.

## 5. Acceptance Criteria

- The Studio can create, edit, validate, duplicate with a new ID, and export the record.
- Visual references resolve deterministically and all selections validate against DATA-003.
- No field can be confused with runtime autonomy state or a save snapshot.
- The core record remains usable without title-specific concepts.

## 6. Deferred Decisions

Naming model, localization, body/species model, pronouns, portrait rules, visual-option taxonomy, gameplay definition contracts, runtime instantiation, and Entity-ID mapping are deferred.

## 7. Related Documents

- [DATA-001 Content Package Manifest](DATA-001_Content_Package_Manifest.md)
- [DATA-003 Sprite/Animation Asset Manifest](DATA-003_Sprite_Animation_Asset_Manifest.md)
- [SYS-005 Character Framework](../Systems/SYS-005_Character_Framework.md)
- [SYS-007 NPC Framework](../Systems/SYS-007_NPC_Framework.md)
