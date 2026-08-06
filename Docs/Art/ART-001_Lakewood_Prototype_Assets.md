# ART-001 - Lakewood Prototype Assets

- Document ID: ART-001
- Version: 0.1
- Status: Non-Canon Prototype
- Owner: Mythos Art Direction
- Last Updated: August 2026

## Purpose

Record the temporary original raster assets created to make M-003 playable. These files establish neither final character appearance nor production art standards.

## Assets

| Asset | Purpose | Status |
|---|---|---|
| `assets/maps/lakewood-background-prototype-v1.png` | Compact Lakewood playable background | Non-canon prototype |
| `assets/characters/khaige-prototype-v1.png` | Temporary Khaige map figure | Non-canon appearance |
| `assets/characters/lakewood-worker-prototype-v1.png` | Shared provisional NPC figure | Non-canon prototype |
| `assets/buildings/storehouse-complete-prototype-v1.png` | Completed Storehouse overlay | Non-canon prototype |

All assets were generated as original bitmap artwork using the built-in image generation workflow. Character and building sources used a flat magenta background followed by local chroma-key removal. Final alpha files were checked over a neutral background before package inclusion.

## Direction

The prompts used the approved grounded, restrained early-medieval frontier direction: practical materials, muted natural color, overcast daylight, readable top-down silhouettes, and no ornate fantasy or modern elements.

## Replacement Rules

- Preserve stable DATA-001 asset IDs when replacing artwork compatibly.
- Update byte size and SHA-256 integrity metadata after replacement.
- Khaige's generated appearance is not canonical.
- The shared worker image does not define the final appearance or identity of any provisional NPC.
- Runtime layout and gameplay must not rely on details unique to these images.

## Related Documents

- [M-003 Lakewood Vertical Slice](../Milestones/M-003_Lakewood_Vertical_Slice.md)
- [DATA-003 Sprite/Animation Asset Manifest](../Data/DATA-003_Sprite_Animation_Asset_Manifest.md)
- [DATA-004 Layered Map Composition Manifest](../Data/DATA-004_Layered_Map_Composition_Manifest.md)
