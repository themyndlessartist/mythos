using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Time;

public sealed class SimulationLayerTests
{
    [Fact]
    public void LayersReportDueMarkersByTimestampThenRegistration()
    {
        var layers = new SimulationLayerCoordinator();
        layers.Register(new SimulationLayerId("slow"), new WorldDuration(4), new WorldTimestamp(0));
        layers.Register(new SimulationLayerId("fast"), new WorldDuration(2), new WorldTimestamp(0));

        var due = layers.QueryDue(new WorldTimestamp(4), 10, out var limited);

        Assert.Equal(["fast", "slow", "fast"], due.Select(item => item.Id.Value));
        Assert.False(limited);
    }

    [Fact]
    public void LayerCatchUpIsBoundedAndProgressRestores()
    {
        var layers = new SimulationLayerCoordinator();
        layers.Register(new SimulationLayerId("layer"), new WorldDuration(1), new WorldTimestamp(0));
        var first = layers.QueryDue(new WorldTimestamp(10), 3, out var limited);
        var restored = new SimulationLayerCoordinator();
        Assert.True(restored.Restore(layers.ExportSnapshots(), new WorldTimestamp(10)).IsSuccess);

        var next = restored.QueryDue(new WorldTimestamp(10), 1, out _);

        Assert.Equal(3, first.Count);
        Assert.True(limited);
        Assert.Equal(4, Assert.Single(next).Tick);
    }
}
