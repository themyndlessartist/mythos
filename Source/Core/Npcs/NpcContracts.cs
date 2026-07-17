using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Npcs;

public readonly record struct NpcPurposeId
{
    public NpcPurposeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct NpcScheduleId
{
    public NpcScheduleId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct NpcScheduleStateId
{
    public NpcScheduleStateId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public enum NpcSimulationTier
{
    Active,
    Abstract,
}

public sealed record NpcScheduleEntry(NpcScheduleStateId StateId, WorldDuration Duration);

public sealed record NpcScheduleDefinition
{
    public NpcScheduleDefinition(NpcScheduleId id, IReadOnlyList<NpcScheduleEntry>? entries)
    {
        Id = id;
        Entries = entries is null ? null : Array.AsReadOnly(entries.ToArray());
    }

    public NpcScheduleId Id { get; }
    public IReadOnlyList<NpcScheduleEntry>? Entries { get; }
}

/// <summary>Validates externally owned, data-defined NPC references.</summary>
public interface INpcReferenceProvider
{
    bool IsKnownPurpose(NpcPurposeId purposeId);
    NpcScheduleDefinition? FindSchedule(NpcScheduleId scheduleId);
}

public sealed record NpcProfileSnapshot(
    EntityId CharacterEntityId,
    NpcPurposeId PurposeId,
    NpcScheduleId ScheduleId,
    NpcScheduleStateId CurrentScheduleStateId,
    int CurrentScheduleEntryIndex,
    WorldTimestamp NextDueAt,
    NpcSimulationTier SimulationTier,
    long CompletedTransitions);

public sealed record NpcFrameworkSnapshot
{
    public const int CurrentVersion = 1;

    public NpcFrameworkSnapshot(int version, IReadOnlyList<NpcProfileSnapshot>? profiles)
    {
        Version = version;
        Profiles = profiles is null ? null : Array.AsReadOnly(profiles.ToArray());
    }

    public int Version { get; }
    public IReadOnlyList<NpcProfileSnapshot>? Profiles { get; }
}

public sealed record NpcUpdateResult(
    NpcProfileSnapshot Profile,
    int ProcessedTransitions,
    bool CatchUpLimitReached);

public sealed record NpcDiagnostic(
    EntityId CharacterEntityId,
    NpcPurposeId PurposeId,
    NpcScheduleId ScheduleId,
    NpcScheduleStateId CurrentScheduleStateId,
    WorldTimestamp NextDueAt,
    NpcSimulationTier SimulationTier,
    long CompletedTransitions,
    string ReferenceStatus);
