# SYS-006 — Save and Persistence Framework

- Document ID: SYS-006
- Title: Save and Persistence Framework
- Version: 0.1
- Status: Approved
- Owner: Mythos Executive Development
- Last Updated: July 2026

--------------------------------------------

# 1. Purpose

The Save and Persistence Framework preserves authoritative Mythos world state across sessions.

It must support persistent entities, characters, regions, time, schedules, historical references, and future framework domains without coupling the save format to one game setting, engine, or implementation model.

--------------------------------------------

# 2. Design Principles

1. Save data represents authoritative world state, not temporary presentation state.
2. Persistent IDs must remain stable across saves and loads.
3. Save architecture must support long-running worlds and future framework expansion.
4. The framework must support versioning and migration.
5. Systems own their domain data; the Persistence Framework coordinates storage and restoration.
6. Save files must remain recoverable, inspectable, and diagnosable.
7. Partial corruption should be detected and reported.
8. Autosave and manual save behavior must remain configurable by title.
9. Save operations must avoid blocking gameplay longer than necessary.
10. The specification must remain engine-agnostic and language-agnostic.

--------------------------------------------

# 3. Responsibilities

The Save and Persistence Framework owns:

- Save orchestration
- Load orchestration
- Save-slot management
- Save metadata
- Save-version metadata
- Framework-state registration
- Serialization coordination
- Deserialization coordination
- Reference restoration
- Save validation
- Migration coordination
- Backup creation
- Corruption detection
- Recovery workflow
- Autosave scheduling hooks
- Checkpoint support
- Save diagnostics
- Save compatibility reporting
- Atomic save completion where practical

--------------------------------------------

# 4. Non-Responsibilities

The Save and Persistence Framework does not own:

- Entity domain rules
- Character progression
- NPC behavior
- Region logic
- Economy logic
- Quest logic
- Combat state rules
- Rendering state
- Engine scene restoration
- Temporary UI state unless explicitly approved
- Final database technology
- Final serialization format
- Cloud synchronization
- Multiplayer synchronization

Each domain system remains responsible for defining the data it owns and validating its own domain state.

--------------------------------------------

# 5. Public Concepts

## Save Slot

A player-facing or system-facing container representing one persistent world state.

Conceptual fields may include:

- Slot identifier
- Display name
- World identifier
- Character identifier or identifiers
- Save timestamp
- In-world timestamp
- Playtime
- Save version
- Title identifier
- Framework version
- Thumbnail or preview reference, optional
- Compatibility status
- Corruption status
- Build identifier
- Mod or content-package manifest reference

## Save Manifest

A top-level description of the save.

Conceptual fields:

- Save format version
- Framework version
- Game title identifier
- World identifier
- Active content packages
- Active mods, if supported
- Schema or domain versions
- Created timestamp
- Last saved timestamp
- Checksums or integrity metadata
- Partition list
- Migration history
- Required dependencies

Do not define a final schema.

## Domain Snapshot

A serialized unit of state owned by one framework or domain.

Examples:

- Entity state
- Event queue state
- Time state
- Region state
- Character state
- Economy state
- Organization state

Domain snapshots must declare their own version.

## Save Transaction

A coordinated save operation that gathers validated domain snapshots and commits them as one consistent world state.

## Load Transaction

A coordinated load operation that validates compatibility, restores domains in dependency order, reconnects references, and reports failures.

## Migration

A controlled transformation from an older save-data version to a newer version.

## Backup

A preserved prior save state used for recovery.

## Recovery Mode

A diagnostic or repair path used when a normal load cannot safely complete.

## Save Scope

The framework must support:

- Manual saves
- Autosaves
- Checkpoints
- Rotating backups
- Developer snapshots
- Test fixtures
- Crash-recovery saves where practical

Titles may enable or disable specific save types.

--------------------------------------------

# 6. Conceptual Public Operations

The framework must eventually support operations equivalent to:

- Register persistent domain
- Create save slot
- Delete save slot
- Rename save slot
- Save world
- Autosave world
- Create checkpoint
- List save slots
- Read save metadata
- Validate save
- Load save
- Restore backup
- Migrate save
- Export diagnostics
- Compare save compatibility
- Mark save corrupted
- Enter recovery mode
- Enumerate domain versions
- Enumerate unresolved references

Do not define language-specific method signatures.

--------------------------------------------

# 7. Events Published

The Save and Persistence Framework should eventually publish events such as:

- SaveStarted
- SaveCompleted
- SaveFailed
- AutosaveStarted
- AutosaveCompleted
- LoadStarted
- LoadCompleted
- LoadFailed
- SaveValidationFailed
- MigrationStarted
- MigrationCompleted
- MigrationFailed
- BackupCreated
- RecoveryStarted
- RecoveryCompleted
- RecoveryFailed

