# SYS-005 — Character Framework

- Document ID: SYS-005
- Title: Character Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

--------------------------------------------

# 1. Purpose

The Character Framework represents person-like entities as persistent individuals with identity, traits, capabilities, progression, knowledge, and life-state data.

Players and named NPCs use the same Character Framework.

--------------------------------------------

# 2. Design Principles

1. Characters are people first, not combat stat blocks.
2. Player and NPC characters share the same foundation.
3. Traits, tendencies, goals, relationships, and reputation remain distinct concepts.
4. Professions and classes are separate.
5. Multiple professions are supported.
6. Profession changes do not delete prior learning.
7. Mastery limits are based on time and effort, not arbitrary permanent locks.
8. Character data should influence at least one gameplay or simulation system.
9. Setting-specific biological, cultural, or role data must extend the framework through configuration or modules.
10. The design must not require a specific engine or programming language.

--------------------------------------------

# 3. Responsibilities

The Character Framework owns:

- Character identity data
- Biological or body-profile data where appropriate
- Age and life-stage references
- Traits
- Tendencies
- Skills
- Professions
- Classes or combat-role references
- Knowledge references
- Memory hooks
- Needs-state hooks
- Personal history references
- Character progression data
- Character status
- Character creation and retirement integration

--------------------------------------------

# 4. Non-Responsibilities

The Character Framework does not own:

- NPC decision-making
- AI planning
- Combat resolution
- Inventory contents
- Relationships
- Reputation
- Dialogue generation
- Economy
- Faction membership
- Property ownership
- Rendering
- Animation

The Character Framework stores or exposes character-related data but does not decide autonomous behavior.

--------------------------------------------

# 5. Public Concepts

## Character Profile

Conceptual fields may include:

- Entity ID
- Name or naming data
- Birth or creation timestamp
- Age or life-stage reference
- Body or species profile
- Culture reference
- Citizenship reference
- Religion reference
- Traits
- Tendencies
- Skills
- Learned professions
- Active profession or professions
- Class or combat-role references
- Knowledge references
- Memory references
- Needs-state references
- Goals and ambitions references
- Status
- Personal-history references

Do not create a final schema.

## Trait

A relatively stable characteristic influencing behavior or system reactions.

Traits may be beneficial, harmful, mixed, or contextual.

## Tendency

A behavioral inclination or preference, distinct from a stable trait.

## Skill

A learned capability with progression.

## Profession

A learned occupational discipline.

Characters may learn multiple professions and change which they actively practice.

## Class or Combat Role

A combat-oriented identity separate from profession.

## Knowledge

Information the character knows or believes, integrated later with the Information Framework.

## Memory Hook

A reference allowing another system to store relevant remembered events.

## Life Stage

A configurable stage such as child, adult, or elder.

Exact aging rules remain deferred.

--------------------------------------------

# 6. Conceptual Public Operations

The framework must eventually support operations equivalent to:

- Create character profile
- Query character identity
- Add or remove trait
- Add or modify tendency
- Learn skill
- Advance skill
- Learn profession
- Set active profession
- Add class or combat-role reference
- Add knowledge reference
- Add memory reference
- Change life stage
- Change status
- Retire character
- Query capabilities

This document must not define language-specific method signatures.

--------------------------------------------

# 7. Events Published

The Character Framework should eventually publish events such as:

- CharacterCreated
- CharacterRetired
- CharacterStatusChanged
- CharacterLifeStageChanged
- TraitAdded
- TraitRemoved
- TendencyChanged
- SkillLearned
- SkillAdvanced
- ProfessionLearned
- ActiveProfessionChanged
- ClassChanged
- KnowledgeAdded
- MemoryReferenceAdded

Final event contracts belong to the Event Framework.

--------------------------------------------

# 8. Events Consumed

The Character Framework should consume only events necessary to maintain approved character identity, lifecycle, status, progression, or reference state.

It must not absorb NPC decision-making, combat, relationship, reputation, economy, or dialogue responsibilities.

--------------------------------------------

