# SYS-014-IMPL-M-002 Economy Framework Implementation Notes

- Document ID: SYS-014-IMPL-M-002
- Related Specification: [SYS-014 Economy Framework](SYS-014_Economy_Framework.md)
- Milestone: [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- Implementation Version: 0.1
- Status: Implemented
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Implemented Scope

The engine-independent `EconomyFramework` owns stable Entity accounts scoped by extensible currency IDs. Accounts retain opening and current balances in non-negative 64-bit minor units, Active or Closed lifecycle, timestamps, and provenance. At most one Active account exists per owner/currency key.

Immutable Transfer records atomically move positive value between distinct Active accounts of the same currency. Transfers validate funds, timestamp ordering, overflow, optional correlation, and provenance before optional event publication and state commit. Queries cover owner, currency, lifecycle, account involvement, Entity owner involvement, correlation, and inclusive time ranges.

Account closure requires zero balance and permits a later replacement account. Registered terminal owners remain valid for historical continuity. Versioned restoration recomputes every account balance from its opening balance and chronologically ordered immutable transfers, rejects negative intermediate balances and arithmetic overflow, and replaces live state only after the complete candidate validates.

## Persistence

Complete-world persistence includes a required `economy` partition restored after Entity identity. Accounts, immutable transfers, stable IDs, balances, lifecycle, timestamps, correlation, and provenance round trip deterministically. The prototype framework marker advances to `m-002.6`.

## Boundaries and Reversible Decisions

The implementation does not define items, barter, prices, markets, supply/demand, production, consumption, currency issuance, exchange, taxes, payroll, debt, loans, bankruptcy, businesses, trade, or title balance. Property and Organizations participate only as generic Entity account owners.

M-002 uses signed 64-bit minor units, UUIDv7 IDs behind an injectable generator, immutable transfers, and deterministic scans pending profiling. Currency definitions and precision remain external content/configuration concerns.

## Verification

Coverage includes account lifecycle and uniqueness, atomic transfers, insufficient funds, currency mismatch, self-transfer, closure and replacement, terminal owner continuity, ledger and correlation queries, event-failure atomicity, balance recomputation, defensive deterministic snapshots, atomic malformed-state rejection, complete-world persistence, deterministic bytes, corrupt owner references, smoke integration, and existing persistence protections.
