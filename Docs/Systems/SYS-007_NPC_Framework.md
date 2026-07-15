# SYS-007 — NPC Framework

- Document ID: SYS-007
- Title: NPC Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

--------------------------------------------

# 1. Purpose

The NPC Framework coordinates autonomous participation by non-player characters in the Mythos world.

It connects character identity and capabilities to schedules, goals, decisions, and simulation tiers without giving NPCs separate world rules from players. The framework remains setting-agnostic and does not define final AI algorithms or gameplay mechanics.

--------------------------------------------

# 2. Design Principles

1. Named NPCs are persistent characters with identities and purposes.
2. NPCs and players use shared world systems whenever practical.
3. NPC autonomy must operate without requiring player participation.
4. NPC behavior is composed from focused policies and data rather than deep inheritance.
5. Detailed local simulation and abstract distant simulation must produce compatible world outcomes.
6. Decisions must be deterministic when given the same state, ordered inputs, configuration, and random seed.
7. NPC knowledge and belief must remain distinct from authoritative world truth.
8. NPC purpose, schedule, needs, goals, and ambitions are distinct concepts.
9. The framework coordinates behavior but does not take ownership of other domains.
10. Behavior depth must scale without requiring every NPC to update every frame.

--------------------------------------------

# 3. Responsibilities

The NPC Framework owns:

- NPC autonomy state
- NPC purpose references
- Schedule-plan references and execution state
- Goal and ambition references
- Decision requests and selected intentions
- Behavior-policy references
- Simulation-tier participation
- NPC activation and abstraction coordination
- Interruption and replanning state
- Autonomous action requests through approved domain interfaces
- NPC-specific diagnostic state
- Deterministic decision context

--------------------------------------------

# 4. Non-Responsibilities

The NPC Framework does not own:

- Character identity, traits, skills, professions, or life stage
- Time progression
- Region hierarchy or movement resolution
- Relationships or reputation
- Economy, property, organization, or inventory rules
- Combat resolution
- Dialogue content or generation
- Quest logic
- Knowledge truth, information propagation, or memory storage
- Needs definitions or need-resolution rules
- Animation, rendering, navigation, or physics
- Setting-specific roles, schedules, or behaviors

The framework may use these domains through approved contracts but must not duplicate their authoritative state or rules.

--------------------------------------------

# 5. Public Concepts

## NPC Profile

A persistent autonomy record linked one-to-one with a valid Character entity.

Conceptual data may include:

- Character entity ID
- Autonomy status
- Purpose references
- Schedule-plan reference
- Active schedule entry
- Goal references
- Ambition references
- Behavior-policy references
- Current intention reference
- Simulation tier
- Interruption state
- Last decision timestamp
- Deterministic decision seed or stream reference

This document does not define a final schema.

## Purpose

A concise reference describing why an NPC participates in the world. A purpose may relate to occupation, family, community, culture, politics, religion, crime, education, or another approved domain.

Purpose is not a quest and does not require player interaction.

## Schedule Plan

A configurable plan describing expected activities or availability over time. Schedule execution requests actions from relevant systems; it does not perform travel, work, trade, or other domain behavior itself.

Schedule depth remains configurable and deferred.

## Goal

A desired outcome that may guide decisions over a limited or extended period.

## Ambition

A broad, long-term aspiration that may generate or rank goals. Not every NPC requires the same ambition depth.

## Intention

The currently selected course of action. An intention is not itself a completed world-state change.

## Behavior Policy

An extensible, data-driven rule or strategy used to evaluate available intentions. The final planning model remains deferred.

## Simulation Tier

A representation of the fidelity at which an NPC is currently simulated. At minimum, the design must support detailed active simulation and abstract inactive simulation without requiring fixed tier names or counts.

## Decision Context

A read-only view of approved information used to choose an intention, including relevant time, region, character state, knowledge, schedule, goals, and available actions.

--------------------------------------------

# 6. Conceptual Public Operations

The framework must eventually support operations equivalent to:

- Register NPC profile
- Retire NPC profile
- Query autonomy status
- Enable or suspend autonomy
- Assign or remove purpose
- Assign schedule plan
- Add, update, complete, fail, or abandon goal
- Add or remove ambition
- Register behavior policy
- Request decision
- Select intention
- Interrupt or cancel intention
- Request replanning
- Change simulation tier
- Activate detailed simulation
- Abstract NPC state
- Query current intention
- Inspect decision context and outcome

This document must not define language-specific method signatures.

