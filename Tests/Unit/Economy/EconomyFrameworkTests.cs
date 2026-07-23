using Mythos.Framework.Economy;
using Mythos.Framework.Entities;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Economy;

public sealed class EconomyFrameworkTests
{
    private static readonly CurrencyId Currency = new("unit");

    [Fact]
    public void AccountsTransferAndQueriesPreserveLedgerBalances()
    {
        var fixture = CreateFixture();
        var source = fixture.Framework.OpenAccount(fixture.First, Currency, 100, new WorldTimestamp(1)).Value!;
        var destination = fixture.Framework.OpenAccount(fixture.Second, Currency, 5, new WorldTimestamp(1)).Value!;
        var transfer = fixture.Framework.Transfer(source.Id, destination.Id, 25, new WorldTimestamp(2), "trade:1").Value!;
        Assert.Equal(75, fixture.Framework.FindAccount(source.Id).Value!.Balance);
        Assert.Equal(30, fixture.Framework.FindAccount(destination.Id).Value!.Balance);
        Assert.Single(fixture.Framework.QueryTransfersByAccount(source.Id));
        Assert.Single(fixture.Framework.QueryTransfersByOwner(fixture.Second));
        Assert.Single(fixture.Framework.QueryTransfersByCorrelation("trade:1"));
        Assert.Equal(transfer.Id, fixture.Framework.QueryTransfersByTime(new(2), new(2)).Single().Id);
        Assert.True(fixture.Framework.ValidateReferences().IsSuccess);
    }

    [Fact]
    public void AccountsEnforceOwnerCurrencyBalanceAndActiveKey()
    {
        var fixture = CreateFixture();
        Assert.Equal(EconomyErrorCodes.InvalidAmount,
            fixture.Framework.OpenAccount(fixture.First, Currency, -1, new WorldTimestamp(1)).Error?.Code);
        Assert.True(fixture.Framework.OpenAccount(fixture.First, Currency, 0, new WorldTimestamp(1)).IsSuccess);
        Assert.Equal(EconomyErrorCodes.DuplicateActiveAccount,
            fixture.Framework.OpenAccount(fixture.First, Currency, 0, new WorldTimestamp(2)).Error?.Code);
        Assert.True(fixture.Entities.Retire(fixture.Second, 1).IsSuccess);
        Assert.Equal(EconomyErrorCodes.InvalidReference,
            fixture.Framework.OpenAccount(fixture.Second, Currency, 0, new WorldTimestamp(2)).Error?.Code);
    }

    [Fact]
    public void TransfersRejectInvalidCasesWithoutMutation()
    {
        var fixture = CreateFixture();
        var source = fixture.Framework.OpenAccount(fixture.First, Currency, 10, new WorldTimestamp(1)).Value!;
        var destination = fixture.Framework.OpenAccount(fixture.Second, Currency, 0, new WorldTimestamp(1)).Value!;
        var other = fixture.Framework.OpenAccount(fixture.Third, new CurrencyId("other"), 0, new WorldTimestamp(1)).Value!;
        Assert.Equal(EconomyErrorCodes.InsufficientFunds,
            fixture.Framework.Transfer(source.Id, destination.Id, 11, new WorldTimestamp(2)).Error?.Code);
        Assert.Equal(EconomyErrorCodes.CurrencyMismatch,
            fixture.Framework.Transfer(source.Id, other.Id, 1, new WorldTimestamp(2)).Error?.Code);
        Assert.Equal(EconomyErrorCodes.InvalidReference,
            fixture.Framework.Transfer(source.Id, source.Id, 1, new WorldTimestamp(2)).Error?.Code);
        Assert.Equal(10, fixture.Framework.FindAccount(source.Id).Value!.Balance);
        Assert.Equal(0, fixture.Framework.TransferCount);
    }

    [Fact]
    public void ClosingRequiresZeroAndPermitsReplacement()
    {
        var fixture = CreateFixture();
        var source = fixture.Framework.OpenAccount(fixture.First, Currency, 1, new WorldTimestamp(1)).Value!;
        var destination = fixture.Framework.OpenAccount(fixture.Second, Currency, 0, new WorldTimestamp(1)).Value!;
        Assert.Equal(EconomyErrorCodes.InvalidAmount,
            fixture.Framework.CloseAccount(source.Id, new WorldTimestamp(2)).Error?.Code);
        Assert.True(fixture.Framework.Transfer(source.Id, destination.Id, 1, new WorldTimestamp(2)).IsSuccess);
        Assert.True(fixture.Framework.CloseAccount(source.Id, new WorldTimestamp(3)).IsSuccess);
        Assert.True(fixture.Framework.OpenAccount(fixture.First, Currency, 0, new WorldTimestamp(4)).IsSuccess);
    }

