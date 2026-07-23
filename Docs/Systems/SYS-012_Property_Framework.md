# SYS-012 Property Framework

- Document ID: SYS-012
- Title: Property Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Purpose

Represent persistent property assets and their property-specific state while preserving the Entity Framework as the sole authority for identity, Region assignment, and the approved single-owner relationship.

## Core Model

A Property Profile identifies:

- the existing Entity that represents the asset
- an extensible property-kind ID
- Active or Retired property-profile lifecycle
- registration and last-change timestamps
- optional provenance reference

The Entity Framework's `OwnerId` is authoritative for M-002 sole ownership. A Property Profile does not duplicate owner or Region data.

## Responsibilities

- Registering an existing Entity as property
- Property-kind classification and lifecycle
- Validated sole-owner assignment and transfer through the Entity Framework
- Deterministic lookup and queries by kind, owner, Region, lifecycle, or involved Entity
- Versioned atomic snapshots, complete-world persistence, diagnostics, and optional events

## Non-Responsibilities

- Entity identity, lifecycle, ownership-link storage, or Region assignment
- Shared ownership, leases, tenancy, mortgages, liens, inheritance, taxes, valuation, income, upkeep, or insurance
- Buildings, construction, terrain modification, businesses, inventories, warehouses, settlements, Organizations, or Economy
- Access permissions, law, theft, repossession, AI behavior, or title-specific property rules
- Rendering, maps, physics, navigation, or scene ownership

## Operations

- Register property
- Find property by Entity ID
- Change property kind
- Assign, transfer, or clear sole owner
- Retire property profile
- Query by kind, owner, Region, lifecycle, or involved Entity
- Validate references
- Inspect diagnostics
- Export and atomically restore a versioned snapshot

Queries and snapshots use stable Entity-ID ordering.

## Invariants

- Each Property Profile references one registered Entity.
- At most one Property Profile exists per Entity.
- Property-kind and provenance identifiers are initialized and normalized.
- Registration time cannot follow last-change time.
- Only Active profiles may change kind or ownership through this framework.
- Retired profiles remain queryable and immutable.
- Property retirement does not destroy or retire the underlying Entity and does not silently clear ownership.
- Owners must be registered Entities and cannot equal the property Entity.
- Entity Framework ownership-cycle validation remains authoritative.
- Terminal property or owner Entities remain referenceable for historical continuity, but ownership mutation requires an Active property Entity and a non-terminal owner.
- Failed publication, mutation, transfer, or restore leaves authoritative state unchanged.
- Null collections, unknown lifecycle, duplicate Entity profiles, broken references, malformed identifiers, and invalid timestamps are rejected.

## Events

- PropertyRegistered
- PropertyKindChanged
- PropertyOwnerChanged
- PropertyRetired

Publication is optional through a narrow adapter and occurs before state commit. The Property Framework must not duplicate Entity Framework events or mutate ownership if Property event publication fails.

## Dependencies

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-004 Region Framework](SYS-004_Region_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

Organization and Economy may consume Property contracts later but do not own Property state.

## Persistence, Performance, and Diagnostics

Persist only versioned Property Profiles. Entity ownership and Region assignment continue to persist in the Entity partition. Restore Properties after Entities and Regions, then validate profiles against authoritative Entity state. M-002 uses deterministic scans and no mandatory per-frame work; production indexes await profiling. Diagnostics expose defensive profiles, current authoritative owner/Region, and validation status.

## Deferred Decisions

- Shared, fractional, collective, or layered ownership
- Leases, tenancy, use rights, access rights, and management delegation
- Mortgages, liens, debt, seizure, inheritance, and succession
- Valuation, rent, income, expenses, taxes, upkeep, and insurance
- Property composition, parcels, construction, damage, and upgrades
- Transfer consideration, contracts, legal validity, and History integration
- Organization-owned property policy beyond generic Entity ownership
- Content Studio authoring

## Tests

- Registration, lookup, classification, lifecycle, owner assignment, transfer, clearing, and query tests
- Duplicate profile, identifier, timestamp, Entity lifecycle, owner, self-owner, and ownership-cycle tests
- Event-failure atomicity and Entity ownership rollback tests
- Defensive deterministic snapshot and atomic restoration tests
- Complete-world persistence, deterministic-byte, corruption, and smoke tests

## Acceptance Criteria

SYS-012 is complete when Property Profiles and approved sole-ownership operations work through the Entity Framework, persist and validate deterministically, and do not introduce duplicate ownership state or deferred economic, legal, construction, access, or title-specific behavior.

## Related Documents

- [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [ADR-009 Broad Ownership](../Architecture/ADR/ADR-009_Broad_Ownership.md)
- [ADR-019 Unified Entity Model](../Architecture/ADR/ADR-019_Unified_Entity_Model.md)

