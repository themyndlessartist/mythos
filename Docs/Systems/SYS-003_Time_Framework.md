# SYS-003 — Time Framework

- Document ID: SYS-003
- Title: Time Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

--------------------------------------------

# 1. Purpose

The Time Framework provides one authoritative world timeline, configurable calendars, layered simulation scheduling, time scaling, and scheduled execution.

--------------------------------------------

# 2. Design Principles

1. There is one authoritative world clock.
2. Calendars are data-configurable per title.
3. The framework must not assume Gregorian calendar terms.
4. Systems may update at different frequencies while sharing one timeline.
5. Time acceleration advances the entire world.
6. Interface screens may pause time.
7. World interactions normally do not pause time.
8. Time advancement must be deterministic.
9. Aging is separate from ordinary calendar progression.
10. The Time Framework does not know why time is advanced; requesting systems provide the request.
11. The design must not require a specific engine or programming language.

--------------------------------------------

# 3. Responsibilities

The Time Framework owns:

- World clock
- Calendar interpretation
- Time advancement
- Time scale
- Pause state
- Layered update scheduling
- Absolute schedules
- Relative schedules
- Recurring schedules
- Season boundaries
- Time queries
- Deterministic timestamps

--------------------------------------------

# 4. Non-Responsibilities

The Time Framework does not own:

- NPC schedules
- Aging outcomes
- Crop growth
- Weather behavior
- Construction behavior
- Crafting outcomes
- Travel rules
- Prison logic
- Research logic

Other systems react to time or request valid time advancement.

--------------------------------------------

# 5. Public Concepts

## World Timestamp

The authoritative position on the shared world timeline.

## Calendar Definition

A configurable title-specific mapping from world time to calendar dates, seasons, and display concepts.

## Calendar Date

A interpreted calendar value derived from the world timestamp and active calendar definition.

## Time Scale

A multiplier or mode describing how quickly world time advances relative to real time or requested simulation time.

## Pause Reason

A tracked reason for stopping world-time advancement when approved pause rules apply.

## Simulation Layer

A named update layer used by systems that operate at different frequencies while sharing one timeline.

## Scheduled Task

A scheduled item that becomes due at an absolute time, after a duration, or according to a recurrence rule.

## Recurrence Rule

A configurable definition for repeated scheduled work.

## Season Definition

A configurable calendar concept used to determine season boundaries.

## Time-Advance Request

A request from another system to advance time normally or at an accelerated rate.

--------------------------------------------

# 6. Conceptual Public Operations

The framework must eventually support operations equivalent to:

- Query current timestamp
- Query calendar date
- Query season
- Query daylight classification through approved calendar/astronomy integration
- Advance time
- Request accelerated time
- Pause
- Resume
- Schedule at absolute time
- Schedule after duration
- Schedule recurring work
- Cancel schedule
- Query due schedules
- Register simulation update layer

This document must not define language-specific method signatures.

--------------------------------------------

# 7. Events Published

The Time Framework should eventually publish events such as:

- TimeAdvanced
- TimeScaleChanged
- TimePaused
- TimeResumed
- MinuteChanged
- HourChanged
- DayStarted
- DayEnded
- WeekStarted
- MonthStarted
- SeasonChanged
- YearChanged
- ScheduledTaskDue

Do not require every title to use every calendar event.

--------------------------------------------

# 8. Events Consumed

The Time Framework should consume only events necessary to maintain approved clock, pause, schedule, or time-scale behavior.

It must not absorb gameplay outcomes from systems that respond to time.

--------------------------------------------

# 9. Data Ownership

The Time Framework owns authoritative timestamp state, calendar interpretation metadata, time-scale state, pause state, scheduler metadata, recurrence state, season boundaries, and simulation-layer progress markers.

It does not own domain outcomes produced by time-aware systems.

--------------------------------------------

# 10. Dependencies

Required conceptual dependencies:

- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)
- Configuration/Data Framework
- [SYS-004 Region Framework](SYS-004_Region_Framework.md) only if local time zones are later approved

Circular dependencies must be avoided.

--------------------------------------------

# 11. Save Requirements

Save data must preserve:

- Authoritative timestamp
- Calendar identifier and version
- Current time scale
- Active pause state where appropriate
- Pending schedules
- Recurrence state
- Simulation-layer progress markers

Loading must restore time state without causing duplicate scheduled work.

--------------------------------------------

# 12. Performance Requirements

The framework should support:

- Efficient large time skips
- Batch processing for distant simulation
- No requirement to process every elapsed minute individually
- Deterministic catch-up
- Protection against schedule storms
- Debuggable scheduled work

Exact performance targets will be established after engine selection and prototyping.

--------------------------------------------

# 13. Validation Rules

The framework must detect or prevent:

- Invalid calendar definitions
- Invalid dates
- Negative time advancement unless explicitly approved
- Duplicate schedule IDs
- Invalid recurrence
- Scheduling in unsupported past states
- Infinite recurrence loops
- Conflicting pause states
- Excessive catch-up workloads

--------------------------------------------

# 14. Extension Points

Future Mythos titles may add:

- New calendar definitions
- New season definitions
- New simulation layers
- New pause categories
- New schedule categories
- New time-display rules
- New approved astronomy or daylight integrations

Extensions must not require the core timeline to assume one setting's calendar.

--------------------------------------------

# 15. Debugging Requirements

Future implementation should provide developer tools to:

- Inspect current timestamp
- Inspect interpreted calendar date
- View active time scale
- View pause reasons
- View simulation-layer progress
- Inspect pending schedules
- Inspect recurrence state
- Detect schedule storms
- Simulate controlled time advancement
- Export time and schedule summaries

--------------------------------------------

# 16. Risks

Primary risks include:

- Hard-coding one calendar model into the framework
- Treating aging as ordinary calendar progression
- Creating nondeterministic catch-up behavior
- Requiring every elapsed minute to be processed individually
- Allowing schedule storms during fast-forward
- Letting pause behavior vary without clear rules
- Mixing time advancement with domain-specific outcomes

--------------------------------------------

# 17. Deferred Decisions

The following remain open:

- Real-time length of an in-game day
- Calendar data format
- Time-zone support
- Astronomy integration
- Daylight computation
- Exact pause behavior in multiplayer
- Threading
- Maximum fast-forward duration
- Scheduler persistence format

--------------------------------------------

# 18. Acceptance Criteria

SYS-003 is complete when it clearly defines:

- What the authoritative world timeline is
- What the Time Framework owns
- What it does not own
- Calendar configuration boundaries
- Time-scale and pause behavior
- Layered simulation scheduling
- Scheduled execution concepts
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
- [SYS-004 Region Framework](SYS-004_Region_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)
- [ADR-014 Hybrid Time](../Architecture/ADR/ADR-014_Hybrid_Time.md)
- [ADR-015 Time Pause Rules](../Architecture/ADR/ADR-015_Time_Pause_Rules.md)
- [ADR-020 Configurable Calendar](../Architecture/ADR/ADR-020_Configurable_Calendar.md)
- [ADR-024 M-001 Prototype Decision Governance and Test Tooling](../Architecture/ADR/ADR-024_M-001_Prototype_Decision_Governance_and_Test_Tooling.md)