# 9. Data Ownership

The Character Framework owns character profile data, trait references, tendency references, skill and profession progress, class or combat-role references, knowledge and memory references, needs-state hooks, status, and personal-history references.

It does not own the domain behavior implemented by systems referenced from the character profile.

--------------------------------------------

# 10. Dependencies

Required conceptual dependencies:

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md) for timestamps and future aging
- Save/Persistence Framework
- Information Framework for full knowledge and belief behavior
- NPC Framework for autonomous use
- Relationship Framework for interpersonal links

Circular dependencies must be avoided.

--------------------------------------------

# 11. Save Requirements

Save data must preserve:

- Character-to-entity link
- Identity
- Life-stage data
- Traits
- Tendencies
- Skills and progress
- Professions
- Active profession state
- Class or combat-role references
- Knowledge and memory references
- Status
- Personal-history references

Loading must restore character records without generating new entity IDs for existing characters.

--------------------------------------------

# 12. Performance Requirements

The framework should support:

- Large named-character populations
- Inactive or abstract character records
- Efficient trait and skill queries
- Batch population updates
- Tiered simulation compatibility
- Debug inspection

Exact performance targets will be established after engine selection and prototyping.

--------------------------------------------

# 13. Validation Rules

The framework must detect or prevent:

- Character records without valid entity IDs
- Invalid trait references
- Invalid profession references
- Invalid skill progression
- Duplicate unique identity records
- Illegal life-stage transitions
- Invalid active professions
- Missing knowledge or memory references
- Domain data stored in the wrong framework

--------------------------------------------

# 14. Extension Points

Future Mythos titles may add:

- Species or body profiles
- Cultural identity fields
- Additional trait libraries
- New skills
- New professions
- New class systems
- New life stages
- New needs
- New knowledge categories

Extensions should be data-driven whenever practical.

--------------------------------------------

# 15. Debugging Requirements

Future implementation should provide developer tools to:

- Inspect a character by entity ID
- View character identity data
- View traits and tendencies
- View skills and progression
- View professions and active profession state
- View class or combat-role references
- View knowledge and memory references
- View life stage and status
- Find invalid references
- Export character summaries

--------------------------------------------

# 16. Risks

Primary risks include:

- Reducing characters to combat stat blocks
- Diverging player and NPC foundations without approval
- Confusing professions with classes or combat roles
- Mixing goals, relationships, reputation, and traits into one concept
- Storing domain behavior inside character identity data
- Hard-coding setting-specific biological or cultural assumptions
- Overcommitting to aging, needs, memory, or emotion models too early

--------------------------------------------

# 17. Deferred Decisions

The following remain open:

- Exact attributes and statistics
- Aging rate
- Player natural death
- Birth rules
- Species model
- Gender model
- Needs simulation depth
- Memory implementation
- Knowledge storage
- Emotional modeling
- Goal storage
- Skill progression formulas
- Profession mastery pacing
- Class architecture
- Health model

--------------------------------------------

# 18. Acceptance Criteria

SYS-005 is complete when it clearly defines:

- What a character is
- What the Character Framework owns
- What it does not own
- Character identity requirements
- Trait and tendency boundaries
- Skill and profession boundaries
- Class or combat-role boundaries
- Knowledge and memory reference boundaries
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
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [ADR-004 Character Identity](../Architecture/ADR/ADR-004_Character_Identity.md)
- [ADR-010 Unlimited Growth](../Architecture/ADR/ADR-010_Unlimited_Growth.md)
- [ADR-011 Purpose-Driven NPCs](../Architecture/ADR/ADR-011_Purpose_Driven_NPCs.md)
- [ADR-013 Shared World Rules](../Architecture/ADR/ADR-013_Shared_World_Rules.md)
- [ADR-016 NPC Goals and Ambitions](../Architecture/ADR/ADR-016_NPC_Goals_and_Ambitions.md)
- [ADR-018 Information and Knowledge](../Architecture/ADR/ADR-018_Information_and_Knowledge.md)
