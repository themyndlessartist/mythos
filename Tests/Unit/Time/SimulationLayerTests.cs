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

    [Fact]
    public void RestoreRejectsMaximumSequenceAtomically()
    {
        var layers = new SimulationLayerCoordinator();
        Assert.True(layers.Register(new SimulationLayerId("existing"), new WorldDuration(2), new WorldTimestamp(0)).IsSuccess);
        var invalid = new[]
        {
            new SimulationLayerSnapshot(new SimulationLayerId("invalid"), new WorldDuration(1), new WorldTimestamp(0), long.MaxValue, 1),
        };

        var result = layers.Restore(invalid, new WorldTimestamp(0));

        Assert.False(result.IsSuccess);
        Assert.Equal(TimeErrorCodes.InvalidSnapshot, result.Error!.Code);
        Assert.Equal("existing", Assert.Single(layers.ExportSnapshots()).Id.Value);
    }

    [Fact]
    public void ExportedSnapshotCollectionIsReadOnly()
    {
        var layers = new SimulationLayerCoordinator();
        layers.Register(new SimulationLayerId("layer"), new WorldDuration(1), new WorldTimestamp(0));
        var snapshots = layers.ExportSnapshots();

        Assert.Throws<NotSupportedException>(() => ((IList<SimulationLayerSnapshot>)snapshots)[0] = snapshots[0]);
    }
}
