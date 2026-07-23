using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Properties;

/// <summary>Classifies property while delegating authoritative ownership to the Entity Framework.</summary>
public sealed class PropertyFramework
{
    private readonly EntityRegistry entities;
    private readonly IPropertyEventSink? events;
    private Dictionary<EntityId, PropertyProfile> profiles = [];

    public PropertyFramework(EntityRegistry entities, IPropertyEventSink? events = null)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.events = events;
    }

    public int Count => profiles.Count;

    public PropertyResult<PropertyProfile> Register(EntityId entityId, PropertyKindId kindId, WorldTimestamp timestamp,
        string? provenanceReference = null)
    {
        if (profiles.ContainsKey(entityId))
            return PropertyResult<PropertyProfile>.Failure(PropertyErrorCodes.DuplicateProfile, "Entity already has a Property Profile.");
        var valid = ValidateInput(entityId, kindId, timestamp, timestamp, provenanceReference);
        if (!valid.IsSuccess) return Fail<PropertyProfile>(valid.Error!);
        var entity = entities.Find(entityId).Value!;
        if (entity.LifecycleState != EntityLifecycleState.Active)
            return PropertyResult<PropertyProfile>.Failure(PropertyErrorCodes.InvalidLifecycle, "Property registration requires an Active Entity.");
        var profile = new PropertyProfile(entityId, kindId, PropertyLifecycleState.Active, timestamp, timestamp,
            Normalize(provenanceReference));
        var published = Publish(new("PropertyRegistered", entityId, timestamp));
        if (!published.IsSuccess) return Fail<PropertyProfile>(published.Error!);
        profiles.Add(entityId, profile);
        return PropertyResult<PropertyProfile>.Success(profile);
    }

    public PropertyResult<PropertyProfile> Find(EntityId entityId) => profiles.TryGetValue(entityId, out var profile)
        ? PropertyResult<PropertyProfile>.Success(profile)
        : PropertyResult<PropertyProfile>.Failure(PropertyErrorCodes.NotFound, "Property Profile was not found.");

    public PropertyResult ChangeKind(EntityId entityId, PropertyKindId kindId, WorldTimestamp timestamp,
        string? provenanceReference = null)
    {
        var mutable = FindMutable(entityId, timestamp, provenanceReference);
        if (!mutable.IsSuccess) return AsResult(mutable.Error!);
        if (!Valid(kindId.Value)) return PropertyResult.Failure(PropertyErrorCodes.InvalidIdentifier, "Property kind must be normalized.");
        var published = Publish(new("PropertyKindChanged", entityId, timestamp));
        if (!published.IsSuccess) return published;
        profiles[entityId] = mutable.Value! with
        {
            KindId = kindId,
            LastChangedAt = timestamp,
            ProvenanceReference = Normalize(provenanceReference),
        };
        return PropertyResult.Success();
    }

    public PropertyResult AssignOwner(EntityId propertyEntityId, EntityId? ownerEntityId, WorldTimestamp timestamp,
        string? provenanceReference = null)
    {
        var mutable = FindMutable(propertyEntityId, timestamp, provenanceReference);
        if (!mutable.IsSuccess) return AsResult(mutable.Error!);
        var property = entities.Find(propertyEntityId).Value!;
        if (property.LifecycleState != EntityLifecycleState.Active)
            return PropertyResult.Failure(PropertyErrorCodes.InvalidLifecycle, "Ownership mutation requires an Active property Entity.");
        if (ownerEntityId is { } owner)
        {
            var ownerResult = entities.Find(owner);
            if (!ownerResult.IsSuccess || ownerResult.Value!.LifecycleState is EntityLifecycleState.Retired or EntityLifecycleState.Destroyed)
                return PropertyResult.Failure(PropertyErrorCodes.InvalidReference, "Owner must be a registered non-terminal Entity.");
        }
        var published = Publish(new("PropertyOwnerChanged", propertyEntityId, timestamp, property.OwnerId, ownerEntityId));
        if (!published.IsSuccess) return published;
        var assigned = entities.AssignOwner(propertyEntityId, ownerEntityId);
        if (!assigned.IsSuccess)
            return PropertyResult.Failure(PropertyErrorCodes.OwnershipRejected, assigned.Error!.Message);
        profiles[propertyEntityId] = mutable.Value! with
        {
            LastChangedAt = timestamp,
            ProvenanceReference = Normalize(provenanceReference),
        };
        return PropertyResult.Success();
    }

    public PropertyResult Retire(EntityId entityId, WorldTimestamp timestamp, string? provenanceReference = null)
    {
        var mutable = FindMutable(entityId, timestamp, provenanceReference);
        if (!mutable.IsSuccess) return AsResult(mutable.Error!);
        var published = Publish(new("PropertyRetired", entityId, timestamp));
        if (!published.IsSuccess) return published;
        profiles[entityId] = mutable.Value! with
        {
            LifecycleState = PropertyLifecycleState.Retired,
            LastChangedAt = timestamp,
            ProvenanceReference = Normalize(provenanceReference),
        };
        return PropertyResult.Success();
    }

    public IReadOnlyList<PropertyProfile> QueryByKind(PropertyKindId id) => Query(profile => profile.KindId == id);
    public IReadOnlyList<PropertyProfile> QueryByOwner(EntityId id) => Query(profile => entities.Find(profile.EntityId).Value?.OwnerId == id);
    public IReadOnlyList<PropertyProfile> QueryByRegion(EntityId id) => Query(profile => entities.Find(profile.EntityId).Value?.RegionId == id);
    public IReadOnlyList<PropertyProfile> QueryByLifecycle(PropertyLifecycleState state) => Query(profile => profile.LifecycleState == state);
    public IReadOnlyList<PropertyProfile> QueryInvolving(EntityId id) => Query(profile =>
        profile.EntityId == id || entities.Find(profile.EntityId).Value?.OwnerId == id);

    public PropertyResult ValidateReferences()
    {
        foreach (var profile in profiles.Values.OrderBy(profile => profile.EntityId.Value))
        {
            var valid = ValidateProfile(profile);
            if (!valid.IsSuccess) return valid;
        }
        return PropertyResult.Success();
    }

    public PropertyResult<PropertyDiagnostic> Inspect(EntityId entityId)
    {
        var found = Find(entityId);
        if (!found.IsSuccess) return PropertyResult<PropertyDiagnostic>.Failure(found.Error!.Code, found.Error.Message);
        var entity = entities.Find(entityId);
        var valid = ValidateProfile(found.Value!);
        return PropertyResult<PropertyDiagnostic>.Success(new(found.Value!, entity.Value?.OwnerId, entity.Value?.RegionId,
            valid.IsSuccess ? "valid" : $"{valid.Error!.Code}: {valid.Error.Message}"));
    }

    public PropertyFrameworkSnapshot ExportSnapshot() => new(PropertyFrameworkSnapshot.CurrentVersion,
        profiles.Values.OrderBy(profile => profile.EntityId.Value).ToArray());

    public PropertyResult RestoreSnapshot(PropertyFrameworkSnapshot? snapshot)
    {
        if (snapshot is null) return PropertyResult.Failure(PropertyErrorCodes.InvalidSnapshot, "Property snapshot cannot be null.");
        if (snapshot.Version != PropertyFrameworkSnapshot.CurrentVersion)
            return PropertyResult.Failure(PropertyErrorCodes.UnsupportedSnapshotVersion, "Property snapshot version is unsupported.");
        if (snapshot.Profiles is null || snapshot.Profiles.Any(profile => profile is null))
            return PropertyResult.Failure(PropertyErrorCodes.InvalidSnapshot, "Property profiles cannot be null or contain null values.");
        var candidate = new Dictionary<EntityId, PropertyProfile>();
        foreach (var profile in snapshot.Profiles)
        {
            if (!candidate.TryAdd(profile.EntityId, profile))
                return PropertyResult.Failure(PropertyErrorCodes.DuplicateProfile, "Snapshot contains duplicate Property Profiles.");
            var valid = ValidateProfile(profile);
            if (!valid.IsSuccess) return valid;
        }
        profiles = candidate;
        return PropertyResult.Success();
    }

    private PropertyResult<PropertyProfile> FindMutable(EntityId entityId, WorldTimestamp timestamp, string? provenance)
    {
        var found = Find(entityId);
        if (!found.IsSuccess) return found;
        if (found.Value!.LifecycleState != PropertyLifecycleState.Active)
            return PropertyResult<PropertyProfile>.Failure(PropertyErrorCodes.InvalidLifecycle, "Only Active Property Profiles may change.");
        if (timestamp.Value < found.Value.LastChangedAt.Value)
            return PropertyResult<PropertyProfile>.Failure(PropertyErrorCodes.InvalidTimestamp, "Change cannot precede the previous change.");
        if (!ValidOptional(provenance))
            return PropertyResult<PropertyProfile>.Failure(PropertyErrorCodes.InvalidIdentifier, "Provenance must be normalized.");
        return found;
    }

    private PropertyResult ValidateProfile(PropertyProfile profile)
    {
        var valid = ValidateInput(profile.EntityId, profile.KindId, profile.RegisteredAt, profile.LastChangedAt,
            profile.ProvenanceReference);
        if (!valid.IsSuccess) return valid;
        if (!Enum.IsDefined(profile.LifecycleState))
            return PropertyResult.Failure(PropertyErrorCodes.InvalidLifecycle, "Property lifecycle is invalid.");
        return PropertyResult.Success();
    }

    private PropertyResult ValidateInput(EntityId entityId, PropertyKindId kindId, WorldTimestamp registeredAt,
        WorldTimestamp lastChangedAt, string? provenance)
    {
        if (!entities.Exists(entityId))
            return PropertyResult.Failure(PropertyErrorCodes.InvalidReference, "Property Entity must remain registered.");
        if (!Valid(kindId.Value) || !ValidOptional(provenance))
            return PropertyResult.Failure(PropertyErrorCodes.InvalidIdentifier, "Property identifiers must be normalized.");
        if (registeredAt.Value > lastChangedAt.Value)
            return PropertyResult.Failure(PropertyErrorCodes.InvalidTimestamp, "Registration cannot follow last change.");
        return PropertyResult.Success();
    }

    private IReadOnlyList<PropertyProfile> Query(Func<PropertyProfile, bool> predicate) => profiles.Values.Where(predicate)
        .OrderBy(profile => profile.EntityId.Value).ToArray();
    private PropertyResult Publish(PropertyDomainEvent value)
    {
        if (events is null) return PropertyResult.Success();
        var result = events.Publish(value);
        return result.IsSuccess ? result : PropertyResult.Failure(PropertyErrorCodes.EventPublicationFailed, result.Error!.Message);
    }
    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static bool ValidOptional(string? value) => value is null || Valid(value);
    private static string? Normalize(string? value) => value?.Trim();
    private static PropertyResult AsResult(PropertyError error) => PropertyResult.Failure(error.Code, error.Message);
    private static PropertyResult<T> Fail<T>(PropertyError error) => PropertyResult<T>.Failure(error.Code, error.Message);
}
