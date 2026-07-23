# SYS-013-IMPL-M-002 Organization Framework Implementation Notes

- Document ID: SYS-013-IMPL-M-002
- Related Specification: [SYS-013 Organization Framework](SYS-013_Organization_Framework.md)
- Milestone: [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- Implementation Version: 0.1
- Status: Implemented
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Implemented Scope

The engine-independent `OrganizationFramework` registers Active Entities categorized as `Organization` with an extensible kind, lifecycle, timestamps, and provenance. Stable Membership records connect an Organization Entity to a distinct member Entity with a canonical set of extensible role references, lifecycle, timestamps, and provenance.

Only one Active Membership may exist for an Organization/member pair. Active role sets may be replaced; retired memberships are immutable and allow later replacement. Organization retirement requires callers to retire Active Memberships explicitly, avoiding hidden cross-record mutation. Registered terminal Entities remain valid for historical membership continuity.

Deterministic queries cover Organization, member, role, lifecycle, and involved Entity. An optional event sink publishes before mutation, making publication failure atomic. Versioned restoration validates the complete profile and Membership candidate, duplicate IDs and Active keys, role canonicalization constraints, Entity categories and references, and retirement consistency before replacing live state.

## Persistence

Complete-world persistence includes a required `organizations` partition restored after Entity identity. Profiles, stable Membership IDs, canonical roles, lifecycle, timestamps, and provenance round trip deterministically. The prototype framework marker advances to `m-002.5`.

## Boundaries and Reversible Decisions

The implementation does not define ranks, authority, leadership, permissions, hierarchy, recruitment workflow, employment, compensation, governance, diplomacy, warfare, AI, goals, treasuries, accounting, or title-specific Organization semantics. Organizations can own Property through the existing generic Entity ownership link without special integration.

M-002 uses UUIDv7 Membership IDs behind an injectable generator, immutable snapshots, and deterministic scans pending profiling. Role meaning and permissions remain data/content concerns for later approved systems.

## Verification

Coverage includes profile and Membership lifecycle, replacement, canonical roles, category and Active-Entity requirements, self-membership, duplicate keys and IDs, explicit retirement consistency, terminal continuity, event-failure atomicity, defensive deterministic snapshots, atomic restoration, complete-world persistence, deterministic bytes, corrupt member references, smoke integration, and existing persistence protections.
