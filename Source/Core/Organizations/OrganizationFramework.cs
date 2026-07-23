using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Organizations;

/// <summary>Owns generic Organization classification and explicit membership records.</summary>
public sealed class OrganizationFramework
{
    private readonly EntityRegistry entities;
    private readonly IMembershipIdGenerator idGenerator;
    private readonly IOrganizationEventSink? events;
    private Dictionary<EntityId, OrganizationProfile> profiles = [];
    private Dictionary<MembershipId, MembershipSnapshot> memberships = [];

    public OrganizationFramework(EntityRegistry entities, IMembershipIdGenerator? idGenerator = null,
        IOrganizationEventSink? events = null)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.idGenerator = idGenerator ?? new Version7MembershipIdGenerator();
        this.events = events;
    }

    public int OrganizationCount => profiles.Count;
    public int MembershipCount => memberships.Count;

    public OrganizationResult<OrganizationProfile> Register(EntityId entityId, OrganizationKindId kindId,
        WorldTimestamp timestamp, string? provenanceReference = null)
    {
        if (profiles.ContainsKey(entityId)) return OrganizationResult<OrganizationProfile>.Failure(
            OrganizationErrorCodes.DuplicateProfile, "Entity already has an Organization Profile.");
        var valid = ValidateProfileInput(entityId, kindId, timestamp, timestamp, provenanceReference, requireActive: true);
        if (!valid.IsSuccess) return Fail<OrganizationProfile>(valid.Error!);
        var published = Publish(new("OrganizationRegistered", entityId, timestamp));
        if (!published.IsSuccess) return Fail<OrganizationProfile>(published.Error!);
        var profile = new OrganizationProfile(entityId, kindId, OrganizationLifecycleState.Active, timestamp, timestamp,
            Normalize(provenanceReference));
        profiles.Add(entityId, profile);
        return OrganizationResult<OrganizationProfile>.Success(profile);
    }

    public OrganizationResult<OrganizationProfile> FindOrganization(EntityId id) => profiles.TryGetValue(id, out var value)
        ? OrganizationResult<OrganizationProfile>.Success(value)
        : OrganizationResult<OrganizationProfile>.Failure(OrganizationErrorCodes.NotFound, "Organization Profile was not found.");
    public OrganizationResult<MembershipSnapshot> FindMembership(MembershipId id) => memberships.TryGetValue(id, out var value)
        ? OrganizationResult<MembershipSnapshot>.Success(value)
        : OrganizationResult<MembershipSnapshot>.Failure(OrganizationErrorCodes.NotFound, "Membership was not found.");
    public OrganizationResult<MembershipSnapshot> FindActiveMembership(EntityId organizationId, EntityId memberId)
    {
        var value = memberships.Values.FirstOrDefault(item => item.LifecycleState == MembershipLifecycleState.Active &&
            item.OrganizationEntityId == organizationId && item.MemberEntityId == memberId);
        return value is null ? OrganizationResult<MembershipSnapshot>.Failure(OrganizationErrorCodes.NotFound,
            "Active Membership was not found.") : OrganizationResult<MembershipSnapshot>.Success(value);
    }

    public OrganizationResult ChangeKind(EntityId id, OrganizationKindId kindId, WorldTimestamp timestamp,
        string? provenanceReference = null)
    {
        var found = FindMutableOrganization(id, timestamp, provenanceReference);
        if (!found.IsSuccess) return AsResult(found.Error!);
        if (!Valid(kindId.Value)) return OrganizationResult.Failure(OrganizationErrorCodes.InvalidIdentifier,
            "Organization kind must be normalized.");
        var published = Publish(new("OrganizationKindChanged", id, timestamp));
        if (!published.IsSuccess) return published;
        profiles[id] = found.Value! with
        {
            KindId = kindId,
            LastChangedAt = timestamp,
            ProvenanceReference = Normalize(provenanceReference)
        };
        return OrganizationResult.Success();
    }

    public OrganizationResult RetireOrganization(EntityId id, WorldTimestamp timestamp, string? provenanceReference = null)
    {
        var found = FindMutableOrganization(id, timestamp, provenanceReference);
        if (!found.IsSuccess) return AsResult(found.Error!);
        if (memberships.Values.Any(item => item.OrganizationEntityId == id && item.LifecycleState == MembershipLifecycleState.Active))
            return OrganizationResult.Failure(OrganizationErrorCodes.ActiveMembershipsRemain,
                "Active Memberships must be retired before the Organization Profile.");
        var published = Publish(new("OrganizationRetired", id, timestamp));
        if (!published.IsSuccess) return published;
        profiles[id] = found.Value! with
        {
            LifecycleState = OrganizationLifecycleState.Retired,
            LastChangedAt = timestamp,
            ProvenanceReference = Normalize(provenanceReference)
        };
        return OrganizationResult.Success();
    }

    public OrganizationResult<MembershipSnapshot> AddMembership(EntityId organizationId, EntityId memberId,
        IReadOnlyList<OrganizationRoleId>? roles, WorldTimestamp timestamp, string? provenanceReference = null)
    {
        var valid = ValidateNewMembership(organizationId, memberId, roles, timestamp, provenanceReference);
        if (!valid.IsSuccess) return Fail<MembershipSnapshot>(valid.Error!);
        if (FindActiveMembership(organizationId, memberId).IsSuccess)
            return OrganizationResult<MembershipSnapshot>.Failure(OrganizationErrorCodes.DuplicateActiveMembership,
                "An Active Membership already exists for this Organization and member.");
        var canonical = CanonicalRoles(roles!);
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = idGenerator.Create();
            if (id.Value == Guid.Empty || memberships.ContainsKey(id)) continue;
            var value = new MembershipSnapshot(id, organizationId, memberId, canonical,
                MembershipLifecycleState.Active, timestamp, timestamp, Normalize(provenanceReference));
            var published = Publish(new("MembershipCreated", organizationId, timestamp, id, memberId));
            if (!published.IsSuccess) return Fail<MembershipSnapshot>(published.Error!);
            memberships.Add(id, value);
            return OrganizationResult<MembershipSnapshot>.Success(value);
        }
        return OrganizationResult<MembershipSnapshot>.Failure(OrganizationErrorCodes.DuplicateMembershipId,
            "Membership ID generator did not produce a unique initialized ID.");
    }

    public OrganizationResult ReplaceRoles(MembershipId id, IReadOnlyList<OrganizationRoleId>? roles,
        WorldTimestamp timestamp, string? provenanceReference = null)
    {
        var found = FindMutableMembership(id, timestamp, provenanceReference);
        if (!found.IsSuccess) return AsResult(found.Error!);
        var roleValidation = ValidateRoles(roles);
        if (!roleValidation.IsSuccess) return roleValidation;
        var published = Publish(new("MembershipRolesChanged", found.Value!.OrganizationEntityId, timestamp, id,
            found.Value.MemberEntityId));
        if (!published.IsSuccess) return published;
        memberships[id] = Copy(found.Value, CanonicalRoles(roles!), found.Value.LifecycleState, timestamp,
            Normalize(provenanceReference));
        return OrganizationResult.Success();
    }

    public OrganizationResult RetireMembership(MembershipId id, WorldTimestamp timestamp,
        string? provenanceReference = null)
    {
        var found = FindMutableMembership(id, timestamp, provenanceReference);
        if (!found.IsSuccess) return AsResult(found.Error!);
        var published = Publish(new("MembershipRetired", found.Value!.OrganizationEntityId, timestamp, id,
            found.Value.MemberEntityId));
        if (!published.IsSuccess) return published;
        memberships[id] = Copy(found.Value, found.Value.RoleIds!, MembershipLifecycleState.Retired, timestamp,
            Normalize(provenanceReference));
        return OrganizationResult.Success();
    }

    public IReadOnlyList<MembershipSnapshot> QueryByOrganization(EntityId id) => Query(item => item.OrganizationEntityId == id);
    public IReadOnlyList<MembershipSnapshot> QueryByMember(EntityId id) => Query(item => item.MemberEntityId == id);
    public IReadOnlyList<MembershipSnapshot> QueryByRole(OrganizationRoleId id) => Query(item => item.RoleIds!.Contains(id));
    public IReadOnlyList<MembershipSnapshot> QueryByLifecycle(MembershipLifecycleState state) => Query(item => item.LifecycleState == state);
    public IReadOnlyList<MembershipSnapshot> QueryInvolving(EntityId id) => Query(item =>
        item.OrganizationEntityId == id || item.MemberEntityId == id);

    public OrganizationResult ValidateReferences() => ValidateCandidate(profiles, memberships);

    public OrganizationResult<OrganizationDiagnostic> Inspect(EntityId id)
    {
        var found = FindOrganization(id);
        if (!found.IsSuccess) return OrganizationResult<OrganizationDiagnostic>.Failure(found.Error!.Code, found.Error.Message);
        var valid = ValidateProfile(found.Value!);
        var count = memberships.Values.Count(item => item.OrganizationEntityId == id && item.LifecycleState == MembershipLifecycleState.Active);
        return OrganizationResult<OrganizationDiagnostic>.Success(new(found.Value!, count,
            valid.IsSuccess ? "valid" : $"{valid.Error!.Code}: {valid.Error.Message}"));
    }

    public OrganizationFrameworkSnapshot ExportSnapshot() => new(OrganizationFrameworkSnapshot.CurrentVersion,
        profiles.Values.OrderBy(item => item.EntityId.Value).ToArray(),
        memberships.Values.OrderBy(item => item.Id.Value).ToArray());

    public OrganizationResult RestoreSnapshot(OrganizationFrameworkSnapshot? snapshot)
    {
        if (snapshot is null) return OrganizationResult.Failure(OrganizationErrorCodes.InvalidSnapshot, "Organization snapshot cannot be null.");
        if (snapshot.Version != OrganizationFrameworkSnapshot.CurrentVersion)
            return OrganizationResult.Failure(OrganizationErrorCodes.UnsupportedSnapshotVersion, "Organization snapshot version is unsupported.");
        if (snapshot.Profiles is null || snapshot.Memberships is null || snapshot.Profiles.Any(item => item is null) ||
            snapshot.Memberships.Any(item => item is null))
            return OrganizationResult.Failure(OrganizationErrorCodes.InvalidSnapshot, "Organization snapshot collections are malformed.");
        var candidateProfiles = new Dictionary<EntityId, OrganizationProfile>();
        foreach (var profile in snapshot.Profiles)
            if (!candidateProfiles.TryAdd(profile.EntityId, profile)) return OrganizationResult.Failure(
                OrganizationErrorCodes.DuplicateProfile, "Snapshot contains duplicate Organization Profiles.");
        var candidateMemberships = new Dictionary<MembershipId, MembershipSnapshot>();
        foreach (var membership in snapshot.Memberships)
            if (!candidateMemberships.TryAdd(membership.Id, membership)) return OrganizationResult.Failure(
                OrganizationErrorCodes.DuplicateMembershipId, "Snapshot contains duplicate Membership IDs.");
        var valid = ValidateCandidate(candidateProfiles, candidateMemberships);
        if (!valid.IsSuccess) return valid;
        profiles = candidateProfiles;
        memberships = candidateMemberships;
        return OrganizationResult.Success();
    }

    private OrganizationResult ValidateCandidate(IReadOnlyDictionary<EntityId, OrganizationProfile> candidateProfiles,
        IReadOnlyDictionary<MembershipId, MembershipSnapshot> candidateMemberships)
    {
        foreach (var profile in candidateProfiles.Values)
        {
            var valid = ValidateProfile(profile);
            if (!valid.IsSuccess) return valid;
        }
        foreach (var membership in candidateMemberships.Values)
        {
            var valid = ValidateMembership(membership, candidateProfiles);
            if (!valid.IsSuccess) return valid;
        }
        if (candidateMemberships.Values.Where(item => item.LifecycleState == MembershipLifecycleState.Active)
            .GroupBy(item => (item.OrganizationEntityId, item.MemberEntityId)).Any(group => group.Count() > 1))
            return OrganizationResult.Failure(OrganizationErrorCodes.DuplicateActiveMembership,
                "Snapshot contains duplicate Active Membership keys.");
        if (candidateProfiles.Values.Any(profile => profile.LifecycleState == OrganizationLifecycleState.Retired &&
            candidateMemberships.Values.Any(item => item.OrganizationEntityId == profile.EntityId &&
                item.LifecycleState == MembershipLifecycleState.Active)))
            return OrganizationResult.Failure(OrganizationErrorCodes.ActiveMembershipsRemain,
                "Retired Organizations cannot retain Active Memberships.");
        return OrganizationResult.Success();
    }

    private OrganizationResult ValidateProfile(OrganizationProfile profile)
    {
        var valid = ValidateProfileInput(profile.EntityId, profile.KindId, profile.RegisteredAt, profile.LastChangedAt,
            profile.ProvenanceReference, requireActive: false);
        if (!valid.IsSuccess) return valid;
        return Enum.IsDefined(profile.LifecycleState) ? OrganizationResult.Success() : OrganizationResult.Failure(
            OrganizationErrorCodes.InvalidLifecycle, "Organization lifecycle is invalid.");
    }

    private OrganizationResult ValidateProfileInput(EntityId id, OrganizationKindId kind, WorldTimestamp registered,
        WorldTimestamp changed, string? provenance, bool requireActive)
    {
        var entity = entities.Find(id);
        if (!entity.IsSuccess || entity.Value!.Category != new EntityCategory("Organization"))
            return OrganizationResult.Failure(OrganizationErrorCodes.InvalidReference,
                "Organization Profile requires a registered Organization Entity.");
        if (requireActive && entity.Value.LifecycleState != EntityLifecycleState.Active)
            return OrganizationResult.Failure(OrganizationErrorCodes.InvalidLifecycle,
                "Organization registration requires an Active Entity.");
        if (!Valid(kind.Value) || !ValidOptional(provenance)) return OrganizationResult.Failure(
            OrganizationErrorCodes.InvalidIdentifier, "Organization identifiers must be normalized.");
        return registered.Value <= changed.Value ? OrganizationResult.Success() : OrganizationResult.Failure(
            OrganizationErrorCodes.InvalidTimestamp, "Registration cannot follow last change.");
    }

    private OrganizationResult ValidateNewMembership(EntityId organization, EntityId member,
        IReadOnlyList<OrganizationRoleId>? roles, WorldTimestamp timestamp, string? provenance)
    {
        var profile = FindOrganization(organization);
        if (!profile.IsSuccess || profile.Value!.LifecycleState != OrganizationLifecycleState.Active)
            return OrganizationResult.Failure(OrganizationErrorCodes.InvalidReference, "Membership requires an Active Organization Profile.");
        if (organization == member || !entities.IsActive(member)) return OrganizationResult.Failure(
            OrganizationErrorCodes.InvalidReference, "New member must be a distinct Active Entity.");
        if (!ValidOptional(provenance)) return OrganizationResult.Failure(OrganizationErrorCodes.InvalidIdentifier,
            "Provenance must be normalized.");
        return ValidateRoles(roles);
    }

    private OrganizationResult ValidateMembership(MembershipSnapshot item,
        IReadOnlyDictionary<EntityId, OrganizationProfile> candidateProfiles)
    {
        if (item.Id.Value == Guid.Empty || item.OrganizationEntityId == item.MemberEntityId ||
            !entities.Exists(item.OrganizationEntityId) || !entities.Exists(item.MemberEntityId) ||
            !candidateProfiles.TryGetValue(item.OrganizationEntityId, out var profile))
            return OrganizationResult.Failure(OrganizationErrorCodes.InvalidReference, "Membership references are invalid.");
        if (!Enum.IsDefined(item.LifecycleState) || !Enum.IsDefined(profile.LifecycleState))
            return OrganizationResult.Failure(OrganizationErrorCodes.InvalidLifecycle, "Membership lifecycle is invalid.");
        if (item.CreatedAt.Value > item.LastChangedAt.Value) return OrganizationResult.Failure(
            OrganizationErrorCodes.InvalidTimestamp, "Membership creation cannot follow last change.");
        if (!ValidOptional(item.ProvenanceReference)) return OrganizationResult.Failure(
            OrganizationErrorCodes.InvalidIdentifier, "Membership provenance must be normalized.");
        return ValidateRoles(item.RoleIds);
    }

    private OrganizationResult<OrganizationProfile> FindMutableOrganization(EntityId id, WorldTimestamp timestamp,
        string? provenance)
    {
        var found = FindOrganization(id);
        if (!found.IsSuccess) return found;
        if (found.Value!.LifecycleState != OrganizationLifecycleState.Active) return OrganizationResult<OrganizationProfile>.Failure(
            OrganizationErrorCodes.InvalidLifecycle, "Only Active Organizations may change.");
        if (timestamp.Value < found.Value.LastChangedAt.Value) return OrganizationResult<OrganizationProfile>.Failure(
            OrganizationErrorCodes.InvalidTimestamp, "Change cannot precede the previous change.");
        if (!ValidOptional(provenance)) return OrganizationResult<OrganizationProfile>.Failure(
            OrganizationErrorCodes.InvalidIdentifier, "Provenance must be normalized.");
        return found;
    }

    private OrganizationResult<MembershipSnapshot> FindMutableMembership(MembershipId id, WorldTimestamp timestamp,
        string? provenance)
    {
        var found = FindMembership(id);
        if (!found.IsSuccess) return found;
        if (found.Value!.LifecycleState != MembershipLifecycleState.Active) return OrganizationResult<MembershipSnapshot>.Failure(
            OrganizationErrorCodes.InvalidLifecycle, "Only Active Memberships may change.");
        if (timestamp.Value < found.Value.LastChangedAt.Value) return OrganizationResult<MembershipSnapshot>.Failure(
            OrganizationErrorCodes.InvalidTimestamp, "Change cannot precede the previous change.");
        if (!ValidOptional(provenance)) return OrganizationResult<MembershipSnapshot>.Failure(
            OrganizationErrorCodes.InvalidIdentifier, "Provenance must be normalized.");
        return found;
    }

    private static OrganizationResult ValidateRoles(IReadOnlyList<OrganizationRoleId>? roles)
    {
        if (roles is null || roles.Any(role => !Valid(role.Value)) || roles.Distinct().Count() != roles.Count)
            return OrganizationResult.Failure(OrganizationErrorCodes.InvalidIdentifier,
                "Membership roles must be non-null, unique, and normalized.");
        return OrganizationResult.Success();
    }
    private static IReadOnlyList<OrganizationRoleId> CanonicalRoles(IEnumerable<OrganizationRoleId> roles) =>
        roles.OrderBy(role => role.Value, StringComparer.Ordinal).ToArray();
    private IReadOnlyList<MembershipSnapshot> Query(Func<MembershipSnapshot, bool> predicate) => memberships.Values
        .Where(predicate).OrderBy(item => item.Id.Value).ToArray();
    private OrganizationResult Publish(OrganizationDomainEvent value)
    {
        if (events is null) return OrganizationResult.Success();
        var result = events.Publish(value);
        return result.IsSuccess ? result : OrganizationResult.Failure(OrganizationErrorCodes.EventPublicationFailed,
            result.Error!.Message);
    }
    private static MembershipSnapshot Copy(MembershipSnapshot source, IReadOnlyList<OrganizationRoleId> roles,
        MembershipLifecycleState lifecycle, WorldTimestamp changed, string? provenance) => new(source.Id,
        source.OrganizationEntityId, source.MemberEntityId, roles, lifecycle, source.CreatedAt, changed, provenance);
    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static bool ValidOptional(string? value) => value is null || Valid(value);
    private static string? Normalize(string? value) => value?.Trim();
    private static OrganizationResult AsResult(OrganizationError error) => OrganizationResult.Failure(error.Code, error.Message);
    private static OrganizationResult<T> Fail<T>(OrganizationError error) => OrganizationResult<T>.Failure(error.Code, error.Message);
}
