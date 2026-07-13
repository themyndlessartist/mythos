# SYS-001 — Entity Framework

- Document ID: SYS-001
- Title: Entity Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

--------------------------------------------

# 1. Purpose

The Entity Framework provides a shared identity and lifecycle foundation for all significant objects within Mythos.

An entity is any identifiable object that can participate in one or more framework systems.

Examples include:

- Players
- NPCs
- Animals
- Monsters
- Items
- Buildings
- Businesses
- Organizations
- Vehicles
- Settlements
- Regions

The Entity Framework must remain setting-agnostic.

--------------------------------------------

# 2. Design Principles

1. Use composition rather than deep inheritance.
2. Entity identity is separate from gameplay behavior.
3. Entity identifiers are globally unique within a saved world.
4. Entity identifiers never change.
5. Retired or destroyed entity identifiers are never reused.
6. Players and NPCs use the same entity foundation.
7. Organizations, properties, settlements, characters, and items use the same identity model.
8. Entity data must support serialization and persistence.
9. The design must not require a specific engine or programming language.
10. The specification does not mandate a full Entity Component System implementation.

--------------------------------------------

# 3. Responsibilities

The Entity Framework owns:

- Entity identifier generation
- Entity registration
- Entity lookup
- Entity creation lifecycle
- Entity retirement lifecycle
- Entity category metadata
- Entity tags
- Component-reference registration
- Generic parent/child hierarchy links
- Generic ownership links
- Region/location assignment
- Lifecycle state
- Serialization requirements for entity identity
- Historical reference compatibility

--------------------------------------------

# 4. Non-Responsibilities

The Entity Framework does not own:

- Character stats
- Health
- Inventory contents
- AI behavior
- NPC schedules
- Combat
- Economy
- Reputation
- Relationships
- Dialogue
- Quests
- Rendering
- Animation
- Physics
- Navigation
- Setting-specific gameplay rules

Other systems may attach data or capabilities to an entity, but the Entity Framework does not implement those systems.

--------------------------------------------

# 5. Public Concepts

## Entity ID

A stable, globally unique identifier for an entity within a world state.

Requirements:

- Immutable
- Never reused
- Serializable
- Suitable for cross-system references
- Suitable for save files
- Suitable for world-history references
- Independent of display names and engine object references

## Entity Record

The minimal persistent record representing an entity.

Conceptual fields:

- Entity ID
- Entity category
- Lifecycle state
- Tags
- Parent entity reference, if applicable
- Owner entity reference, if applicable
- Region entity reference, if applicable
- Registered component references
- Creation timestamp
- Retirement timestamp, if applicable

Do not define a final programming-language structure or schema yet.

## Entity Category

A broad classification used for organization, validation, and querying.

Example conceptual categories:

- Character
- Creature
- Item
- Structure
- Organization
- Business
- Vehicle
- Settlement
- Region
- Abstract world entity

Categories must be data-driven or extensible.

## Tags

Lightweight, extensible metadata used for filtering and identification.

Examples:

- Named
- Persistent
- Historical
- PlayerControlled
- Simulated
- Tradable
- Ownable

Tags must not replace dedicated system data when structured behavior is required.

## Lifecycle State

Minimum conceptual lifecycle states:

- Active
- Inactive
- Retired
- Destroyed

Destroyed and retired entities may no longer participate in active simulation but must remain referenceable by history, relationships, inheritance, ownership records, or save data.

## Hierarchy Relationship

A generic parent/child relationship.

Examples:

- World -> Continent
- Organization -> Branch
- Army -> Unit
- Settlement -> District
- Business -> Location

Hierarchy does not imply ownership.

## Ownership Relationship

A generic owner/owned relationship.

Examples:

- Character owns property
- Organization owns warehouse
- Kingdom owns fortress
- Business owns vehicle

Ownership does not imply hierarchy.

## Region Assignment

An entity may be assigned to a region entity.

Region assignment identifies where the entity belongs for simulation and lookup purposes.

Movement and travel logic belong to other systems.

## Component Reference

The Entity Framework may record which capabilities or component datasets are associated with an entity.

Examples:

- Character data
- Inventory data
- Organization data
- Property data
- Reputation data
- Simulation data

The Entity Framework does not own the contents of those components.

--------------------------------------------

# 6. Conceptual Public Operations

