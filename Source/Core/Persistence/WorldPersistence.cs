using System.Security.Cryptography;
using System.Text.Json;
using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Information;
using Mythos.Framework.History;
using Mythos.Framework.Npcs;
using Mythos.Framework.Regions;
using Mythos.Framework.Relationships;
using Mythos.Framework.Time;

namespace Mythos.Framework.Persistence;

/// <summary>Coordinates the complete M-001 world snapshot without owning domain state.</summary>
public sealed class WorldPersistence(ISaveStorage storage)
{
    private const string ManifestId = "manifest";
    private const string FrameworkVersion = "m-002.2";
    private static readonly HashSet<string> PhysicalPartitionIds =
        new([ManifestId, "characters", "entities", "history", "information", "npcs", "regions", "relationships", "time"], StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = PersistenceJson.CreateOptions();

    public PersistenceResult Save(string slotId, string worldId, PersistentWorldState world)
    {
        if (world is null || !Valid(worldId)) return PersistenceResult.Failure(PersistenceErrorCodes.InvalidData, "World and normalized world ID are required.");
        var references = Validate(world);
        if (!references.IsSuccess) return references;

        var data = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["characters"] = Serialize(world.Characters.ExportSnapshot()),
            ["entities"] = Serialize(new EntityDomainSnapshot(EntityDomainSnapshot.CurrentVersion, world.Entities.ExportSnapshots())),
            ["history"] = Serialize(world.History.ExportSnapshot()),
            ["information"] = Serialize(world.Information.ExportSnapshot()),
            ["npcs"] = Serialize(world.Npcs.ExportSnapshot()),
            ["regions"] = Serialize(world.Regions.ExportSnapshot()),
            ["relationships"] = Serialize(world.Relationships.ExportSnapshot()),
            ["time"] = Serialize(world.Clock.CreateSnapshot()),
        };
        var descriptors = data.Select(item => new SavePartitionDescriptor(item.Key, DomainVersion(item.Key), Hash(item.Value))).ToArray();
        data[ManifestId] = Serialize(new SaveManifest(SaveManifest.CurrentVersion, FrameworkVersion, worldId, descriptors));

        using var transaction = storage.BeginWrite(slotId);
        foreach (var item in data)
        {
            var written = transaction.Write(item.Key, item.Value);
            if (!written.IsSuccess) return written;
        }
        return transaction.Commit();
    }

