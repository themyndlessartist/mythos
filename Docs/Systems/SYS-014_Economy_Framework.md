# SYS-014 Economy Framework

- Document ID: SYS-014
- Title: Economy Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

## Purpose

Provide a deterministic, setting-agnostic monetary ledger for Entity accounts and atomic value transfers as the first reusable Economy foundation.

## Core Model

An Economy Account has a stable Account ID, owner Entity, extensible currency ID, non-negative integer balance in indivisible minor units, Active or Closed lifecycle, timestamps, and optional provenance. At most one Active account exists for an owner and currency pair.

An immutable Transfer Record has a stable Transfer ID, source and destination Account IDs, positive amount, authoritative timestamp, optional correlation reference, and optional provenance.

## Responsibilities

- Account identity, lifecycle, balances, and owner/currency scope
- Atomic validated transfers between Active accounts of the same currency
- Immutable transfer ledger and deterministic account/transfer queries
- Versioned atomic snapshots, complete-world persistence, diagnostics, and optional events

## Non-Responsibilities

- Items, inventory, barter, prices, markets, supply, demand, production, consumption, trade routes, warehouses, work orders, businesses, payroll, taxes, fees, debt, loans, interest, credit, exchange rates, inflation, or monetary policy
- Currency content definitions, issuance authority, minting, destruction, or balance tuning
- Property or Organization ownership, NPC decisions, quests, law, crime, UI, or title-specific semantics
- External financial systems or real-world money

## Operations

- Open account with explicit initial balance
- Find account by ID or Active owner/currency key
- Transfer value atomically
- Close zero-balance account
- Find transfer by ID
- Query accounts by owner, currency, or lifecycle
- Query transfers by account, Entity owner, currency, correlation, or time range
- Validate references and ledger consistency
- Inspect diagnostics
- Export and atomically restore a versioned snapshot

## Invariants

- Account and Transfer IDs are initialized, immutable, and unique.
- Account owners remain registered Entities; terminal owners remain valid for historical continuity.
- Currency, correlation, and provenance identifiers are normalized.
- Balances are non-negative and arithmetic cannot overflow.
- At most one Active account exists per owner/currency key.
- Transfers use distinct Active accounts with the same currency and a positive amount.
- Source balance must cover the transfer.
- Transfer mutation, ledger append, and optional event publication are atomic.
- Transfer records are immutable and cannot be deleted in M-002.
- Accounts close only at zero balance; Closed accounts remain queryable and immutable.
- Failed operations or restore leave authoritative state unchanged.

## Events

- EconomyAccountOpened
- EconomyValueTransferred
- EconomyAccountClosed

Publication is optional through a narrow adapter and occurs before state commit.

## Dependencies

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

Property and Organization integrate only through generic Entity account ownership. Later market and business systems may consume this ledger through public contracts.

## Persistence, Performance, and Diagnostics

Persist versioned accounts and immutable transfers with stable IDs and canonical ordering. Restore after Entities. M-002 uses 64-bit signed minor units, deterministic scans, and no mandatory per-frame work. Production indexing and aggregate simulation await representative profiling.

## Deferred Decisions

- Currency definition packages, precision, issuance, minting, and destruction
- Markets, prices, supply/demand, barter, items, production, and consumption
- Account permissions, joint accounts, treasuries, escrow, and budgets
- Fees, taxes, payroll, debt, loans, credit, interest, insolvency, and bankruptcy
- Exchange rates, inflation, monetary policy, and economic reports
- Trade, transport, warehouses, work orders, and businesses
- Abstract regional economic simulation and reconciliation
- Content Studio authoring

## Tests

- Account opening, lookup, lifecycle, uniqueness, and query tests
- Transfer success, insufficient funds, currency mismatch, self-transfer, overflow, and atomicity tests
- Stable ledger, correlation, owner, currency, and time-range query tests
- Terminal owner continuity, event-failure atomicity, defensive deterministic snapshots, and atomic restore tests
- Complete-world persistence, deterministic-byte, corruption, and smoke tests

## Acceptance Criteria

SYS-014 is complete when Entity-owned currency accounts and immutable transfers persist and validate deterministically without introducing unapproved markets, prices, production, debt, policy, title content, or gameplay balance.

## Related Documents

- [M-002 Framework Alpha](../Milestones/M-002_Framework_Alpha.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [ADR-003 Hybrid Economy](../Architecture/ADR/ADR-003_Hybrid_Economy.md)
- [ADR-007 Meaningful Failure](../Architecture/ADR/ADR-007_Meaningful_Failure.md)
- [ADR-013 Shared World Rules](../Architecture/ADR/ADR-013_Shared_World_Rules.md)

