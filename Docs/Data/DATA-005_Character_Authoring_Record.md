# DATA-005 - Character Authoring Record

- Document ID: DATA-005
- Title: Character Authoring Record
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: August 2026

## 1. Purpose and Boundary

Define a minimal setting-independent authoring record for a named person who may be player-controlled, NPC-controlled, or used in another title-defined role.

This is design-time content. It is not a runtime Entity, Character snapshot, NPC autonomy profile, spawn instance, or save record. Control, instantiation, current location, schedules, goals, inventory, progression, and simulation state belong to later importer or runtime contracts.

DATA-005 prevents playable characters such as Khaige from being misrepresented as DATA-002 NPC records. It does not create separate world rules for players and NPCs.

## 2. Record

The UTF-8 JSON object contains:

| Member | Type | Contract |
|---|---|---|
| `document_kind` | string | Exactly `mythos.character-authoring` |
| `schema_version` | string | Initially `1.0` |
| `character_record_id` | string | Stable namespaced authoring identity |
| `display_name` | string | Non-empty author-facing name |
| `visual` | object | Optional DATA-003 sprite reference and selected visual options |
| `tags` | array | Optional sorted unique namespaced organizational tags |
| `notes` | string | Optional author-only plain text |
| `extensions` | object | Optional namespaced passive title/module data |

When present, `visual` has the same `sprite_manifest` reference and `options` selection shape as DATA-002. Omitting it means visual production is incomplete; it does not imply invisibility or a runtime rendering rule.

## 3. Identity and Roles

`character_record_id` follows DATA-001 identity rules and never becomes a live Entity ID. Display-name changes do not change identity.

The core record does not contain `is_player`, `is_npc`, or control state. A title package may identify an intended test role through a namespaced passive extension. Runtime control remains an importer or gameplay responsibility and may change without replacing the character definition.

## 4. Validation

Validation rejects unsupported versions, malformed IDs, blank display names, duplicate or unsorted tags, unsafe notes, unnamespaced extensions, unresolved or wrong-kind sprite references, and undeclared visual choices.

An omitted visual is valid. A supplied visual must resolve completely.

## 5. Package Integration

DATA-001 schema `1.1` adds the `character` entry kind. Character records use deterministic paths under `records/characters/`. DATA-001 `1.0` packages remain valid but cannot declare `character` entries.

## 6. Extension and Migration

Titles may add passive fields for biography, culture, origin, body profile, presentation, or intended test role only under namespaced extensions until dedicated contracts are approved. Extensions cannot introduce executable behavior or mutable world state.

## 7. Acceptance Criteria

- Khaige can be authored without being mislabeled as an NPC.
- The record remains neutral about runtime controller and simulation role.
- Optional visual references validate against DATA-003.
- Existing DATA-001 `1.0` and DATA-002 records remain compatible.
- Content Studio can preserve, validate, and deterministically export the record.

## 8. Deferred Decisions

Character creation, localization, pronouns, species/body schemas, portraits, biography fields, origin rules, player-control mapping, NPC linkage, runtime instantiation, and Entity-ID mapping remain deferred.

## 9. Related Documents

- [DATA-001 Content Package Manifest](DATA-001_Content_Package_Manifest.md)
- [DATA-002 NPC Authoring Record](DATA-002_NPC_Authoring_Record.md)
- [DATA-003 Sprite/Animation Asset Manifest](DATA-003_Sprite_Animation_Asset_Manifest.md)
- [SYS-005 Character Framework](../Systems/SYS-005_Character_Framework.md)

