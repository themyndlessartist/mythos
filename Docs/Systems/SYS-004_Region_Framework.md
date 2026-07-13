# SYS-004 — Region Framework

- Document ID: SYS-004
- Title: Region Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

--------------------------------------------

# 1. Purpose

The Region Framework represents hierarchical world areas and assigns simulation responsibility, containment, adjacency, and lookup boundaries.

Regions are entities.

--------------------------------------------

# 2. Design Principles

1. Regions use the Entity Framework.
2. Hierarchy does not imply political ownership.
3. Adjacency does not imply unrestricted travel.
4. Region assignment identifies simulation location, not exact physical coordinates.
5. Regions may operate at different simulation fidelities.
6. Cross-region effects use controlled events and public interfaces.
7. Region categories must remain extensible.
8. The design must not require a specific engine or programming language.

--------------------------------------------

# 3. Responsibilities

The Region Framework owns:

- Region hierarchy
- Region identity integration
- Parent and child regions
- Region categories
- Region boundaries
- Region adjacency
- Region containment
- Region lookup
- Entity-to-region assignment validation
- Simulation ownership boundaries
- Region activation state
- Cross-region transition metadata
- Region queries

--------------------------------------------

# 4. Non-Responsibilities

The Region Framework does not own:

- Political ownership
- Economy
- Weather
- NPC behavior
- Travel resolution
- Settlement gameplay
- Map rendering
- Pathfinding
- Faction control
- Borders created solely by politics unless represented through region metadata or another domain

Other systems may use regions as simulation and lookup boundaries, but the Region Framework does not implement those systems.

--------------------------------------------

# 5. Public Concepts

## Region Entity

An entity that represents a world area.

## Region Category

An extensible classification describing the role or scale of a region.

## Parent Region

A containing region above another region in the hierarchy.

## Child Region

A contained region below another region in the hierarchy.

## Boundary

A conceptual definition or reference describing the extent of a region.

## Adjacency

A relationship indicating that two regions are neighbors for approved lookup or transition purposes.

## Containment

A hierarchy or boundary relationship indicating that one region contains another region or assigned entity.

## Simulation Owner

The region or system scope responsible for coordinating simulation for a location.

## Active Region

A region currently simulated at an approved active fidelity.

## Abstract Region

A region currently simulated or represented at an approved abstract fidelity.

## Transition Point

Metadata describing a controlled transition between regions.

## Region Metadata

Extensible non-domain-specific metadata associated with a region.

## Hierarchical Model

The framework supports configurable nested levels.

Examples may include:

- World
- Continent
- Nation
- Province
- Settlement
- District

or:

- Galaxy
- Sector
- Star system
- Planet
- Colony

No level is mandatory except a root world scope.

--------------------------------------------

# 6. Conceptual Public Operations

The framework must eventually support operations equivalent to:

- Create region entity
- Assign parent region
- Remove parent
- Add adjacency
- Remove adjacency
- Query parent
- Query children
- Query ancestors
- Query descendants
- Query adjacent regions
- Assign entity to region
- Transfer entity between regions
- Activate region
- Abstract region
- Query entities by region
- Determine containment
- Resolve simulation ownership

This document must not define language-specific method signatures.

--------------------------------------------

# 7. Events Published

The Region Framework should eventually publish events such as:

- RegionCreated
- RegionRetired
- RegionHierarchyChanged
- RegionAdjacencyChanged
- RegionActivated
- RegionAbstracted
- EntityEnteredRegion
- EntityLeftRegion
- EntityRegionChanged

Final event contracts belong to the Event Framework.

--------------------------------------------

# 8. Events Consumed

The Region Framework should consume only events necessary to maintain approved region hierarchy, assignment, activation, abstraction, adjacency, or transition behavior.

It must not absorb political, travel, economy, weather, or NPC responsibilities from other systems.

--------------------------------------------

# 9. Data Ownership

The Region Framework owns region hierarchy metadata, region categories, boundary references, adjacency, containment metadata, activation state, simulation ownership boundaries, assigned entity references, and transition metadata.

It does not own domain-specific control, weather, travel, pathfinding, or map-rendering data.

--------------------------------------------

# 10. Dependencies

Required conceptual dependencies:

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- Save/Persistence Framework
- [SYS-003 Time Framework](SYS-003_Time_Framework.md) for simulation scheduling

Circular dependencies must be avoided.

--------------------------------------------

# 11. Save Requirements

Save data must preserve:

- Region entity IDs
- Categories
- Parent/child links
- Adjacency
- Boundaries or boundary references
- Metadata
- Activation state
- Simulation ownership
- Assigned entity references
- Transition metadata

Loading must restore region references without generating new IDs for existing region entities.

--------------------------------------------

# 12. Performance Requirements

The framework should support:

- Efficient hierarchy queries
- Efficient region entity lookup
- Batch activation and abstraction
- Large world support
- Regional save partitioning compatibility
- Debug visualization support

Exact performance targets will be established after engine selection and prototyping.

--------------------------------------------

# 13. Validation Rules

The framework must detect or prevent:

- Circular region hierarchies
- Self-parenting
- Invalid adjacency references
- Invalid boundary data
- Multiple incompatible primary-region assignments
- Missing root region
- Orphaned regions
- Invalid transitions
- Conflicting simulation ownership

--------------------------------------------

# 14. Extension Points

Future Mythos titles may add:

- New region categories
- New region hierarchy levels
- New boundary metadata
- New transition metadata
- New activation policies
- New abstraction policies
- New region-query indexes

Extensions must not require the core region model to assume one setting's world scale.

--------------------------------------------

# 15. Debugging Requirements

Future implementation should provide developer tools to:

- Inspect a region by ID
- View parent and child regions
- View ancestors and descendants
- View adjacency
- View assigned entities
- View activation state
- View simulation ownership
- Detect hierarchy cycles
- Detect orphaned regions
- Export region summaries

--------------------------------------------

# 16. Risks

Primary risks include:

- Confusing hierarchy with political ownership
- Treating adjacency as unrestricted travel
- Hard-coding one setting's region levels
- Creating circular region hierarchies
- Mixing physical coordinates with simulation-region assignment too early
- Letting region responsibilities absorb travel, pathfinding, or settlement gameplay

--------------------------------------------

# 17. Deferred Decisions

The following remain open:

- Exact boundary representation
- Coordinate system
- Streaming model
- Region partition size
- Local time zones
- Region-level save partitioning
- Overlapping-region rules
- Multi-region entity support
- Exact activation thresholds

--------------------------------------------

# 18. Acceptance Criteria

SYS-004 is complete when it clearly defines:

- What a region is
- What the Region Framework owns
- What it does not own
- Region identity requirements
- Hierarchy behavior
- Adjacency behavior
- Containment behavior
- Region assignment boundaries
- Save requirements
- Events
- Validation requirements
- Extension points
- Deferred implementation decisions

--------------------------------------------

# 19. Cross-References

- [SD-001 Project Charter](../Executive/SD-001_Project_Charter.md)
- [SD-002 Framework Overview](../Executive/SD-002_Framework_Overview.md)
- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [ADR-008 Hybrid World Simulation](../Architecture/ADR/ADR-008_Hybrid_World_Simulation.md)
- [ADR-017 Hierarchical Regions](../Architecture/ADR/ADR-017_Hierarchical_Regions.md)
- [ADR-019 Unified Entity Model](../Architecture/ADR/ADR-019_Unified_Entity_Model.md)
