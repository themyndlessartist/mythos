namespace Mythos.Framework.Npcs;

public static class NpcErrorCodes
{
    public const string DuplicateProfile = "npc.duplicate_profile";
    public const string ProfileNotFound = "npc.profile_not_found";
    public const string InvalidIdentifier = "npc.invalid_identifier";
    public const string InvalidReference = "npc.invalid_reference";
    public const string InvalidLifecycle = "npc.invalid_lifecycle";
    public const string InvalidSchedule = "npc.invalid_schedule";
    public const string InvalidState = "npc.invalid_state";
    public const string InvalidSnapshot = "npc.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "npc.unsupported_snapshot_version";
}

public sealed record NpcError(string Code, string Message);

public readonly record struct NpcResult(NpcError? Error)
{
    public bool IsSuccess => Error is null;
    public static NpcResult Success() => new(null);
    public static NpcResult Failure(string code, string message) => new(new NpcError(code, message));
}

public readonly record struct NpcResult<T>(T? Value, NpcError? Error)
{
    public bool IsSuccess => Error is null;
    public static NpcResult<T> Success(T value) => new(value, null);
    public static NpcResult<T> Failure(string code, string message) => new(default, new NpcError(code, message));
}
