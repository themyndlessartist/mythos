namespace Mythos.Framework.Economy;

public static class EconomyErrorCodes
{
    public const string NotFound = "economy.not_found";
    public const string DuplicateId = "economy.duplicate_id";
    public const string DuplicateActiveAccount = "economy.duplicate_active_account";
    public const string InvalidIdentifier = "economy.invalid_identifier";
    public const string InvalidReference = "economy.invalid_reference";
    public const string InvalidAmount = "economy.invalid_amount";
    public const string InsufficientFunds = "economy.insufficient_funds";
    public const string CurrencyMismatch = "economy.currency_mismatch";
    public const string InvalidLifecycle = "economy.invalid_lifecycle";
    public const string InvalidTimestamp = "economy.invalid_timestamp";
    public const string InvalidSnapshot = "economy.invalid_snapshot";
    public const string UnsupportedSnapshotVersion = "economy.unsupported_snapshot_version";
    public const string LedgerMismatch = "economy.ledger_mismatch";
    public const string EventPublicationFailed = "economy.event_publication_failed";
}
public sealed record EconomyError(string Code, string Message);
public readonly record struct EconomyResult(EconomyError? Error)
{
    public bool IsSuccess => Error is null;
    public static EconomyResult Success() => new(null);
    public static EconomyResult Failure(string code, string message) => new(new(code, message));
}
public readonly record struct EconomyResult<T>(T? Value, EconomyError? Error)
{
    public bool IsSuccess => Error is null;
    public static EconomyResult<T> Success(T value) => new(value, null);
    public static EconomyResult<T> Failure(string code, string message) => new(default, new(code, message));
}
