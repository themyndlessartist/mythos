# M-003 - Lakewood Vertical Slice

- Document ID: M-003
- Version: 0.1
- Status: Complete
- Owner: Mythos Executive Development
- Last Updated: August 2026

## Purpose

Create the first title-specific playable proof for **Mythos: Genesis**. The slice begins in Lakewood with Khaige as the test player character and demonstrates that a settlement can gather resources, choose construction priorities, and change through shared player and NPC world systems.

## Approved Inputs

- First title: **Mythos: Genesis**.
- Starting area: **Lakewood**.
- Test player character: **Khaige**.
- Central slice: secure resources and develop practical buildings and homes.
- Visual direction: the approved grounded, restrained early-medieval frontier style.
- Supporting NPCs may be provisional, but every named NPC requires a purpose and independent activity.

Khaige is fixed for this test slice only. This milestone does not replace the franchise rule that future players begin as ordinary people and define their own path.

## Deliverables

1. A versioned `mythos.genesis` content package with provenance and validation.
2. An engine-neutral content-bundle importer with path, inventory, size, and SHA-256 integrity validation.
3. A title-authoring contract for Khaige that does not misuse the NPC authoring schema.
4. A compact Lakewood map composition with resource, construction, spawn, and reference markers.
5. Khaige visual definitions and a small provisional NPC population.
6. A Godot adapter that loads the accepted package without moving title rules into the shared framework.
7. One playable loop: acquire resources, commit them to one building, observe completion and settlement-state change, save, and reload.
8. Automated import, deterministic-content, simulation, and persistence tests.

## First Implementation Increment

The first increment establishes the milestone, creates a minimal package carrying the approved title identity, and implements the safe bundle-import boundary. It intentionally contains no invented Lakewood geography, production art, or gameplay rules.

## Completed Prototype

The completed slice includes a packaged Lakewood map, control-neutral Khaige authoring, four provisional purpose-driven NPC records, generated non-canon visuals, and the DATA-006 Storehouse project. The setting-specific `Mythos.Genesis` module applies the approved 20 timber, 10 stone, and 8 labor requirements without moving title rules into the shared framework.

Player commands and autonomous NPC assistance use the same contribution operations. Completion consumes the required inputs, displays the Storehouse on the selected construction site, applies the storage-expanded state, and persists through a versioned prototype save. A reset command supports repeatable testing.

## Non-Goals

- A complete town, region, economy, crafting system, construction sandbox, or final NPC AI.
- Final character creation, combat, magic, dialogue, navigation, or quest systems.
- Treating Google Drive or Notion drafts as canon without approval.
- Treating Content Studio IDs as runtime Entity IDs.
- Using placeholder visuals as approved production art.

## Acceptance Criteria

M-003 is complete when:

- Godot loads the Genesis package and creates a compact Lakewood test scene;
- Khaige and the required provisional NPCs are represented through approved authoring contracts;
- one building can progress from planned to complete using validated resources and labor;
- the same world rules permit NPC participation and world progress without Khaige;
- save/load preserves the demonstrated state;
- all content references and package bytes validate deterministically; and
- automated and headless Godot verification passes.

All criteria were verified in August 2026. A visual playtest completed the Storehouse, restarted Godot, and confirmed that the building and settlement state were restored.

## Deferred Production Inputs

- Lakewood geography and settlement handoff
- Khaige character and visual handoff
- minimum visual asset pack
- construction/resource authoring contracts
- representative NPC purposes and schedules

These inputs may arrive incrementally. Missing creative detail must use explicit test placeholders rather than accidental canon.

## Related Documents

- [M-002 Framework Alpha](M-002_Framework_Alpha.md)
- [DATA-001 Content Package Manifest](../Data/DATA-001_Content_Package_Manifest.md)
- [DATA-002 NPC Authoring Record](../Data/DATA-002_NPC_Authoring_Record.md)
- [DATA-004 Layered Map Composition Manifest](../Data/DATA-004_Layered_Map_Composition_Manifest.md)
- [TOOL-001 Mythos Content Studio MVP](../Tools/TOOL-001_Mythos_Content_Studio_MVP.md)