Final event contracts belong to the Event Framework.

--------------------------------------------

# 8. Events Consumed

The Save and Persistence Framework may consume:

- AutosaveRequested
- CheckpointRequested
- ApplicationClosing
- WorldTransitionRequested
- CrashRecoveryRequested
- FrameworkStateChanged, if future dirty-state tracking is approved

It must not absorb gameplay responsibilities from domain systems.

--------------------------------------------

# 9. Data Ownership

The Save and Persistence Framework owns:

- Save manifests
- Save-slot metadata
- Domain registration metadata
- Migration registry
- Backup metadata
- Recovery metadata
- Integrity metadata
- Save diagnostics

It does not own domain gameplay state.

--------------------------------------------

# 10. Dependencies

Required conceptual dependencies:

- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-004 Region Framework](SYS-004_Region_Framework.md)
- [SYS-005 Character Framework](SYS-005_Character_Framework.md)
- Configuration/Data Framework
- Storage adapter or platform file service

Future dependencies may include:

- Mod Framework
- Content Package Framework
- Cloud Save Integration
- Analytics or crash-reporting integration

Circular dependencies must be avoided.

--------------------------------------------

# 11. Save Requirements

## Save Ordering

Loading should restore systems in a controlled dependency order.

Initial conceptual order:

1. Save manifest and compatibility validation
2. Core configuration references
3. Entity identity registry
4. Event Framework state required for pending durable work
5. Time Framework
6. Region Framework
7. Character Framework
8. Other domain frameworks
9. Cross-domain reference restoration
10. Validation pass
11. Simulation resume

The exact order may be refined as more systems are specified.

## Reference Restoration

The framework must preserve and restore:

- Entity references
- Parent and child links
- Ownership links
- Region assignments
- Component references
- Character references
- Scheduled task references
- Historical references
- Future relationship, reputation, property, and organization references

Missing references must be reported.

The framework must not silently generate replacement IDs for missing persistent entities.

## Save Consistency

A completed save should represent one coherent world state.

The framework should support:

- Snapshot boundaries
- Write-to-temporary then commit
- Integrity checks
- Failure rollback
- Backup before overwrite
- Detection of incomplete save operations

Exact mechanisms remain implementation decisions.

## Versioning

Every persistent domain must declare:

- Domain identifier
- Domain version
- Minimum supported version
- Migration availability
- Compatibility status

Save versioning must distinguish:

- Framework version
- Game-title version
- Domain-data versions
- Content-package or mod versions
- Build version

## Migration Rules

Migrations must:

- Be explicit
- Be ordered
- Be testable
- Preserve original backups
- Report data loss
- Refuse unsafe migration
- Support dry-run validation where practical
- Never silently discard unknown data

## Save Requirements for Existing Systems

### Entity Framework

Persist:

- Entity IDs
- Categories
- Tags
- Lifecycle states
- Hierarchy
- Ownership
- Region assignment
- Component references
- Creation and retirement timestamps

### Event Framework

Persist only approved durable event state, such as:

- Pending durable events
- Delayed events
- Correlation metadata required for restoration

Temporary diagnostic events are not normally persistent.

### Time Framework

Persist:

- Authoritative world timestamp
- Calendar identifier and version
- Time scale
- Pause state where appropriate
- Pending schedules
- Recurrence state
- Simulation progress markers

### Region Framework

Persist:

- Region entities
- Hierarchy
- Adjacency
- Boundaries or references
- Activation state
- Simulation ownership
- Assigned entity references

### Character Framework

Persist:

- Character-to-entity links
- Identity
- Life-stage data
- Traits
- Tendencies
- Skills
- Professions
- Active profession state
- Class references
- Knowledge and memory references
- Status
- Personal-history references

--------------------------------------------

# 12. Performance Requirements

The framework should support:

- Large world states
- Incremental or partitioned saves
- Background preparation where safe
- Bounded gameplay interruption
- Regional partitioning
- Batch serialization
- Lazy loading where approved
- Save-slot metadata access without loading the world
- Developer profiling
- Deterministic save and load tests

Exact performance targets will be established after engine selection and prototype profiling.

## Partitioning

The architecture should support future save partitioning by:

- Framework domain
- Region
- Entity range
- Content package
- Historical archive
- Active versus abstract simulation state

The initial prototype may use a simpler representation, but it must not prevent later partitioning.

--------------------------------------------

# 13. Validation Rules

The framework must detect or prevent:

