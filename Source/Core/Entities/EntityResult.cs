namespace Mythos.Framework.Entities;

public static class EntityErrorCodes
{
    public const string DuplicateId = "entity.duplicate_id";
    public const string InvalidId = "entity.invalid_id";
    public const string NotFound = "entity.not_found";
    public const string InvalidReference = "entity.invalid_reference";
    public const string SelfReference = "entity.self_reference";
    public const string HierarchyCycle = "entity.hierarchy_cycle";
    public const string OwnershipCycle = "entity.ownership_cycle";
    public const string InvalidLifecycleTransition = "entity.invalid_lifecycle_transition";
    public const string InvalidTimestamp = "entity.invalid_timestamp";
    public const string InvalidSnapshot = "entity.invalid_snapshot";
    public const string InvalidIdentifier = "entity.invalid_identifier";
}

public sealed record EntityError(string Code, string Message);

public readonly record struct EntityResult<T>(T? Value, EntityError? Error)
{
    public bool IsSuccess => Error is null;

    public static EntityResult<T> Success(T value) => new(value, null);

    public static EntityResult<T> Failure(string code, string message) =>
        new(default, new EntityError(code, message));
}

public readonly record struct EntityResult(EntityError? Error)
{
    public bool IsSuccess => Error is null;

    public static EntityResult Success() => new(null);

    public static EntityResult Failure(string code, string message) =>
        new(new EntityError(code, message));
}
