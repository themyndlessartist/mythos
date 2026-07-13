# SD-002 — Mythos Framework Overview

Version: 0.1

Status: Approved

Owner: Mythos Executive Development

Last Updated: July 2026

--------------------------------------------

# 1. Purpose

This document defines the executive-level overview for the Mythos Framework.

The Mythos Framework is the reusable foundation for multiple standalone Mythos titles. It must support different settings while preserving shared architecture, shared development workflows, and setting-independent core systems whenever practical.

Implementation architecture such as ECS, OOP, data-oriented design, or a hybrid approach remains undecided.

--------------------------------------------

# 2. Framework Layers

The Mythos Framework is organized into four conceptual layers.

## Foundation

The Foundation layer contains the lowest-level shared concepts required by all Mythos titles.

It includes identity, event processing, time, regions, persistence boundaries, and other cross-cutting systems that other layers depend on.

## Simulation

The Simulation layer governs world behavior that can continue independently of the player.

It includes regional simulation, NPC activity, relationships, economy, organizations, world events, world history, and other persistent state changes.

## Gameplay

The Gameplay layer exposes player-facing and NPC-facing systems built on top of the foundation and simulation layers.

Gameplay systems must follow approved specifications and should avoid setting-specific assumptions in shared framework code.

## Content

The Content layer contains title-specific data, assets, rules, modules, locations, characters, writing, and setting-specific implementations.

Content packages adapt the shared framework for individual Mythos titles.

--------------------------------------------

# 3. Architectural Principles

The Mythos Framework follows these approved architectural principles:

- Setting-agnostic framework
- Living world
- Shared player/NPC rules
- Domain ownership
- Event-driven world-state changes
- Unified entity model
- Hybrid world simulation
- Regional simulation
- Hierarchical regions
- Layered time
- Configurable calendars
- Persistent world history
- Reality, knowledge, and belief separation
- Modular game-specific content packages

--------------------------------------------

# 4. Foundational System Order

Current foundational specification order:

1. Entity Framework
2. Event Framework
3. Time Framework
4. Region Framework
5. Character Framework
6. NPC Framework
7. Relationship Framework
8. Economy Framework
9. Organization Framework
10. Gameplay frameworks

This order defines planning priority only. It does not imply final implementation classes, storage formats, engine selection, or runtime architecture.

--------------------------------------------

# 5. Setting Independence

Shared framework systems should use abstract concepts rather than setting-specific assumptions.

Setting-specific logic belongs in modular content packages, data definitions, configuration, or approved title modules.

The framework should remain compatible with future Mythos titles unless an approved specification defines a narrower scope.
