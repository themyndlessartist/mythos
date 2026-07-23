using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.Economy;

/// <summary>Owns deterministic currency accounts and an immutable transfer ledger.</summary>
public sealed class EconomyFramework
{
    private readonly EntityRegistry entities;
    private readonly IEconomyIdGenerator ids;
    private readonly IEconomyEventSink? events;
    private Dictionary<EconomyAccountId, EconomyAccountSnapshot> accounts = [];
    private Dictionary<EconomyTransferId, EconomyTransferSnapshot> transfers = [];

    public EconomyFramework(EntityRegistry entities, IEconomyIdGenerator? ids = null, IEconomyEventSink? events = null)
    {
        this.entities = entities ?? throw new ArgumentNullException(nameof(entities));
        this.ids = ids ?? new Version7EconomyIdGenerator();
        this.events = events;
    }
    public int AccountCount => accounts.Count;
    public int TransferCount => transfers.Count;

    public EconomyResult<EconomyAccountSnapshot> OpenAccount(EntityId ownerId, CurrencyId currencyId,
        long openingBalance, WorldTimestamp timestamp, string? provenanceReference = null)
    {
        var valid = ValidateAccountInput(ownerId, currencyId, openingBalance, openingBalance, timestamp, timestamp,
            provenanceReference, requireActiveOwner: true);
        if (!valid.IsSuccess) return Fail<EconomyAccountSnapshot>(valid.Error!);
        if (FindActiveAccount(ownerId, currencyId).IsSuccess) return EconomyResult<EconomyAccountSnapshot>.Failure(
            EconomyErrorCodes.DuplicateActiveAccount, "An Active account already exists for this owner and currency.");
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = ids.CreateAccountId();
            if (id.Value == Guid.Empty || accounts.ContainsKey(id)) continue;
            var account = new EconomyAccountSnapshot(id, ownerId, currencyId, openingBalance, openingBalance,
                EconomyAccountLifecycleState.Active, timestamp, timestamp, Normalize(provenanceReference));
            var published = Publish(new("EconomyAccountOpened", timestamp, id, OwnerEntityId: ownerId,
                Amount: openingBalance));
            if (!published.IsSuccess) return Fail<EconomyAccountSnapshot>(published.Error!);
            accounts.Add(id, account);
            return EconomyResult<EconomyAccountSnapshot>.Success(account);
        }
        return EconomyResult<EconomyAccountSnapshot>.Failure(EconomyErrorCodes.DuplicateId,
            "Account ID generator did not produce a unique initialized ID.");
    }

    public EconomyResult<EconomyAccountSnapshot> FindAccount(EconomyAccountId id) => accounts.TryGetValue(id, out var value)
        ? EconomyResult<EconomyAccountSnapshot>.Success(value)
        : EconomyResult<EconomyAccountSnapshot>.Failure(EconomyErrorCodes.NotFound, "Economy account was not found.");
    public EconomyResult<EconomyTransferSnapshot> FindTransfer(EconomyTransferId id) => transfers.TryGetValue(id, out var value)
        ? EconomyResult<EconomyTransferSnapshot>.Success(value)
        : EconomyResult<EconomyTransferSnapshot>.Failure(EconomyErrorCodes.NotFound, "Economy transfer was not found.");
    public EconomyResult<EconomyAccountSnapshot> FindActiveAccount(EntityId ownerId, CurrencyId currencyId)
    {
        var value = accounts.Values.FirstOrDefault(item => item.LifecycleState == EconomyAccountLifecycleState.Active &&
            item.OwnerEntityId == ownerId && item.CurrencyId == currencyId);
        return value is null ? EconomyResult<EconomyAccountSnapshot>.Failure(EconomyErrorCodes.NotFound,
            "Active account was not found.") : EconomyResult<EconomyAccountSnapshot>.Success(value);
    }

    public EconomyResult<EconomyTransferSnapshot> Transfer(EconomyAccountId sourceId, EconomyAccountId destinationId,
        long amount, WorldTimestamp timestamp, string? correlationReference = null, string? provenanceReference = null)
    {
        if (sourceId == destinationId) return EconomyResult<EconomyTransferSnapshot>.Failure(
            EconomyErrorCodes.InvalidReference, "Source and destination accounts must differ.");
        var source = FindAccount(sourceId);
        var destination = FindAccount(destinationId);
        if (!source.IsSuccess || !destination.IsSuccess) return EconomyResult<EconomyTransferSnapshot>.Failure(
            EconomyErrorCodes.InvalidReference, "Transfer accounts must exist.");
        if (source.Value!.LifecycleState != EconomyAccountLifecycleState.Active ||
            destination.Value!.LifecycleState != EconomyAccountLifecycleState.Active)
            return EconomyResult<EconomyTransferSnapshot>.Failure(EconomyErrorCodes.InvalidLifecycle,
                "Transfers require Active accounts.");
        if (source.Value.CurrencyId != destination.Value.CurrencyId) return EconomyResult<EconomyTransferSnapshot>.Failure(
            EconomyErrorCodes.CurrencyMismatch, "Transfer accounts must use the same currency.");
        if (amount <= 0) return EconomyResult<EconomyTransferSnapshot>.Failure(EconomyErrorCodes.InvalidAmount,
            "Transfer amount must be positive.");
        if (source.Value.Balance < amount) return EconomyResult<EconomyTransferSnapshot>.Failure(
            EconomyErrorCodes.InsufficientFunds, "Source account has insufficient funds.");
        if (timestamp.Value < source.Value.LastChangedAt.Value || timestamp.Value < destination.Value.LastChangedAt.Value)
            return EconomyResult<EconomyTransferSnapshot>.Failure(EconomyErrorCodes.InvalidTimestamp,
                "Transfer cannot precede an account's last change.");
        if (!ValidOptional(correlationReference) || !ValidOptional(provenanceReference))
            return EconomyResult<EconomyTransferSnapshot>.Failure(EconomyErrorCodes.InvalidIdentifier,
                "Transfer references must be normalized.");
        long destinationBalance;
        try { destinationBalance = checked(destination.Value.Balance + amount); }
        catch (OverflowException)
        {
            return EconomyResult<EconomyTransferSnapshot>.Failure(
            EconomyErrorCodes.InvalidAmount, "Destination balance would overflow.");
        }
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var id = ids.CreateTransferId();
            if (id.Value == Guid.Empty || transfers.ContainsKey(id)) continue;
            var transfer = new EconomyTransferSnapshot(id, sourceId, destinationId, amount, timestamp,
                Normalize(correlationReference), Normalize(provenanceReference));
            var published = Publish(new("EconomyValueTransferred", timestamp, TransferId: id, Amount: amount));
            if (!published.IsSuccess) return Fail<EconomyTransferSnapshot>(published.Error!);
            accounts[sourceId] = source.Value with { Balance = source.Value.Balance - amount, LastChangedAt = timestamp };
            accounts[destinationId] = destination.Value with { Balance = destinationBalance, LastChangedAt = timestamp };
            transfers.Add(id, transfer);
            return EconomyResult<EconomyTransferSnapshot>.Success(transfer);
        }
        return EconomyResult<EconomyTransferSnapshot>.Failure(EconomyErrorCodes.DuplicateId,
            "Transfer ID generator did not produce a unique initialized ID.");
    }

    public EconomyResult CloseAccount(EconomyAccountId id, WorldTimestamp timestamp,
        string? provenanceReference = null)
    {
        var found = FindAccount(id);
        if (!found.IsSuccess) return AsResult(found.Error!);
        if (found.Value!.LifecycleState != EconomyAccountLifecycleState.Active) return EconomyResult.Failure(
            EconomyErrorCodes.InvalidLifecycle, "Only Active accounts may close.");
        if (found.Value.Balance != 0) return EconomyResult.Failure(EconomyErrorCodes.InvalidAmount,
            "Only zero-balance accounts may close.");
        if (timestamp.Value < found.Value.LastChangedAt.Value) return EconomyResult.Failure(
            EconomyErrorCodes.InvalidTimestamp, "Closure cannot precede the account's last change.");
        if (!ValidOptional(provenanceReference)) return EconomyResult.Failure(EconomyErrorCodes.InvalidIdentifier,
            "Provenance must be normalized.");
        var published = Publish(new("EconomyAccountClosed", timestamp, id, OwnerEntityId: found.Value.OwnerEntityId));
        if (!published.IsSuccess) return published;
        accounts[id] = found.Value with
        {
            LifecycleState = EconomyAccountLifecycleState.Closed,
            LastChangedAt = timestamp,
            ProvenanceReference = Normalize(provenanceReference)
        };
        return EconomyResult.Success();
    }

    public IReadOnlyList<EconomyAccountSnapshot> QueryAccountsByOwner(EntityId id) => QueryAccounts(item => item.OwnerEntityId == id);
    public IReadOnlyList<EconomyAccountSnapshot> QueryAccountsByCurrency(CurrencyId id) => QueryAccounts(item => item.CurrencyId == id);
    public IReadOnlyList<EconomyAccountSnapshot> QueryAccountsByLifecycle(EconomyAccountLifecycleState state) =>
        QueryAccounts(item => item.LifecycleState == state);
    public IReadOnlyList<EconomyTransferSnapshot> QueryTransfersByAccount(EconomyAccountId id) => QueryTransfers(item =>
        item.SourceAccountId == id || item.DestinationAccountId == id);
    public IReadOnlyList<EconomyTransferSnapshot> QueryTransfersByOwner(EntityId id) => QueryTransfers(item =>
        accounts[item.SourceAccountId].OwnerEntityId == id || accounts[item.DestinationAccountId].OwnerEntityId == id);
    public IReadOnlyList<EconomyTransferSnapshot> QueryTransfersByCurrency(CurrencyId id) => QueryTransfers(item =>
        accounts[item.SourceAccountId].CurrencyId == id);
    public IReadOnlyList<EconomyTransferSnapshot> QueryTransfersByCorrelation(string reference) =>
        QueryTransfers(item => item.CorrelationReference == reference);
    public IReadOnlyList<EconomyTransferSnapshot> QueryTransfersByTime(WorldTimestamp start, WorldTimestamp end) =>
        start.Value > end.Value ? [] : QueryTransfers(item => item.OccurredAt.Value >= start.Value && item.OccurredAt.Value <= end.Value);

    public EconomyResult ValidateReferences() => ValidateCandidate(accounts, transfers);
    public EconomyResult<EconomyAccountDiagnostic> Inspect(EconomyAccountId id)
    {
        var found = FindAccount(id);
        if (!found.IsSuccess) return EconomyResult<EconomyAccountDiagnostic>.Failure(found.Error!.Code, found.Error.Message);
        var account = found.Value!;
        var computed = ComputeBalance(account, transfers.Values);
        var status = computed.IsSuccess && computed.Value == account.Balance ? "valid" :
            computed.IsSuccess ? $"{EconomyErrorCodes.LedgerMismatch}: Balance does not match ledger." :
            $"{computed.Error!.Code}: {computed.Error.Message}";
        return EconomyResult<EconomyAccountDiagnostic>.Success(new(account, computed.Value, status));
    }

    public EconomyFrameworkSnapshot ExportSnapshot() => new(EconomyFrameworkSnapshot.CurrentVersion,
        accounts.Values.OrderBy(item => item.Id.Value).ToArray(),
        transfers.Values.OrderBy(item => item.OccurredAt.Value).ThenBy(item => item.Id.Value).ToArray());

    public EconomyResult RestoreSnapshot(EconomyFrameworkSnapshot? snapshot)
    {
        if (snapshot is null) return EconomyResult.Failure(EconomyErrorCodes.InvalidSnapshot, "Economy snapshot cannot be null.");
        if (snapshot.Version != EconomyFrameworkSnapshot.CurrentVersion) return EconomyResult.Failure(
            EconomyErrorCodes.UnsupportedSnapshotVersion, "Economy snapshot version is unsupported.");
        if (snapshot.Accounts is null || snapshot.Transfers is null || snapshot.Accounts.Any(item => item is null) ||
            snapshot.Transfers.Any(item => item is null)) return EconomyResult.Failure(EconomyErrorCodes.InvalidSnapshot,
            "Economy snapshot collections are malformed.");
        var candidateAccounts = new Dictionary<EconomyAccountId, EconomyAccountSnapshot>();
        foreach (var account in snapshot.Accounts)
            if (!candidateAccounts.TryAdd(account.Id, account)) return EconomyResult.Failure(EconomyErrorCodes.DuplicateId,
                "Snapshot contains duplicate Account IDs.");
        var candidateTransfers = new Dictionary<EconomyTransferId, EconomyTransferSnapshot>();
        foreach (var transfer in snapshot.Transfers)
            if (!candidateTransfers.TryAdd(transfer.Id, transfer)) return EconomyResult.Failure(EconomyErrorCodes.DuplicateId,
                "Snapshot contains duplicate Transfer IDs.");
        var valid = ValidateCandidate(candidateAccounts, candidateTransfers);
        if (!valid.IsSuccess) return valid;
        accounts = candidateAccounts;
        transfers = candidateTransfers;
        return EconomyResult.Success();
    }

    private EconomyResult ValidateCandidate(IReadOnlyDictionary<EconomyAccountId, EconomyAccountSnapshot> accountSet,
        IReadOnlyDictionary<EconomyTransferId, EconomyTransferSnapshot> transferSet)
    {
        foreach (var account in accountSet.Values)
        {
            var valid = ValidateAccount(account);
            if (!valid.IsSuccess) return valid;
        }
        if (accountSet.Values.Where(item => item.LifecycleState == EconomyAccountLifecycleState.Active)
            .GroupBy(item => (item.OwnerEntityId, item.CurrencyId)).Any(group => group.Count() > 1))
            return EconomyResult.Failure(EconomyErrorCodes.DuplicateActiveAccount,
                "Snapshot contains duplicate Active owner/currency accounts.");
        foreach (var transfer in transferSet.Values)
        {
            var valid = ValidateTransfer(transfer, accountSet);
            if (!valid.IsSuccess) return valid;
        }
        foreach (var account in accountSet.Values)
        {
            var computed = ComputeBalance(account, transferSet.Values);
            if (!computed.IsSuccess) return AsResult(computed.Error!);
            if (computed.Value != account.Balance) return EconomyResult.Failure(EconomyErrorCodes.LedgerMismatch,
                "Account balance does not match opening balance and transfers.");
        }
        return EconomyResult.Success();
    }

    private EconomyResult ValidateAccount(EconomyAccountSnapshot item)
    {
        if (item.Id.Value == Guid.Empty) return EconomyResult.Failure(EconomyErrorCodes.InvalidIdentifier,
            "Account ID must be initialized.");
        var valid = ValidateAccountInput(item.OwnerEntityId, item.CurrencyId, item.OpeningBalance, item.Balance,
            item.OpenedAt, item.LastChangedAt, item.ProvenanceReference, requireActiveOwner: false);
        if (!valid.IsSuccess) return valid;
        if (!Enum.IsDefined(item.LifecycleState)) return EconomyResult.Failure(EconomyErrorCodes.InvalidLifecycle,
            "Account lifecycle is invalid.");
        if (item.LifecycleState == EconomyAccountLifecycleState.Closed && item.Balance != 0)
            return EconomyResult.Failure(EconomyErrorCodes.InvalidAmount, "Closed accounts require zero balance.");
        return EconomyResult.Success();
    }

    private EconomyResult ValidateAccountInput(EntityId owner, CurrencyId currency, long openingBalance, long balance,
        WorldTimestamp opened, WorldTimestamp changed, string? provenance, bool requireActiveOwner)
    {
        if (!entities.Exists(owner) || requireActiveOwner && !entities.IsActive(owner)) return EconomyResult.Failure(
            EconomyErrorCodes.InvalidReference, "Account owner must be a registered Entity in an allowed lifecycle.");
        if (!Valid(currency.Value) || !ValidOptional(provenance)) return EconomyResult.Failure(
            EconomyErrorCodes.InvalidIdentifier, "Economy identifiers must be normalized.");
        if (openingBalance < 0 || balance < 0) return EconomyResult.Failure(EconomyErrorCodes.InvalidAmount,
            "Economy balances cannot be negative.");
        return opened.Value <= changed.Value ? EconomyResult.Success() : EconomyResult.Failure(
            EconomyErrorCodes.InvalidTimestamp, "Account opening cannot follow last change.");
    }

    private static EconomyResult ValidateTransfer(EconomyTransferSnapshot item,
        IReadOnlyDictionary<EconomyAccountId, EconomyAccountSnapshot> accountSet)
    {
        if (item.Id.Value == Guid.Empty || item.SourceAccountId == item.DestinationAccountId ||
            !accountSet.TryGetValue(item.SourceAccountId, out var source) ||
            !accountSet.TryGetValue(item.DestinationAccountId, out var destination))
            return EconomyResult.Failure(EconomyErrorCodes.InvalidReference, "Transfer references are invalid.");
        if (item.Amount <= 0) return EconomyResult.Failure(EconomyErrorCodes.InvalidAmount, "Transfer amount must be positive.");
        if (source.CurrencyId != destination.CurrencyId) return EconomyResult.Failure(EconomyErrorCodes.CurrencyMismatch,
            "Transfer account currencies do not match.");
        if (item.OccurredAt.Value < source.OpenedAt.Value || item.OccurredAt.Value < destination.OpenedAt.Value)
            return EconomyResult.Failure(EconomyErrorCodes.InvalidTimestamp, "Transfer precedes an account opening.");
        if (!ValidOptional(item.CorrelationReference) || !ValidOptional(item.ProvenanceReference))
            return EconomyResult.Failure(EconomyErrorCodes.InvalidIdentifier, "Transfer references must be normalized.");
        return EconomyResult.Success();
    }

    private static EconomyResult<long> ComputeBalance(EconomyAccountSnapshot account,
        IEnumerable<EconomyTransferSnapshot> ledger)
    {
        var balance = account.OpeningBalance;
        try
        {
            foreach (var item in ledger.OrderBy(item => item.OccurredAt.Value).ThenBy(item => item.Id.Value))
            {
                if (item.SourceAccountId == account.Id) balance = checked(balance - item.Amount);
                if (item.DestinationAccountId == account.Id) balance = checked(balance + item.Amount);
                if (balance < 0) return EconomyResult<long>.Failure(EconomyErrorCodes.InsufficientFunds,
                    "Ledger causes a negative intermediate balance.");
            }
        }
        catch (OverflowException)
        {
            return EconomyResult<long>.Failure(EconomyErrorCodes.InvalidAmount,
            "Ledger balance arithmetic overflowed.");
        }
        return EconomyResult<long>.Success(balance);
    }

    private IReadOnlyList<EconomyAccountSnapshot> QueryAccounts(Func<EconomyAccountSnapshot, bool> predicate) =>
        accounts.Values.Where(predicate).OrderBy(item => item.Id.Value).ToArray();
    private IReadOnlyList<EconomyTransferSnapshot> QueryTransfers(Func<EconomyTransferSnapshot, bool> predicate) =>
        transfers.Values.Where(predicate).OrderBy(item => item.OccurredAt.Value).ThenBy(item => item.Id.Value).ToArray();
    private EconomyResult Publish(EconomyDomainEvent value)
    {
        if (events is null) return EconomyResult.Success();
        var result = events.Publish(value);
        return result.IsSuccess ? result : EconomyResult.Failure(EconomyErrorCodes.EventPublicationFailed, result.Error!.Message);
    }
    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static bool ValidOptional(string? value) => value is null || Valid(value);
    private static string? Normalize(string? value) => value?.Trim();
    private static EconomyResult AsResult(EconomyError error) => EconomyResult.Failure(error.Code, error.Message);
    private static EconomyResult<T> Fail<T>(EconomyError error) => EconomyResult<T>.Failure(error.Code, error.Message);
}
