# SYS-012-IMPL-M-002 Property Framework Implementation Notes

- Document ID: SYS-012-IMPL-M-002
- Related Specification: [SYS-012 Property Framework](SYS-012_Property_Framework.md)
- Milestone: [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- Implementation Version: 0.1
- Status: Implemented
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Implemented Scope

The engine-independent `PropertyFramework` registers existing Entities as property with an extensible kind, Active or Retired profile lifecycle, timestamps, and optional provenance. It delegates sole-owner storage and cycle validation to the Entity Framework, preserving one authoritative ownership link.

Active profiles support kind changes and owner assignment, transfer, or clearing. Deterministic queries cover kind, authoritative owner, authoritative Region, lifecycle, and involved Entity. Profile retirement is terminal but intentionally leaves the underlying Entity and ownership link unchanged.

Ownership mutation requires an Active property Entity and a registered non-terminal owner. Registered terminal Entities remain valid in existing profiles for historical continuity. An optional event sink publishes before mutation; event or Entity ownership failure leaves profile and owner state unchanged. Versioned restoration validates a complete candidate before replacing live profile state and never restores or overrides ownership itself.

## Persistence

Complete-world persistence includes a required `properties` partition restored after Entities and Regions. Only Property Profiles are stored there; owner and Region links remain in the Entity partition. The prototype framework marker advances to `m-002.4`.

## Boundaries and Reversible Decisions

The implementation does not provide shared title, leases, access rights, management delegation, mortgages, liens, inheritance, valuation, income, expenses, taxes, construction, damage, contracts, or legal semantics. It does not treat businesses, buildings, settlements, or inventory as property unless explicitly registered by a caller.

M-002 supports sole ownership through the existing Entity `OwnerId`, immutable profile snapshots, and deterministic in-memory scans pending profiling. Shared or layered ownership requires a later approved model that does not duplicate authoritative state.

## Verification

Coverage includes registration, classification, authoritative owner and Region queries, transfer and clearing, ownership cycles, lifecycle rules, profile retirement, terminal continuity, event-failure atomicity, deterministic defensive snapshots, atomic restoration, complete-world persistence, deterministic bytes, corrupt Entity references, smoke integration, and existing persistence protections.
