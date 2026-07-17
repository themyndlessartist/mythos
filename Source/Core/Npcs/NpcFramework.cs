using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Regions;
using Mythos.Framework.Time;

namespace Mythos.Framework.Npcs;

/// <summary>Owns the minimal deterministic NPC autonomy fixture required by M-001.</summary>
public sealed class NpcFramework
{
    private readonly EntityRegistry entities;
    private readonly CharacterRegistry characters;
    private readonly RegionFramework regions;
    private readonly INpcReferenceProvider references;
    private Dictionary<EntityId, NpcProfileSnapshot> profiles = [];

    public NpcFramework(EntityRegistry entities, CharacterRegistry characters, RegionFramework regions, INpcReferenceProvider references)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
        this.references = references ?? throw new ArgumentNullException(nameof(references));
    }

    public int Count => profiles.Count;

    public NpcResult<NpcProfileSnapshot> Register(NpcProfileSnapshot? profile)
    {
        if (profile is null) return NpcResult<NpcProfileSnapshot>.Failure(NpcErrorCodes.InvalidSnapshot, "NPC profile cannot be null.");
        if (profiles.ContainsKey(profile.CharacterEntityId))
            return NpcResult<NpcProfileSnapshot>.Failure(NpcErrorCodes.DuplicateProfile, "An NPC profile already exists for the Character entity.");
        var validation = ValidateOperationalProfile(profile);
        if (!validation.IsSuccess) return NpcResult<NpcProfileSnapshot>.Failure(validation.Error!.Code, validation.Error.Message);
        profiles.Add(profile.CharacterEntityId, profile);
        return NpcResult<NpcProfileSnapshot>.Success(profile);
    }

    public NpcResult<NpcProfileSnapshot> Find(EntityId characterEntityId) => profiles.TryGetValue(characterEntityId, out var profile)
        ? NpcResult<NpcProfileSnapshot>.Success(profile)
        : NpcResult<NpcProfileSnapshot>.Failure(NpcErrorCodes.ProfileNotFound, "NPC profile was not found.");

    public NpcResult<NpcUpdateResult> Update(EntityId characterEntityId, WorldTimestamp through, int maximumTransitions)
    {
        if (maximumTransitions <= 0)
            return NpcResult<NpcUpdateResult>.Failure(NpcErrorCodes.InvalidState, "Maximum transitions must be positive.");
        if (!profiles.TryGetValue(characterEntityId, out var profile))
            return NpcResult<NpcUpdateResult>.Failure(NpcErrorCodes.ProfileNotFound, "NPC profile was not found.");
        var validation = ValidateOperationalProfile(profile);
        if (!validation.IsSuccess) return NpcResult<NpcUpdateResult>.Failure(validation.Error!.Code, validation.Error.Message);

        var schedule = references.FindSchedule(profile.ScheduleId)!;
        var current = profile;
        var processed = 0;
        while (current.NextDueAt.Value <= through.Value && processed < maximumTransitions)
        {
            var nextIndex = (current.CurrentScheduleEntryIndex + 1) % schedule.Entries!.Count;
            var nextEntry = schedule.Entries[nextIndex];
            long nextDue;
            try { nextDue = checked(current.NextDueAt.Value + nextEntry.Duration.Value); }
            catch (OverflowException) { return NpcResult<NpcUpdateResult>.Failure(NpcErrorCodes.InvalidState, "NPC schedule timestamp overflowed."); }
            if (current.CompletedTransitions == long.MaxValue)
                return NpcResult<NpcUpdateResult>.Failure(NpcErrorCodes.InvalidState, "NPC transition count is exhausted.");
            current = current with
            {
                CurrentScheduleEntryIndex = nextIndex,
                CurrentScheduleStateId = nextEntry.StateId,
                NextDueAt = new WorldTimestamp(nextDue),
                CompletedTransitions = current.CompletedTransitions + 1,
            };
            processed++;
        }

        profiles[characterEntityId] = current;
        return NpcResult<NpcUpdateResult>.Success(new NpcUpdateResult(current, processed, current.NextDueAt.Value <= through.Value));
    }

    public NpcResult SetSimulationTier(EntityId characterEntityId, NpcSimulationTier tier)
    {
        if (!Enum.IsDefined(tier)) return NpcResult.Failure(NpcErrorCodes.InvalidState, "NPC simulation tier is invalid.");
        if (!profiles.TryGetValue(characterEntityId, out var profile)) return NpcResult.Failure(NpcErrorCodes.ProfileNotFound, "NPC profile was not found.");
        var validation = ValidateOperationalProfile(profile);
        if (!validation.IsSuccess) return validation;
        profiles[characterEntityId] = profile with { SimulationTier = tier };
        return NpcResult.Success();
    }

    public NpcResult ValidateReferences()
    {
        foreach (var profile in profiles.Values.OrderBy(item => item.CharacterEntityId.Value))
        {
            var result = ValidateOperationalProfile(profile);
            if (!result.IsSuccess) return result;
        }
        return NpcResult.Success();
    }

    public NpcResult<NpcDiagnostic> Inspect(EntityId characterEntityId)
    {
        if (!profiles.TryGetValue(characterEntityId, out var profile)) return NpcResult<NpcDiagnostic>.Failure(NpcErrorCodes.ProfileNotFound, "NPC profile was not found.");
        var validation = ValidateOperationalProfile(profile);
        return NpcResult<NpcDiagnostic>.Success(new NpcDiagnostic(profile.CharacterEntityId, profile.PurposeId, profile.ScheduleId,
            profile.CurrentScheduleStateId, profile.NextDueAt, profile.SimulationTier, profile.CompletedTransitions,
            validation.IsSuccess ? "valid" : $"{validation.Error!.Code}: {validation.Error.Message}"));
    }

    public NpcFrameworkSnapshot ExportSnapshot() => new(NpcFrameworkSnapshot.CurrentVersion,
        profiles.Values.OrderBy(profile => profile.CharacterEntityId.Value).ToArray());

    public NpcResult RestoreSnapshot(NpcFrameworkSnapshot? snapshot)
    {
        if (snapshot is null) return NpcResult.Failure(NpcErrorCodes.InvalidSnapshot, "NPC snapshot cannot be null.");
        if (snapshot.Version != NpcFrameworkSnapshot.CurrentVersion)
            return NpcResult.Failure(NpcErrorCodes.UnsupportedSnapshotVersion, "NPC snapshot version is unsupported.");
        if (snapshot.Profiles is null || snapshot.Profiles.Any(profile => profile is null))
            return NpcResult.Failure(NpcErrorCodes.InvalidSnapshot, "NPC snapshot profiles cannot be null or contain null entries.");
        var candidate = new Dictionary<EntityId, NpcProfileSnapshot>();
        foreach (var profile in snapshot.Profiles)
        {
            if (!candidate.TryAdd(profile.CharacterEntityId, profile)) return NpcResult.Failure(NpcErrorCodes.DuplicateProfile, "NPC snapshot contains duplicate Character links.");
            var validation = ValidateProfile(profile);
            if (!validation.IsSuccess) return validation;
        }
        profiles = candidate;
        return NpcResult.Success();
    }

    private NpcResult ValidateProfile(NpcProfileSnapshot profile)
    {
        if (profile.CharacterEntityId.Value == Guid.Empty || !Initialized(profile.PurposeId.Value) ||
            !Initialized(profile.ScheduleId.Value) || !Initialized(profile.CurrentScheduleStateId.Value))
            return NpcResult.Failure(NpcErrorCodes.InvalidIdentifier, "NPC identifiers must be initialized and normalized.");
        if (!Enum.IsDefined(profile.SimulationTier) || profile.CompletedTransitions < 0)
            return NpcResult.Failure(NpcErrorCodes.InvalidState, "NPC lifecycle or transition state is invalid.");
        var entity = entities.Find(profile.CharacterEntityId);
        if (!entity.IsSuccess || entity.Value!.LifecycleState != EntityLifecycleState.Active)
            return NpcResult.Failure(NpcErrorCodes.InvalidLifecycle, "NPC requires an active Character Entity.");
        if (!characters.Find(profile.CharacterEntityId).IsSuccess)
            return NpcResult.Failure(NpcErrorCodes.InvalidReference, "NPC requires a matching Character profile.");
        if (entity.Value.RegionId is not { } regionId || !regions.Find(regionId).IsSuccess)
            return NpcResult.Failure(NpcErrorCodes.InvalidReference, "NPC requires a valid Region assignment.");
        if (!references.IsKnownPurpose(profile.PurposeId))
            return NpcResult.Failure(NpcErrorCodes.InvalidReference, "NPC purpose reference was not found.");
        var schedule = references.FindSchedule(profile.ScheduleId);
        if (!ValidSchedule(schedule) || schedule!.Id != profile.ScheduleId)
            return NpcResult.Failure(NpcErrorCodes.InvalidSchedule, "NPC schedule definition is missing, mismatched, or malformed.");
        if (profile.CurrentScheduleEntryIndex < 0 || profile.CurrentScheduleEntryIndex >= schedule!.Entries!.Count ||
            schedule.Entries[profile.CurrentScheduleEntryIndex].StateId != profile.CurrentScheduleStateId)
            return NpcResult.Failure(NpcErrorCodes.InvalidState, "NPC schedule execution state does not match its definition.");
        return NpcResult.Success();
    }

    private NpcResult ValidateOperationalProfile(NpcProfileSnapshot profile)
    {
        var characterResult = characters.ValidateReferences();
        if (!characterResult.IsSuccess) return NpcResult.Failure(NpcErrorCodes.InvalidReference, characterResult.Error!.Message);
        var regionResult = regions.ValidateReferences();
        if (!regionResult.IsSuccess) return NpcResult.Failure(NpcErrorCodes.InvalidReference, regionResult.Error!.Message);
        return ValidateProfile(profile);
    }

    private static bool ValidSchedule(NpcScheduleDefinition? schedule) => schedule is not null && Initialized(schedule.Id.Value) &&
        schedule.Entries is { Count: > 0 } && schedule.Entries.All(entry => entry is not null && Initialized(entry.StateId.Value) && entry.Duration.Value > 0) &&
        schedule.Entries.Select(entry => entry.StateId).Distinct().Count() == schedule.Entries.Count;

    private static bool Initialized(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
}
