using Mythos.Genesis;

namespace Mythos.Framework.UnitTests.Genesis;

public sealed class LakewoodSettlementStateTests
{
    private static readonly LakewoodProjectDefinition Project = new(
        "mythos-genesis.storehouse",
        "Lakewood Storehouse",
        "mythos-genesis.construction-site-one",
        [new("mythos-genesis.stone", 10), new("mythos-genesis.timber", 20)],
        8,
        "mythos-genesis.storehouse-complete-image",
        "mythos-genesis.storage-expanded");

    [Fact]
    public void SharedContributionsCompleteProjectAndConsumeRequirements()
    {
        var state = new LakewoodSettlementState(Project);
        Assert.True(state.ContributeResource("mythos-genesis.timber", 12).IsSuccess);
        Assert.True(state.ContributeResource("mythos-genesis.timber", 8).IsSuccess);
        Assert.True(state.ContributeResource("mythos-genesis.stone", 10).IsSuccess);
        Assert.True(state.ContributeLabor(3).IsSuccess);
        Assert.True(state.ContributeLabor(5).IsSuccess);

        Assert.True(state.TryComplete().IsSuccess);

        Assert.True(state.ProjectComplete);
        Assert.Equal("mythos-genesis.storage-expanded", state.CompletionStateId);
        Assert.Equal(0, state.Stockpile["mythos-genesis.timber"]);
        Assert.Equal(0, state.Stockpile["mythos-genesis.stone"]);
    }

    [Fact]
    public void CannotCompleteEarlyOrAcceptInvalidContributions()
    {
        var state = new LakewoodSettlementState(Project);
        Assert.False(state.ContributeResource("mythos-genesis.food", 1).IsSuccess);
        Assert.False(state.ContributeLabor(0).IsSuccess);
        Assert.False(state.TryComplete().IsSuccess);
        Assert.False(state.ProjectComplete);
    }

    [Fact]
    public void SnapshotRoundTripPreservesProgressAndCompletion()
    {
        var source = new LakewoodSettlementState(Project);
        Assert.True(source.ContributeResource("mythos-genesis.timber", 20).IsSuccess);
        Assert.True(source.ContributeResource("mythos-genesis.stone", 10).IsSuccess);
        Assert.True(source.ContributeLabor(8).IsSuccess);
        Assert.True(source.TryComplete().IsSuccess);

        var restored = new LakewoodSettlementState(Project);
        Assert.True(restored.Restore(source.ExportSnapshot()).IsSuccess);

        Assert.Equal(source.LaborContributed, restored.LaborContributed);
        Assert.Equal(source.ProjectComplete, restored.ProjectComplete);
        Assert.Equal(source.CompletionStateId, restored.CompletionStateId);
        Assert.Equal(source.Stockpile, restored.Stockpile);
    }
}
