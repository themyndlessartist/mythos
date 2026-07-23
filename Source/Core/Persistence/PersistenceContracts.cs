using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Information;
using Mythos.Framework.Npcs;
using Mythos.Framework.Relationships;
using Mythos.Framework.Regions;
using Mythos.Framework.Time;

namespace Mythos.Framework.Persistence;

public static class PersistenceErrorCodes
{
    public const string CorruptData = "persistence.corrupt_data";
    public const string MissingPartition = "persistence.missing_partition";
    public const string UnsupportedVersion = "persistence.unsupported_version";
    public const string InvalidData = "persistence.invalid_data";
    public const string UnresolvedReference = "persistence.unresolved_reference";
    public const string StorageFailure = "persistence.storage_failure";
    public const string SizeLimitExceeded = "persistence.size_limit_exceeded";
}

/// <summary>M-002 defensive load bounds; replaceable with a future approved storage policy.</summary>
public static class PersistenceLimits
{
    public const int ManifestBytes = 64 * 1024;
    public const int DomainPartitionBytes = 1024 * 1024;
    public const long AggregateBytes = 2L * 1024 * 1024;
}

public sealed record PersistenceError(string Code, string Message, string? Partition = null);

public readonly record struct PersistenceResult(PersistenceError? Error)
{
    public bool IsSuccess => Error is null;
    public static PersistenceResult Success() => new(null);
    public static PersistenceResult Failure(string code, string message, string? partition = null) => new(new(code, message, partition));
}

public readonly record struct PersistenceResult<T>(T? Value, PersistenceError? Error)
{
    public bool IsSuccess => Error is null;
    public static PersistenceResult<T> Success(T value) => new(value, null);
    public static PersistenceResult<T> Failure(string code, string message, string? partition = null) => new(default, new(code, message, partition));
}

public sealed record SavePartitionDescriptor(string Id, int Version, string Sha256);

public sealed record SaveManifest(
    int Version,
    string FrameworkVersion,
    string WorldId,
    IReadOnlyList<SavePartitionDescriptor>? Partitions)
{
    public const int CurrentVersion = 1;
}

public sealed record EntityDomainSnapshot(int Version, IReadOnlyList<EntitySnapshot>? Entities)
{
    public const int CurrentVersion = 1;
}

public sealed class PersistentWorldState
{
    public PersistentWorldState(EntityRegistry entities, WorldClock clock, RegionFramework regions,
        CharacterRegistry characters, NpcFramework npcs, RelationshipFramework relationships, InformationFramework information)
    {
        Entities = entities ?? throw new ArgumentNullException(nameof(entities));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        Characters = characters ?? throw new ArgumentNullException(nameof(characters));
        Npcs = npcs ?? throw new ArgumentNullException(nameof(npcs));
        Relationships = relationships ?? throw new ArgumentNullException(nameof(relationships));
        Information = information ?? throw new ArgumentNullException(nameof(information));
    }

    public EntityRegistry Entities { get; }
    public WorldClock Clock { get; }
    public RegionFramework Regions { get; }
    public CharacterRegistry Characters { get; }
    public NpcFramework Npcs { get; }
    public RelationshipFramework Relationships { get; }
    public InformationFramework Information { get; }
}

public sealed record PersistenceLoadContext(
    CalendarModel Calendar,
    ICharacterReferenceValidator CharacterReferences,
    INpcReferenceProvider NpcReferences);

public interface ISaveStorage
{
    PersistenceResult<IReadOnlyDictionary<string, byte[]>> Read(string slotId);
    ISaveWriteTransaction BeginWrite(string slotId);
}

public interface ISaveWriteTransaction : IDisposable
{
    PersistenceResult Write(string partitionId, ReadOnlyMemory<byte> data);
    PersistenceResult Commit();
}
