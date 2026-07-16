using Mythos.Framework.Entities;

namespace Mythos.Framework.Regions;

public readonly record struct RegionCategory
{
    public RegionCategory(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public enum RegionSimulationState
{
    Active,
    Abstract,
}

public sealed record RegionAdjacencySnapshot
{
    public RegionAdjacencySnapshot(EntityId regionId, EntityId adjacentRegionId, IReadOnlyDictionary<string, string>? metadata)
    {
        RegionId = regionId;
        AdjacentRegionId = adjacentRegionId;
        Metadata = metadata is null ? null : new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(metadata, StringComparer.Ordinal));
    }

    public EntityId RegionId { get; }
    public EntityId AdjacentRegionId { get; }
    public IReadOnlyDictionary<string, string>? Metadata { get; }
}

public sealed record RegionRecordSnapshot
{
    public RegionRecordSnapshot(
        EntityId id,
        RegionCategory category,
        EntityId? parentId,
        RegionSimulationState simulationState,
        EntityId simulationOwnerId,
        string? boundaryReference,
        IReadOnlyDictionary<string, string>? metadata)
    {
        Id = id;
        Category = category;
        ParentId = parentId;
        SimulationState = simulationState;
        SimulationOwnerId = simulationOwnerId;
        BoundaryReference = boundaryReference;
        Metadata = metadata is null ? null : new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(metadata, StringComparer.Ordinal));
    }

    public EntityId Id { get; }
    public RegionCategory Category { get; }
    public EntityId? ParentId { get; }
    public RegionSimulationState SimulationState { get; }
    public EntityId SimulationOwnerId { get; }
    public string? BoundaryReference { get; }
    public IReadOnlyDictionary<string, string>? Metadata { get; }
}

public sealed record RegionAssignmentSnapshot(EntityId EntityId, EntityId RegionId);

public sealed record RegionFrameworkSnapshot
{
    public const int CurrentVersion = 1;

    public RegionFrameworkSnapshot(
        int version,
        EntityId rootRegionId,
        IReadOnlyList<RegionRecordSnapshot>? regions,
        IReadOnlyList<RegionAdjacencySnapshot>? adjacency,
        IReadOnlyList<RegionAssignmentSnapshot>? assignments)
    {
        Version = version;
        RootRegionId = rootRegionId;
        Regions = regions is null ? null : Array.AsReadOnly(regions.ToArray());
        Adjacency = adjacency is null ? null : Array.AsReadOnly(adjacency.ToArray());
        Assignments = assignments is null ? null : Array.AsReadOnly(assignments.ToArray());
    }

    public int Version { get; }
    public EntityId RootRegionId { get; }
    public IReadOnlyList<RegionRecordSnapshot>? Regions { get; }
    public IReadOnlyList<RegionAdjacencySnapshot>? Adjacency { get; }
    public IReadOnlyList<RegionAssignmentSnapshot>? Assignments { get; }
}

public static class RegionErrorCodes
{
    public const string InvalidIdentifier = "region.invalid_identifier";
    public const string InvalidReference = "region.invalid_reference";
    public const string InvalidCategory = "region.invalid_category";
    public const string InvalidHierarchy = "region.invalid_hierarchy";
    public const string HierarchyCycle = "region.hierarchy_cycle";
    public const string InvalidState = "region.invalid_state";
    public const string InvalidSnapshot = "region.invalid_snapshot";
    public const string IncompatibleSnapshot = "region.incompatible_snapshot";
    public const string RootConflict = "region.root_conflict";
}

public sealed record RegionError(string Code, string Message);

public readonly record struct RegionResult(RegionError? Error)
{
    public bool IsSuccess => Error is null;
    public static RegionResult Success() => new(null);
    public static RegionResult Failure(string code, string message) => new(new RegionError(code, message));
}

public readonly record struct RegionResult<T>(T? Value, RegionError? Error)
{
    public bool IsSuccess => Error is null;
    public static RegionResult<T> Success(T value) => new(value, null);
    public static RegionResult<T> Failure(string code, string message) => new(default, new RegionError(code, message));
}

public sealed record RegionDiagnostic(
    EntityId RegionId,
    RegionCategory Category,
    EntityId? ParentId,
    IReadOnlyList<EntityId> ChildIds,
    IReadOnlyList<EntityId> AdjacentRegionIds,
    IReadOnlyList<EntityId> AssignedEntityIds,
    RegionSimulationState SimulationState,
    EntityId SimulationOwnerId);
