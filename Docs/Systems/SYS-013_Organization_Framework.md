# SYS-013 Organization Framework

- Document ID: SYS-013
- Title: Organization Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Purpose

Represent persistent Organizations and explicit Entity membership without defining title-specific guild, company, government, faction, religious, military, or criminal behavior.

## Core Model

An Organization Profile identifies an existing Organization Entity, extensible organization-kind ID, Active or Retired lifecycle, timestamps, and optional provenance.

A Membership Record identifies a stable membership ID, Organization Entity, member Entity, canonical set of extensible role IDs, Active or Retired lifecycle, timestamps, and optional provenance. At most one Active membership exists for an Organization and member pair.

## Responsibilities

- Organization profile classification and lifecycle
- Explicit membership identity, lifecycle, and role references
- Join, role replacement, retirement, lookup, and deterministic queries
- Versioned atomic snapshots, complete-world persistence, diagnostics, and optional events

## Non-Responsibilities

- Entity identity, hierarchy, ownership, or Region assignment
- Direct Relationships, Reputation, Information, History, or NPC behavior
- Leadership authority, permissions, ranks, elections, succession, governance, laws, diplomacy, warfare, employment, payroll, contracts, goals, schedules, or AI
- Property, businesses, markets, currencies, accounting, inventory, taxation, or Economy
- Title-specific Organization kinds or roles

## Operations

- Register Organization profile
- Find Organization by Entity ID
- Change Organization kind
- Retire Organization profile
- Add membership
- Find membership by ID or active Organization/member key
- Replace canonical role set
- Retire membership
- Query memberships by Organization, member, role, lifecycle, or involved Entity
- Validate references, inspect diagnostics, export and atomically restore snapshot

## Invariants

- Organization profiles and memberships reference registered Entities.
- Profile Entity category is `Organization`.
- Organization registration and new membership require Active Entities.
- Membership Organization must have an Active Organization Profile.
- Organization and member cannot be the same Entity.
- Role sets are non-null, unique, normalized, canonical, and may be empty.
- At most one Active membership exists per Organization/member pair.
- Retired profiles and memberships remain queryable and immutable.
- Retiring an Organization Profile does not silently mutate its memberships; callers must retire memberships explicitly or accept validation failure before persistence.
- Registered terminal Entities remain valid for historical membership continuity.
- Failed publication, mutation, or restoration leaves authoritative state unchanged.

## Events

- OrganizationRegistered
- OrganizationKindChanged
- OrganizationRetired
- MembershipCreated
- MembershipRolesChanged
- MembershipRetired

Publication is optional through a narrow adapter and occurs before state commit.

## Dependencies

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

Property, Relationship, Reputation, Information, History, NPC, and Economy may integrate later through public contracts.

## Persistence, Performance, and Diagnostics

Persist versioned profiles and memberships with stable IDs, canonical roles, lifecycle, timestamps, and provenance. Restore after Entities and before higher domains that consume Organizations. M-002 uses deterministic scans and no mandatory per-frame work; production indexes await profiling.

## Deferred Decisions

- Rank, role definitions, permissions, authority, leadership, offices, and succession
- Organization hierarchy and branches beyond generic Entity hierarchy
- Recruitment rules, applications, expulsion, suspension, and invitations
- Employment, compensation, ownership shares, contracts, and management
- Governance, voting, policies, diplomacy, warfare, and faction behavior
- Organization goals, AI, schedules, resources, treasuries, and accounting
- Relationship or Reputation projections
- Content Studio authoring

## Tests

- Profile and membership creation, lookup, roles, lifecycle, replacement, and queries
- Category, Entity lifecycle, self-membership, duplicate key/ID, role, timestamp, and reference validation
- Organization retirement consistency and terminal continuity
- Event-failure atomicity, defensive deterministic snapshots, and atomic restoration
- Complete-world persistence, deterministic-byte, corruption, and smoke tests

## Acceptance Criteria

SYS-013 is complete when generic Organization Profiles and explicit Membership Records persist and validate deterministically without introducing title semantics, authority, governance, employment, economy, AI, or automatic cross-domain effects.

## Related Documents

- [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [ADR-005 Scalable Gameplay](../Architecture/ADR/ADR-005_Scalable_Gameplay.md)
- [ADR-013 Shared World Rules](../Architecture/ADR/ADR-013_Shared_World_Rules.md)
- [ADR-019 Unified Entity Model](../Architecture/ADR/ADR-019_Unified_Entity_Model.md)

