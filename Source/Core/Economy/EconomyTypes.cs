using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Economy;

public readonly record struct EconomyAccountId(Guid Value) { public override string ToString() => Value.ToString("D"); }
public readonly record struct EconomyTransferId(Guid Value) { public override string ToString() => Value.ToString("D"); }
public interface IEconomyIdGenerator
{
    EconomyAccountId CreateAccountId();
    EconomyTransferId CreateTransferId();
}
public sealed class Version7EconomyIdGenerator : IEconomyIdGenerator
{
    public EconomyAccountId CreateAccountId() => new(Guid.CreateVersion7());
    public EconomyTransferId CreateTransferId() => new(Guid.CreateVersion7());
}
public readonly record struct CurrencyId
{
    public CurrencyId(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); Value = value.Trim(); }
    public string Value { get; }
    public override string ToString() => Value;
}
public enum EconomyAccountLifecycleState { Active, Closed }
public sealed record EconomyAccountSnapshot(EconomyAccountId Id, EntityId OwnerEntityId, CurrencyId CurrencyId,
    long OpeningBalance, long Balance, EconomyAccountLifecycleState LifecycleState, WorldTimestamp OpenedAt,
    WorldTimestamp LastChangedAt, string? ProvenanceReference);
public sealed record EconomyTransferSnapshot(EconomyTransferId Id, EconomyAccountId SourceAccountId,
    EconomyAccountId DestinationAccountId, long Amount, WorldTimestamp OccurredAt, string? CorrelationReference,
    string? ProvenanceReference);
public sealed record EconomyFrameworkSnapshot
{
    public const int CurrentVersion = 1;
    public EconomyFrameworkSnapshot(int version, IReadOnlyList<EconomyAccountSnapshot>? accounts,
        IReadOnlyList<EconomyTransferSnapshot>? transfers)
    {
        Version = version;
        Accounts = accounts is null ? null : Array.AsReadOnly(accounts.ToArray());
        Transfers = transfers is null ? null : Array.AsReadOnly(transfers.ToArray());
    }
    public int Version { get; }
    public IReadOnlyList<EconomyAccountSnapshot>? Accounts { get; }
    public IReadOnlyList<EconomyTransferSnapshot>? Transfers { get; }
}
public sealed record EconomyDomainEvent(string Type, WorldTimestamp OccurredAt, EconomyAccountId? AccountId = null,
    EconomyTransferId? TransferId = null, EntityId? OwnerEntityId = null, long? Amount = null);
public interface IEconomyEventSink { EconomyResult Publish(EconomyDomainEvent domainEvent); }
public sealed record EconomyAccountDiagnostic(EconomyAccountSnapshot Account, long ComputedBalance, string ValidationStatus);
