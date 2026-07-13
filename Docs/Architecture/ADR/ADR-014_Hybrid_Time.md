# ADR-014 — Hybrid Time

ADR number: ADR-014

Title: Hybrid Time

Version: 0.1

Status: Approved

Owner: Mythos Executive Development

Date: July 2026

## Context

Mythos requires continuous world time while supporting activities and simulations that operate at different update frequencies.

## Decision

The framework will support layered time, allowing systems to update at appropriate frequencies while sharing one calendar.

## Consequences

Time specifications must define update layers, time acceleration, synchronization expectations, and save requirements.

## Related systems

Time Framework, Event Framework, Region Framework, Simulation Systems