The framework must eventually support operations equivalent to:

- Create entity
- Register entity
- Find entity by ID
- Query entities by category
- Query entities by tag
- Assign or remove tag
- Assign parent
- Remove parent
- Assign owner
- Transfer ownership
- Assign region
- Change lifecycle state
- Retire entity
- Destroy entity
- Check whether an entity exists
- Check whether an entity is active
- Enumerate child entities
- Enumerate owned entities
- Enumerate entities within a region

This document must not define language-specific method signatures.

--------------------------------------------

# 7. Events Published

The Entity Framework should eventually publish events such as:

- EntityCreated
- EntityRegistered
- EntityActivated
- EntityDeactivated
- EntityRetired
- EntityDestroyed
- EntityTagAdded
- EntityTagRemoved
- EntityParentChanged
- EntityOwnerChanged
- EntityRegionChanged
- EntityComponentRegistered
- EntityComponentRemoved

The Event Framework will define final event contracts.

--------------------------------------------

# 8. Events Consumed

The Entity Framework should consume only events necessary to maintain entity identity, lifecycle, hierarchy, ownership, or region assignment.

It must not absorb gameplay responsibilities from other systems.

--------------------------------------------

# 9. Data Ownership

The Entity Framework owns only minimal entity identity and relationship metadata.

It does not own domain-specific component data.

--------------------------------------------

# 10. Dependencies

Required conceptual dependencies:

- None for basic identity
- Event Framework for published lifecycle events
- Save/Persistence Framework for serialization
- Region Framework for validated region assignment

Circular dependencies must be avoided.

--------------------------------------------

# 11. Save Requirements

Save data must preserve:

- Entity IDs
- Category
- Tags
- Lifecycle state
- Parent relationship
- Ownership relationship
- Region assignment
- Component references
- Creation and retirement timestamps

Loading must restore references without generating new IDs for existing entities.

Missing or invalid references must be detectable and reported.

--------------------------------------------

# 12. Performance Requirements

The framework should support:

- Large entity counts
- Fast lookup by ID
- Efficient filtering by category, tag, owner, parent, and region
- Abstract distant simulation
- Entity activation and deactivation
- Batch operations
- Debug inspection

Exact performance targets will be established after engine selection and prototyping.

--------------------------------------------

# 13. Validation Rules

The framework must detect or prevent:

- Duplicate entity IDs
- Reused retired IDs
- Invalid owner references
- Invalid parent references
- Self-parenting
- Circular hierarchy relationships
- Invalid region references
- References to missing components
- Illegal lifecycle transitions

Ownership cycles may be allowed only if explicitly approved by a future specification. Default behavior should reject them.

--------------------------------------------

# 14. Extension Points

Future Mythos titles may add:

- New entity categories
- New tags
- New component types
- New lifecycle metadata
- New entity-query indexes
- New validation policies

Extensions must not require modification of the core identity model whenever practical.

--------------------------------------------

# 15. Debugging Requirements

Future implementation should provide developer tools to:

- Inspect an entity by ID
- View tags
- View lifecycle state
- View parent and children
- View owner and owned entities
- View region
- View registered components
- Find broken references
- Detect hierarchy cycles
- Export entity summaries

--------------------------------------------

# 16. Risks

Primary risks include:

- Turning the Entity Framework into a monolithic gameplay system
- Treating tags as replacements for structured data
- Creating circular dependencies
- Overcommitting to a specific ECS architecture too early
- Allowing engine object references to replace persistent entity IDs
- Deleting entities that remain referenced by history or relationships

--------------------------------------------

# 17. Deferred Decisions

The following remain open:

- UUID versus another identifier strategy
- ECS versus OOP versus hybrid implementation
- Final serialization format
- Database strategy
- Component storage model
- Entity ID representation
- Entity garbage-collection policy
- Exact active/inactive simulation behavior
- Ownership-cycle rules

--------------------------------------------

# 18. Acceptance Criteria

SYS-001 is complete when it clearly defines:

- What an entity is
- What the Entity Framework owns
- What it does not own
- Entity identity requirements
- Lifecycle behavior
- Hierarchy behavior
- Ownership behavior
- Region assignment
- Component-reference boundaries
- Save requirements
- Events
- Validation requirements
- Extension points
- Deferred implementation decisions