    public PersistenceResult<PersistentWorldState> Load(string slotId, PersistenceLoadContext context)
    {
        if (context is null) return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.InvalidData, "Load context is required.");
        var read = storage.Read(slotId);
        if (!read.IsSuccess) return PersistenceResult<PersistentWorldState>.Failure(read.Error!.Code, read.Error.Message, read.Error.Partition);
        var partitions = read.Value!;
        var missingPhysical = PhysicalPartitionIds.FirstOrDefault(id => !partitions.ContainsKey(id));
        if (missingPhysical is not null) return Missing<PersistentWorldState>(missingPhysical);
        if (partitions.Keys.Any(id => !PhysicalPartitionIds.Contains(id)))
            return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.InvalidData,
                "Save contains an undeclared physical partition.");
        long aggregateBytes = 0;
        foreach (var partition in partitions)
        {
            var limit = partition.Key == ManifestId ? PersistenceLimits.ManifestBytes : PersistenceLimits.DomainPartitionBytes;
            if (partition.Value is null || partition.Value.LongLength > limit)
                return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.SizeLimitExceeded,
                    $"Partition '{partition.Key}' exceeds the M-002 load limit of {limit} bytes.", partition.Key);
            aggregateBytes += partition.Value.LongLength;
        }
        if (aggregateBytes > PersistenceLimits.AggregateBytes)
            return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.SizeLimitExceeded,
                $"Save exceeds the M-002 aggregate load limit of {PersistenceLimits.AggregateBytes} bytes.");
        if (!partitions.TryGetValue(ManifestId, out var manifestBytes)) return Missing<PersistentWorldState>(ManifestId);
        var manifestResult = Deserialize<SaveManifest>(manifestBytes, ManifestId);
        if (!manifestResult.IsSuccess) return Fail<PersistentWorldState>(manifestResult.Error!);
        var manifest = manifestResult.Value!;
        if (manifest.Version != SaveManifest.CurrentVersion)
            return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.UnsupportedVersion, "Save manifest version is unsupported.", ManifestId);
        if (!string.Equals(manifest.FrameworkVersion, FrameworkVersion, StringComparison.Ordinal))
            return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.UnsupportedVersion, "Framework save version is unsupported.", ManifestId);
        if (!Valid(manifest.WorldId) || manifest.Partitions is null)
            return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.InvalidData, "Save manifest is malformed.", ManifestId);
        var required = new HashSet<string>(["characters", "entities", "history", "information", "npcs", "regions", "relationships", "time"], StringComparer.Ordinal);
        if (manifest.Partitions.Count != required.Count || manifest.Partitions.Any(p => p is null || !Valid(p.Id) || !Valid(p.Sha256)) ||
            !required.SetEquals(manifest.Partitions.Select(p => p.Id)))
            return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.MissingPartition, "Save manifest does not declare the complete required partition set.", ManifestId);

        foreach (var descriptor in manifest.Partitions)
        {
            if (!partitions.TryGetValue(descriptor.Id, out var bytes)) return Missing<PersistentWorldState>(descriptor.Id);
            if (descriptor.Version != DomainVersion(descriptor.Id))
                return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.UnsupportedVersion, "Domain version is unsupported.", descriptor.Id);
            if (!string.Equals(descriptor.Sha256, Hash(bytes), StringComparison.Ordinal))
                return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.CorruptData, "Partition checksum does not match the manifest.", descriptor.Id);
        }

        try
        {
            var entityData = Get<EntityDomainSnapshot>(partitions, "entities");
            if (!entityData.IsSuccess) return Fail<PersistentWorldState>(entityData.Error!);
            var entitiesResult = RestoreEntities(entityData.Value!);
            if (!entitiesResult.IsSuccess) return Fail<PersistentWorldState>(entitiesResult.Error!);
            var entities = entitiesResult.Value!;

            var relationships = new RelationshipFramework(entities);
            var relationshipData = Get<RelationshipFrameworkSnapshot>(partitions, "relationships");
            if (!relationshipData.IsSuccess) return Fail<PersistentWorldState>(relationshipData.Error!);
            var relationshipRestore = relationships.RestoreSnapshot(relationshipData.Value);
            if (!relationshipRestore.IsSuccess) return DomainFailure<PersistentWorldState>(relationshipRestore.Error!.Code,
                relationshipRestore.Error.Message, "relationships");

            var information = new InformationFramework(entities);
            var informationData = Get<InformationFrameworkSnapshot>(partitions, "information");
            if (!informationData.IsSuccess) return Fail<PersistentWorldState>(informationData.Error!);
            var informationRestore = information.RestoreSnapshot(informationData.Value);
            if (!informationRestore.IsSuccess) return DomainFailure<PersistentWorldState>(informationRestore.Error!.Code,
                informationRestore.Error.Message, "information");

            var timeData = Get<WorldClockSnapshot>(partitions, "time");
            if (!timeData.IsSuccess) return Fail<PersistentWorldState>(timeData.Error!);
            var clock = WorldClock.Restore(timeData.Value, context.Calendar);
            if (!clock.IsSuccess) return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.InvalidData, clock.Error!.Message, "time");

            var regions = new RegionFramework(entities);
            var regionData = Get<RegionFrameworkSnapshot>(partitions, "regions");
            if (!regionData.IsSuccess) return Fail<PersistentWorldState>(regionData.Error!);
            var regionRestore = regions.Restore(regionData.Value!);
            if (!regionRestore.IsSuccess) return DomainFailure<PersistentWorldState>(regionRestore.Error!.Code, regionRestore.Error.Message, "regions");

            var history = new WorldHistoryFramework(entities, regions);
            var historyData = Get<WorldHistorySnapshot>(partitions, "history");
            if (!historyData.IsSuccess) return Fail<PersistentWorldState>(historyData.Error!);
            var historyRestore = history.RestoreSnapshot(historyData.Value);
            if (!historyRestore.IsSuccess) return DomainFailure<PersistentWorldState>(historyRestore.Error!.Code,
                historyRestore.Error.Message, "history");

            var characters = new CharacterRegistry(entities, context.CharacterReferences);
            var characterData = Get<CharacterRegistrySnapshot>(partitions, "characters");
            if (!characterData.IsSuccess) return Fail<PersistentWorldState>(characterData.Error!);
            var characterRestore = characters.RestoreSnapshot(characterData.Value);
            if (!characterRestore.IsSuccess) return DomainFailure<PersistentWorldState>(characterRestore.Error!.Code, characterRestore.Error.Message, "characters");

            var npcs = new NpcFramework(entities, characters, regions, context.NpcReferences);
            var npcData = Get<NpcFrameworkSnapshot>(partitions, "npcs");
            if (!npcData.IsSuccess) return Fail<PersistentWorldState>(npcData.Error!);
            var npcRestore = npcs.RestoreSnapshot(npcData.Value);
            if (!npcRestore.IsSuccess) return DomainFailure<PersistentWorldState>(npcRestore.Error!.Code, npcRestore.Error.Message, "npcs");

            var candidate = new PersistentWorldState(entities, clock.Value!, regions, characters, npcs, relationships, information, history);
            var valid = Validate(candidate);
            return valid.IsSuccess ? PersistenceResult<PersistentWorldState>.Success(candidate) : Fail<PersistentWorldState>(valid.Error!);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return PersistenceResult<PersistentWorldState>.Failure(PersistenceErrorCodes.InvalidData, $"Save data is malformed: {exception.Message}");
        }
    }

    private static PersistenceResult<EntityRegistry> RestoreEntities(EntityDomainSnapshot snapshot)
    {
        if (snapshot.Version != EntityDomainSnapshot.CurrentVersion)
            return PersistenceResult<EntityRegistry>.Failure(PersistenceErrorCodes.UnsupportedVersion, "Entity snapshot version is unsupported.", "entities");
        if (snapshot.Entities is null || snapshot.Entities.Any(e => e is null))
            return PersistenceResult<EntityRegistry>.Failure(PersistenceErrorCodes.InvalidData, "Entity snapshot is null or malformed.", "entities");
        var registry = new EntityRegistry();
        foreach (var entity in snapshot.Entities.OrderBy(e => e.Id.Value))
        {
            var baseSnapshot = new EntitySnapshot(entity.Id, entity.Category, entity.LifecycleState, entity.Tags, null, null, null,
                entity.ComponentTypes, entity.CreatedAt, entity.RetiredAt);
            var registered = registry.Register(baseSnapshot);
            if (!registered.IsSuccess) return DomainFailure<EntityRegistry>(registered.Error!.Message, "entities");
        }
        foreach (var entity in snapshot.Entities.OrderBy(e => e.Id.Value))
        {
            var parent = registry.AssignParent(entity.Id, entity.ParentId);
            var owner = registry.AssignOwner(entity.Id, entity.OwnerId);
            var region = registry.AssignRegion(entity.Id, entity.RegionId);
            if (!parent.IsSuccess || !owner.IsSuccess || !region.IsSuccess)
                return DomainFailure<EntityRegistry>((parent.Error ?? owner.Error ?? region.Error)!.Message, "entities");
        }
        return PersistenceResult<EntityRegistry>.Success(registry);
    }

    private static PersistenceResult Validate(PersistentWorldState world)
    {
        var region = world.Regions.ValidateReferences();
        if (!region.IsSuccess) return DomainFailure(region.Error!.Message, "regions");
        var character = world.Characters.ValidateReferences();
        if (!character.IsSuccess) return DomainFailure(character.Error!.Message, "characters");
        var npc = world.Npcs.ValidateReferences();
        if (!npc.IsSuccess) return DomainFailure(npc.Error!.Message, "npcs");
        var relationships = world.Relationships.ValidateReferences();
        if (!relationships.IsSuccess) return DomainFailure(relationships.Error!.Message, "relationships");
        var information = world.Information.ValidateReferences();
        if (!information.IsSuccess) return DomainFailure(information.Error!.Message, "information");
        var history = world.History.ValidateReferences();
        if (!history.IsSuccess) return DomainFailure(history.Error!.Message, "history");
        return PersistenceResult.Success();
    }

    private static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
    private static PersistenceResult<T> Get<T>(IReadOnlyDictionary<string, byte[]> data, string id) =>
        data.TryGetValue(id, out var bytes) ? Deserialize<T>(bytes, id) : Missing<T>(id);
    private static PersistenceResult<T> Deserialize<T>(byte[] bytes, string id)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
            return value is null ? PersistenceResult<T>.Failure(PersistenceErrorCodes.InvalidData, "Partition contains null data.", id) : PersistenceResult<T>.Success(value);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
        {
            return PersistenceResult<T>.Failure(PersistenceErrorCodes.InvalidData, $"Partition is malformed: {exception.Message}", id);
        }
    }
    private static int DomainVersion(string id) => id switch
    {
        "entities" => EntityDomainSnapshot.CurrentVersion,
        "time" => WorldClock.SnapshotVersion,
        "regions" => RegionFrameworkSnapshot.CurrentVersion,
        "characters" => CharacterRegistrySnapshot.CurrentVersion,
        "npcs" => NpcFrameworkSnapshot.CurrentVersion,
        "relationships" => RelationshipFrameworkSnapshot.CurrentVersion,
        "information" => InformationFrameworkSnapshot.CurrentVersion,
        "history" => WorldHistorySnapshot.CurrentVersion,
        _ => -1,
    };
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static bool Valid(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static PersistenceResult<T> Missing<T>(string id) => PersistenceResult<T>.Failure(PersistenceErrorCodes.MissingPartition, $"Required partition '{id}' is missing.", id);
    private static PersistenceResult<T> Fail<T>(PersistenceError error) => PersistenceResult<T>.Failure(error.Code, error.Message, error.Partition);
    private static PersistenceResult<T> DomainFailure<T>(string message, string partition) => PersistenceResult<T>.Failure(PersistenceErrorCodes.UnresolvedReference, message, partition);
    private static PersistenceResult<T> DomainFailure<T>(string code, string message, string partition) => PersistenceResult<T>.Failure(
        code.Contains("unsupported", StringComparison.Ordinal) || code.Contains("incompatible", StringComparison.Ordinal)
            ? PersistenceErrorCodes.UnsupportedVersion
            : code.Contains("snapshot", StringComparison.Ordinal) ? PersistenceErrorCodes.InvalidData : PersistenceErrorCodes.UnresolvedReference,
        message, partition);
    private static PersistenceResult DomainFailure(string message, string partition) => PersistenceResult.Failure(PersistenceErrorCodes.UnresolvedReference, message, partition);
}
