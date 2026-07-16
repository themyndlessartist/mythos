# ADR-024 — M-001 Prototype Decision Governance and Test Tooling

ADR number: ADR-024

Title: M-001 Prototype Decision Governance and Test Tooling

Version: 0.1

Status: Approved

Owner: Mythos Executive Development

Date: July 2026

## Context

M-001 must implement and validate approved framework specifications while several specifications intentionally defer final implementation details. The milestone also requires automated tests, but the approved documentation did not previously identify the selected prototype test tooling or define how milestone-local implementation choices relate to deferred production decisions.

Without an explicit governance rule, reversible prototype choices could be mistaken for final architecture, or implementation could be blocked whenever a specification deliberately leaves a replaceable detail open.

## Decision

M-001 implementation notes may establish reversible, milestone-local prototype choices where an approved specification intentionally defers implementation details.

Each such choice must be documented as prototype-local and replaceable. It does not close, supersede, or approve the corresponding production decision. Final production behavior remains subject to the approved specification, a future specification revision, or a later Architecture Decision Record.

xUnit 3.2.0 is approved as test-only tooling for M-001. It is not a framework runtime dependency and does not select the permanent testing stack for later milestones.

M-001 implementation notes use the document identity convention `SYS-<number>-IMPL-M-<milestone>`. Each note must include Document ID, Title, Related Specification, Prototype Milestone, Implementation Version, Status, Owner, Last Updated, Applies Through Commit, and Approval/Decision References metadata.

## Alternatives considered

- Require every prototype implementation detail to be approved through a specification revision or separate ADR before implementation.
- Treat implementation-note decisions as permanent resolutions of deferred specification decisions.
- Avoid external test tooling during M-001.
- Approve xUnit as a permanent project-wide testing standard.

## Consequences

- M-001 can make necessary, reversible implementation choices without silently converting them into production architecture.
- Implementation notes become the authoritative record of milestone-local choices and must clearly label their limited scope.
- Deferred production decisions remain open until separately approved.
- Reviewers can distinguish approved system contracts from replaceable prototype mechanisms.
- xUnit 3.2.0 may be used by M-001 test projects without becoming a runtime dependency or a permanent tooling commitment.
- Implementation notes have unique, milestone-scoped identities and consistent provenance metadata.

## Related systems

- [M-001 Foundation Prototype](../../Milestones/M-001_Foundation_Prototype.md)
- [STD-001 Technical Architecture Standards](../STD-001_Technical_Architecture_Standards.md)
- [SYS-001 Entity Framework Implementation Notes](../../Systems/SYS-001_Entity_Framework_Implementation.md)
- [SYS-002 Event Framework Implementation Notes](../../Systems/SYS-002_Event_Framework_Implementation.md)
- [SYS-003 Time Framework Implementation Notes](../../Systems/SYS-003_Time_Framework_Implementation.md)

