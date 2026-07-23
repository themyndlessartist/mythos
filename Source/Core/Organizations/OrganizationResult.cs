namespace Mythos.Framework.Organizations;

public static class OrganizationErrorCodes
{
    public const string NotFound = "organization.not_found";
    public const string DuplicateProfile = "organization.duplicate_profile";
    public const string DuplicateMembershipId = "organization.duplicate_membership_id";
    public const string DuplicateActiveMembership = "organization.duplicate_active_membership";
    public const string InvalidIdentifier = "organization.invalid_identifier";
    public const string InvalidReference = "organization.invalid_reference";
    public const string InvalidLifecycle = "organization.invalid_lifecycle";
    public const string InvalidTimestamp = "organization.invalid_timestamp";
    public const string InvalidSnapshot = "organization.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "organization.unsupported_snapshot_version";
    public const string ActiveMembershipsRemain = "organization.active_memberships_remain";
    public const string EventPublicationFailed = "organization.event_publication_failed";
}

public sealed record OrganizationError(string Code, string Message);
public readonly record struct OrganizationResult(OrganizationError? Error)
{
    public bool IsSuccess => Error is null;
    public static OrganizationResult Success() => new(null);
    public static OrganizationResult Failure(string code, string message) => new(new(code, message));
}
public readonly record struct OrganizationResult<T>(T? Value, OrganizationError? Error)
{
    public bool IsSuccess => Error is null;
    public static OrganizationResult<T> Success(T value) => new(value, null);
    public static OrganizationResult<T> Failure(string code, string message) => new(default, new(code, message));
}