--------------------------------------------

# 7. Decision and Action Flow

Conceptual flow:

1. Time, events, schedules, needs, or domain changes create a decision opportunity.
2. The NPC Framework builds a deterministic decision context from approved public data.
3. Applicable policies produce or rank valid intentions.
4. The framework selects an intention.
5. The intention requests an action through the owning domain.
6. The owning domain validates and resolves the action.
7. Published outcomes update NPC schedule, goal, or replanning state.

The NPC Framework must not directly mutate another domain's authoritative state.

--------------------------------------------

# 8. Events Published

The NPC Framework should eventually publish events such as:

- NPCRegistered
- NPCRetired
- NPCAutonomyChanged
- NPCPurposeChanged
- NPCScheduleChanged
- NPCGoalAdded
- NPCGoalCompleted
- NPCGoalFailed
- NPCAmbitionChanged
- NPCIntentionSelected
- NPCIntentionInterrupted
- NPCReplanRequested
- NPCSimulationTierChanged
- NPCDecisionFailed

Final event contracts belong to the Event Framework.

--------------------------------------------

# 9. Events Consumed

The framework may consume approved events related to:

- Time and scheduled updates
- Character status or lifecycle changes
- Region activation, abstraction, or assignment
- Action completion or failure
- Knowledge and memory changes
- Relationship and reputation changes
- Needs-state changes
- Goal-relevant domain outcomes

Subscriptions must remain explicit and must not transfer ownership of the publishing domain's data.

--------------------------------------------

# 10. Data Ownership

The NPC Framework owns only autonomy coordination data: purposes, schedule execution state, goals, ambitions, policy references, intentions, simulation-tier state, and decision diagnostics.

Character, region, time, knowledge, relationship, reputation, inventory, economy, organization, property, combat, and other domain state remain owned by their respective frameworks.

--------------------------------------------

# 11. Dependencies

Required conceptual dependencies:

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-004 Region Framework](SYS-004_Region_Framework.md)
- [SYS-005 Character Framework](SYS-005_Character_Framework.md)
- [SYS-006 Save and Persistence Framework](SYS-006_Save_and_Persistence_Framework.md)

Future public integrations may include Relationship, Reputation, Information, Economy, Organization, Property, Inventory, Combat, Dialogue, and Quest frameworks.

Circular dependencies are prohibited.

--------------------------------------------

# 12. Save Requirements

Save data must preserve:

- Character-to-NPC profile link
- Autonomy status
- Purpose references
- Schedule plan and execution state
- Goals and their state
- Ambition references
- Behavior-policy references
- Current intention where safe to resume
- Simulation tier
- Interruption and replanning state
- Deterministic decision state required for reproducibility
- Relevant decision timestamps

Loading must validate all references before autonomy resumes. Unsafe in-progress intentions must be canceled or reconstructed through an explicit policy rather than silently assumed complete.

--------------------------------------------

# 13. Simulation Behavior

- Active NPCs may evaluate decisions and execute actions at detailed frequencies.
- Abstract NPCs use batched or outcome-oriented updates appropriate to their simulation tier.
- Tier changes must preserve identity, persistent goals, and authoritative outcomes.
- Abstract simulation must not grant actions that the owning domain would reject under shared world rules.
- Catch-up processing must be bounded and deterministic.
- NPC autonomy must pause during save restoration and world-integrity validation.

--------------------------------------------

# 14. Performance Requirements

The framework should support:

- Large named-NPC populations
- Configurable decision frequencies
- Batch evaluation for abstract NPCs
- Region-scoped activation and abstraction
- Indexed queries by purpose, goal, schedule, and simulation tier
- Bounded catch-up after long time advances
- Disableable decision tracing
- Profiling by policy, region, and simulation tier

Exact targets will be established after engine selection and prototype profiling.

--------------------------------------------

# 15. Validation Rules

The framework must detect or prevent:

- NPC profiles without valid Character entities
- Duplicate NPC profiles for one Character entity
- Invalid purpose, schedule, goal, ambition, or policy references
- Intentions requiring unavailable capabilities
- Actions directed at invalid or unknown entities
- Illegal simulation-tier transitions
- Autonomous processing before load validation completes
- Unbounded replanning loops
- Non-deterministic ordering within authoritative decisions
- Direct mutation of another domain's authoritative data
- Resuming an invalid in-progress intention

--------------------------------------------

# 16. Error Handling