- Duplicate save-slot IDs
- Duplicate world IDs
- Unsupported save versions
- Missing required domain snapshots
- Invalid dependency order
- Incomplete transactions
- Broken persistent references
- Duplicate entity IDs
- Unsafe migration paths
- Save overwrite without required backup
- Loading incompatible content packages without warning
- Resume of simulation before validation completes

## Corruption Detection

The framework should detect:

- Missing required partitions
- Invalid manifest data
- Checksum failures
- Duplicate entity IDs
- Broken references
- Unsupported versions
- Missing content packages
- Partial writes
- Invalid timestamps
- Invalid domain versions
- Failed migrations

## Recovery Behavior

Recovery may include:

- Restore latest valid backup
- Load with non-critical domains disabled
- Produce a diagnostic report
- Isolate corrupted partitions
- Attempt safe reference repair
- Refuse load when world integrity cannot be preserved

The framework must clearly distinguish repaired, partially recovered, and fully valid saves.

--------------------------------------------

# 14. Extension Points

Future titles may add:

- New persistent domains
- New save-slot metadata
- New partition types
- New migration handlers
- New backup policies
- New storage adapters
- Cloud save adapters
- Console platform storage
- Mod manifests
- Multiplayer world-state persistence
- Server-authoritative persistence

Extensions must not require rewriting the core save orchestration whenever practical.

--------------------------------------------

# 15. Debugging Requirements

Future implementation should provide tools to:

- Inspect save manifest
- Inspect domain versions
- Validate references
- Compare two saves
- Export save summaries
- List unresolved references
- Test migration paths
- Simulate corruption
- Restore backups
- Report save size by domain
- Report load time by domain
- Identify incompatible content packages
- Produce machine-readable diagnostics

--------------------------------------------

# 16. Security and Safety Requirements

The framework should:

- Avoid executable content in saves
- Treat external or modified saves as untrusted input
- Validate all serialized data before use
- Prevent path traversal
- Avoid writing outside approved save locations
- Avoid exposing credentials or platform secrets
- Sanitize player-supplied save names
- Preserve user backups before destructive migration

--------------------------------------------

# 17. Risks

Primary risks include:

- Coupling save format directly to runtime classes
- Loading domains in the wrong order
- Silent data loss during migration
- Very large monolithic save files
- Broken cross-domain references
- Save corruption during interruption
- Excessive autosave stalls
- Engine-specific assumptions leaking into domain data
- Treating save compatibility as an afterthought
- Attempting full event sourcing without explicit approval

--------------------------------------------

# 18. Deferred Decisions

The following remain open:

- Final serialization format
- Database versus file-based storage
- Binary versus text representation
- Compression
- Encryption
- Save location
- Autosave frequency
- Maximum backup count
- Incremental save strategy
- Region partitioning strategy
- Lazy loading
- Cloud saves
- Cross-platform compatibility
- Mod compatibility rules
- Event-sourcing usage
- Recovery UI
- Save-slot limits
- Ironman or restricted-save modes

--------------------------------------------

# 19. Acceptance Criteria

SYS-006 is complete when it clearly defines:

- Persistence ownership boundaries
- Save and load orchestration
- Save manifest requirements
- Domain snapshot model
- Versioning
- Migration
- Backups
- Corruption detection
- Recovery
- Reference restoration
- Save ordering
- Existing-framework persistence requirements
- Performance expectations
- Validation
- Extension points
- Security requirements
- Deferred implementation decisions

--------------------------------------------

# 20. Cross-References

- [SD-001 Project Charter](../Executive/SD-001_Project_Charter.md)
- [SD-002 Framework Overview](../Executive/SD-002_Framework_Overview.md)
- [SD-004 Open Questions](../Executive/SD-004_Open_Questions.md)
- [SD-005 Development Roadmap](../Executive/SD-005_Development_Roadmap.md)
- [SYS-001 Entity Framework](SYS-001_Entity_Framework.md)
- [SYS-002 Event Framework](SYS-002_Event_Framework.md)
- [SYS-003 Time Framework](SYS-003_Time_Framework.md)
- [SYS-004 Region Framework](SYS-004_Region_Framework.md)
- [SYS-005 Character Framework](SYS-005_Character_Framework.md)
- [ADR-008 Hybrid World Simulation](../Architecture/ADR/ADR-008_Hybrid_World_Simulation.md)
- [ADR-017 Hierarchical Regions](../Architecture/ADR/ADR-017_Hierarchical_Regions.md)
- [ADR-019 Unified Entity Model](../Architecture/ADR/ADR-019_Unified_Entity_Model.md)
- [ADR-021 Event-Driven World State](../Architecture/ADR/ADR-021_Event_Driven_World_State.md)
- [ADR-022 Persistent World History](../Architecture/ADR/ADR-022_Persistent_World_History.md)
