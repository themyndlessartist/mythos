# SD-005 — Development Roadmap

Version: 0.1

Status: Approved

Owner: Mythos Executive Development

Last Updated: July 2026

--------------------------------------------

# 1. Purpose

This document records the current executive roadmap for Mythos framework planning and development.

The roadmap does not select the first Mythos title setting.

--------------------------------------------

# 2. Phase 0 — Vision

Status: Complete

--------------------------------------------

# 3. Phase 1 — Executive Architecture

Status: Complete

--------------------------------------------

# 4. Phase 2 — Foundation Specifications

Status: Complete

Planned order:

1. SYS-001 Entity Framework — Specified
2. SYS-002 Event Framework — Specified
3. SYS-003 Time Framework — Specified
4. SYS-004 Region Framework — Specified
5. SYS-005 Character Framework — Specified
6. SYS-006 Save and Persistence Framework — Specified
7. SYS-007 NPC Framework — Specified

--------------------------------------------

# 5. Phase 3 — World Simulation Specifications

Status: Complete

Phase 3 may proceed after or alongside completion of the Phase 4 Foundation Prototype when dependencies permit.

- SYS-008 Relationship Framework — Implemented
- SYS-011 Reputation Framework — Implemented
- SYS-012 Property Framework — Implemented
- SYS-014 Economy Framework — Implemented
- SYS-013 Organization Framework — Implemented
- SYS-009 Information and Knowledge Framework — Implemented
- SYS-010 World History Framework — Implemented
- SYS-015 Dynamic World Event Framework — Implemented

--------------------------------------------

# 6. Phase 4 — Foundation Prototype

Status: Complete

Milestone: [M-001 Foundation Prototype](../Milestones/M-001_Foundation_Prototype.md)

- Minimal entity model
- Event processing
- Time progression
- One simulated region
- Basic NPC schedules
- Save/load proof of concept

--------------------------------------------

# 7. Phase 5 — Framework Alpha

Status: Complete

Milestone: [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)

Completion Report: [M-002 Framework Alpha Report](../Milestones/M-002_Framework_Alpha_Report.md)

- Reusable world-simulation domains implemented
- Domain diagnostics and canonical verification integrated
- Modular title-package validation deferred to Phase 6 because no title package is approved
- Representative performance testing deferred to Phase 6 title workloads

--------------------------------------------

# 8. Parallel Tooling Track — Content Studio

Status: MVP Implemented and Verified

This track proceeds alongside framework milestones without changing their scope, priority, or exit conditions.

- [TOOL-001 Mythos Content Studio MVP](../Tools/TOOL-001_Mythos_Content_Studio_MVP.md)
- DATA-001 through DATA-004 authoring/export contracts
- Local-first authoring and validation
- Engine-neutral content-package export for a later Godot importer
- React, TypeScript, and Vite MVP implementation under `Tools/ContentStudio`

--------------------------------------------

# 9. Phase 6 — First Mythos Title Pre-Production

Status: Planned

- Select first setting
- Define title-specific scope
- Build content pipeline
- Produce vertical slice