- Invalid decisions must return structured diagnostic results.
- A failed action request must not be treated as a successful world outcome.
- Policy failures should isolate the affected decision where safe and trigger explicit fallback or suspension behavior.
- Repeated decision failure must be observable to development tools.
- World-integrity failures must suspend unsafe autonomy rather than continue silently.

--------------------------------------------

# 17. Extension Points

Future titles may add:

- Purpose definitions
- Schedule-plan formats
- Goal and ambition libraries
- Behavior policies
- Decision evaluators
- Simulation-tier policies
- Action adapters
- Interruption and fallback policies
- Setting-specific routine data

Extensions should be data-driven and must use public domain contracts whenever practical.

--------------------------------------------

# 18. Debugging Requirements

Future implementation should provide tools to:

- Inspect an NPC profile by Entity ID
- View purpose, schedule, goals, ambitions, and current intention
- View simulation tier and last decision timestamp
- Explain why an intention was selected or rejected
- Trace action requests and outcomes
- Suspend and resume autonomy
- Force a safe replanning request
- Find invalid references
- Detect replanning loops
- Compare active and abstract outcomes
- Report decision cost by region and policy

--------------------------------------------

# 19. Test Requirements

Future implementation must include:

- Unit tests for autonomy state and policy selection boundaries
- Integration tests with Entity, Event, Time, Region, and Character frameworks
- Persistence round-trip tests
- Determinism tests using identical state and seeds
- Active-to-abstract transition tests
- Long time-advance and catch-up tests
- Invalid-reference and invalid-action tests
- Failure isolation and replanning-loop tests
- Tests confirming shared domain validation for player and NPC actions

--------------------------------------------

# 20. Risks

Primary risks include:

- Building a monolithic all-purpose AI manager
- Duplicating domain rules inside NPC behavior
- Simulating every NPC at full fidelity
- Allowing active and abstract simulation to produce incompatible outcomes
- Treating goals, needs, schedules, and traits as interchangeable
- Giving NPCs authoritative information they have not learned
- Creating non-deterministic decision order
- Producing unbounded replanning or event loops
- Coupling behavior policies to one setting or engine

--------------------------------------------

# 21. Deferred Decisions

The following remain open:

- Schedule depth
- Needs simulation
- Memory duration and implementation
- Emotional modeling
- Goal-planning algorithm
- Goal generation
- Ambition depth
- Number and names of simulation tiers
- Decision frequency by tier
- Navigation and movement integration
- Action-selection scoring model
- Conflict resolution between schedule, needs, and goals
- Social and group decision-making
- Generic unnamed NPC treatment
- Population generation rules
- Engine and programming language

Every deferred item remains deferred, not rejected.

--------------------------------------------

# 22. Acceptance Criteria

SYS-007 is complete when it clearly defines:

- NPC Framework purpose and ownership boundaries
- Shared player and NPC world rules
- Purpose, schedule, goal, ambition, intention, and policy concepts
- Deterministic decision and action flow
- Simulation-tier compatibility
- Events and dependencies
- Persistence and restoration behavior
- Validation and failure handling
- Performance, debugging, and test expectations
- Extension points
- Deferred AI and gameplay decisions

--------------------------------------------

# 23. Related Documents

- [SD-001 Project Charter](../Executive/SD-001_Project_Charter.md)
- [SD-002 Framework Overview](../Executive/SD-002_Framework_Overview.md)
- [SD-004 Open Questions](../Executive/SD-004_Open_Questions.md)
- [SD-005 Development Roadmap](../Executive/SD-005_Development_Roadmap.md)
- [STD-001 Technical Architecture Standards](../Architecture/STD-001_Technical_Architecture_Standards.md)
- [ADR-006 Player Independence](../Architecture/ADR/ADR-006_Player_Independence.md)
- [ADR-008 Hybrid World Simulation](../Architecture/ADR/ADR-008_Hybrid_World_Simulation.md)
- [ADR-011 Purpose-Driven NPCs](../Architecture/ADR/ADR-011_Purpose_Driven_NPCs.md)
- [ADR-013 Shared World Rules](../Architecture/ADR/ADR-013_Shared_World_Rules.md)
- [ADR-016 NPC Goals and Ambitions](../Architecture/ADR/ADR-016_NPC_Goals_and_Ambitions.md)
- [ADR-018 Information and Knowledge](../Architecture/ADR/ADR-018_Information_and_Knowledge.md)
- [ADR-021 Event-Driven World State](../Architecture/ADR/ADR-021_Event_Driven_World_State.md)