    [Fact]
    public void TerminalOwnerRemainsValidForHistoricalContinuity()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Framework.OpenAccount(fixture.First, Currency, 0, new WorldTimestamp(1)).IsSuccess);
        Assert.True(fixture.Entities.Destroy(fixture.First, 2).IsSuccess);
        Assert.True(fixture.Framework.ValidateReferences().IsSuccess);
    }

    [Fact]
    public void EventFailureLeavesAccountsAndLedgerUnchanged()
    {
        var sink = new FailingSink { Fail = true };
        var fixture = CreateFixture(sink);
        Assert.Equal(EconomyErrorCodes.EventPublicationFailed,
            fixture.Framework.OpenAccount(fixture.First, Currency, 10, new WorldTimestamp(1)).Error?.Code);
        sink.Fail = false;
        var source = fixture.Framework.OpenAccount(fixture.First, Currency, 10, new WorldTimestamp(1)).Value!;
        var destination = fixture.Framework.OpenAccount(fixture.Second, Currency, 0, new WorldTimestamp(1)).Value!;
        sink.Fail = true;
        Assert.Equal(EconomyErrorCodes.EventPublicationFailed,
            fixture.Framework.Transfer(source.Id, destination.Id, 1, new WorldTimestamp(2)).Error?.Code);
        Assert.Equal(10, fixture.Framework.FindAccount(source.Id).Value!.Balance);
        Assert.Equal(0, fixture.Framework.TransferCount);
    }

    [Fact]
    public void SnapshotIsDefensiveDeterministicAndRejectsLedgerMismatchAtomically()
    {
        var fixture = CreateFixture();
        var source = fixture.Framework.OpenAccount(fixture.First, Currency, 10, new WorldTimestamp(1)).Value!;
        var destination = fixture.Framework.OpenAccount(fixture.Second, Currency, 0, new WorldTimestamp(1)).Value!;
        Assert.True(fixture.Framework.Transfer(source.Id, destination.Id, 2, new WorldTimestamp(2)).IsSuccess);
        var snapshot = fixture.Framework.ExportSnapshot();
        var accountSource = snapshot.Accounts!.ToList();
        var defensive = new EconomyFrameworkSnapshot(1, accountSource, snapshot.Transfers);
        accountSource.Clear();
        Assert.Equal(2, defensive.Accounts!.Count);
        var restored = new EconomyFramework(fixture.Entities);
        Assert.True(restored.RestoreSnapshot(snapshot).IsSuccess);
        var before = restored.ExportSnapshot();
        var malformed = snapshot.Accounts![0] with { Balance = 999 };
        Assert.Equal(EconomyErrorCodes.LedgerMismatch,
            restored.RestoreSnapshot(new EconomyFrameworkSnapshot(1, [malformed, snapshot.Accounts[1]], snapshot.Transfers)).Error?.Code);
        Assert.Equal(before.Accounts, restored.ExportSnapshot().Accounts);
    }

    [Fact]
    public void RestoreRejectsNullVersionAndDuplicateIds()
    {
        var fixture = CreateFixture();
        Assert.Equal(EconomyErrorCodes.InvalidSnapshot, fixture.Framework.RestoreSnapshot(null).Error?.Code);
        Assert.Equal(EconomyErrorCodes.UnsupportedSnapshotVersion,
            fixture.Framework.RestoreSnapshot(new EconomyFrameworkSnapshot(0, [], [])).Error?.Code);
        var account = new EconomyAccountSnapshot(new EconomyAccountId(Guid.Parse("10000000-0000-0000-0000-000000000000")),
            fixture.First, Currency, 0, 0, EconomyAccountLifecycleState.Active, new WorldTimestamp(1), new WorldTimestamp(1), null);
        Assert.Equal(EconomyErrorCodes.DuplicateId,
            fixture.Framework.RestoreSnapshot(new EconomyFrameworkSnapshot(1, [account, account], [])).Error?.Code);
    }

    private static Fixture CreateFixture(IEconomyEventSink? sink = null)
    {
        var entities = new EntityRegistry();
        var first = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var second = entities.Create(new EntityCategory("Character"), 0).Value!.Id;
        var third = entities.Create(new EntityCategory("Organization"), 0).Value!.Id;
        return new Fixture(entities, new EconomyFramework(entities, new FixedIds(), sink), first, second, third);
    }
    private sealed record Fixture(EntityRegistry Entities, EconomyFramework Framework, EntityId First, EntityId Second, EntityId Third);
    private sealed class FixedIds : IEconomyIdGenerator
    {
        private int next = 1;
        public EconomyAccountId CreateAccountId() => new(new Guid(next++, 0, 0, new byte[8]));
        public EconomyTransferId CreateTransferId() => new(new Guid(next++, 0, 0, new byte[8]));
    }
    private sealed class FailingSink : IEconomyEventSink
    {
        public bool Fail { get; set; }
        public EconomyResult Publish(EconomyDomainEvent domainEvent) => Fail
            ? EconomyResult.Failure("event.failed", "Fixture event failed.") : EconomyResult.Success();
    }
}
