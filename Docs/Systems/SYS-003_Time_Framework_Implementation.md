# SYS-003 — Time Framework Implementation Notes

- Related Specification: [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- Prototype Milestone: [M-001 Foundation Prototype](../Milestones/M-001_Foundation_Prototype.md)
- Implementation Version: 0.1
- Status: In Progress
- Last Updated: July 2026

## Implemented Scope

The M-001 prototype currently provides:

- One engine-independent authoritative clock using non-negative integer world units
- Exact rational time scales with persisted fractional remainder
- Composable pause reasons that all must be resumed before advancement
- Validated, data-configurable calendars with variable-length periods and optional seasons
- Absolute, relative, and fixed-interval recurring schedules
- Deterministic due-work ordering by due timestamp and creation sequence
- Bounded schedule-storm catch-up that leaves remaining overdue work pending
- Named simulation layers with independent intervals, deterministic progress markers, and bounded catch-up
- Versioned immutable clock, scheduler, recurrence, pause, scale, calendar-reference, and simulation-layer snapshots
- An optional adapter that publishes completed `TimeAdvanced` and `ScheduledTaskDue` outcomes through SYS-002 public contracts
- Automated xUnit and smoke coverage independent of Godot

## Public Contract and Data Ownership

`WorldClock` owns the authoritative timestamp, time scale, scale remainder, and pause reasons. `CalendarModel` owns validated interpretation metadata. `TimeScheduler` owns schedule identity, due time, recurrence state, metadata, and creation ordering. `SimulationLayerCoordinator` owns layer intervals and progress markers.

Due schedules and layer markers are immutable descriptors. The framework does not execute gameplay callbacks or own outcomes produced by consuming systems. Schedule category and string metadata are opaque coordination data; their meaning remains with the registering domain.

## Implementation Decisions

- World units are deliberately abstract. A title calendar defines `UnitsPerDay`; the core does not name or assume seconds, minutes, Gregorian months, or a real-time day length.
- Year and day numbering are zero-based and one-based respectively: the epoch is year 0, period 0, day 1, unit 0.
- Period lengths are expressed in whole calendar days. Seasons are optional and begin at configured one-based days of year. Before the first configured boundary, interpretation wraps to the final season.
- Time scale is a reduced non-negative rational number. This avoids floating-point nondeterminism and preserves fractional progress across advances and snapshots.
- Pause requests are idempotence-sensitive: duplicate pause and missing resume operations return structured failures so conflicting state is diagnosable.
- Scheduling at the current timestamp is supported; scheduling before it is rejected. Recurrence is a positive fixed world-unit interval.
- Catch-up is pull-based and bounded per operation. Repeating work is advanced occurrence by occurrence only when due because each occurrence is externally observable; callers may continue draining after a limit report.
- Simulation layers report progress markers rather than calling systems. Consumers decide how to batch or abstract domain simulation.
- Event publication is explicit through `TimeEventBridge` after a successful advance. Event-handler failure cannot roll back or partially own authoritative time state.

## Persistence and Restore Behavior

The prototype snapshot is versioned but is not a final serialized save schema. Restore requires the exact calendar ID and version supplied by configuration. It rejects incompatible versions, invalid rational scale state, duplicate pause reasons, overdue schedules, duplicate IDs, invalid recurrence, and invalid layer progress. Restore does not dispatch due work and therefore cannot duplicate scheduled outcomes merely by loading.

## Failure and Diagnostics Behavior

Domain boundary failures use stable `time.*` error codes. Primitive value constructors reject negative timestamps and durations immediately. Advance overflow is atomic and leaves the timestamp and fractional scale state unchanged. Catch-up limits are reported on successful advances and pending due state remains inspectable through deterministic snapshot export.

## Known Limitations and Deferred Work

- The specification defers the final calendar and scheduler persistence formats; these snapshots are coordination contracts for M-001 only.
- Recurrence supports fixed world-unit intervals, not calendar-relative rules such as “each period.” No such rule is approved yet.
- Calendar leap rules, time zones, astronomy, daylight, multiplayer pause policy, threading, and a maximum fast-forward duration remain deferred by SYS-003.
- Calendar boundary events beyond `TimeAdvanced` and `ScheduledTaskDue` are not published. Their exact semantics and title opt-in policy remain unspecified.
- Catch-up bounds schedules and simulation layers independently with the same caller-supplied limit. Cross-domain global workload budgeting awaits profiling and broader simulation coordination.
- The scheduler and clock are single-threaded prototype services.

## Verification

Automated tests cover clock and scale advancement, invalid time, calendar validation and interpretation, pause reasons, absolute/relative/recurring schedules, deterministic ordering, cancellation, large skips, storm bounds, simulation-layer progress, recurrence snapshot restoration, incompatible restore state, metadata isolation, and Event Framework publication. The repository build script additionally runs the smoke executable and headless Godot import and entry-scene checks.
