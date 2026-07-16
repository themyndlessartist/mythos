using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Time;

public sealed class TimeSchedulerTests
{
    [Fact]
    public void AbsoluteRelativeAndRecurringSchedulesAreDeterministic()
    {
        var scheduler = new TimeScheduler();
        scheduler.ScheduleAbsolute(Id("later"), At(10), At(0), "test");
        scheduler.ScheduleAfter(Id("first"), new WorldDuration(5), At(0), "test", recurrence: new WorldDuration(2));
        scheduler.ScheduleAbsolute(Id("tie"), At(5), At(0), "test");

        var due = scheduler.DrainDue(At(10), 10);

        Assert.Equal(["first", "tie", "first", "first", "later"], due.DueTasks.Select(item => item.Id.Value));
        Assert.Equal([0L, 0L, 1L, 2L, 0L], due.DueTasks.Select(item => item.Occurrence));
    }

    [Fact]
    public void CancellationAndDuplicateValidationAreExplicit()
    {
        var scheduler = new TimeScheduler();
        Assert.True(scheduler.ScheduleAbsolute(Id("one"), At(1), At(0), "test").IsSuccess);
        Assert.Equal(TimeErrorCodes.DuplicateScheduleId, scheduler.ScheduleAbsolute(Id("one"), At(2), At(0), "test").Error!.Code);
        Assert.True(scheduler.Cancel(Id("one")).IsSuccess);
        Assert.Equal(TimeErrorCodes.ScheduleNotFound, scheduler.Cancel(Id("one")).Error!.Code);
        Assert.Empty(scheduler.DrainDue(At(10), 10).DueTasks);
    }

    [Fact]
    public void RejectsPastZeroRecurrenceAndOverflowingRelativeSchedules()
    {
        var scheduler = new TimeScheduler();

        Assert.Equal(TimeErrorCodes.InvalidSchedule, scheduler.ScheduleAbsolute(Id("past"), At(1), At(2), "test").Error!.Code);
        Assert.Equal(TimeErrorCodes.InvalidSchedule, scheduler.ScheduleAbsolute(Id("repeat"), At(2), At(2), "test", recurrence: new WorldDuration(0)).Error!.Code);
        Assert.Equal(TimeErrorCodes.Overflow, scheduler.ScheduleAfter(Id("overflow"), new WorldDuration(1), At(long.MaxValue), "test").Error!.Code);
    }

    [Fact]
    public void LargeSkipProcessesOnlyDueSchedulesNotElapsedUnits()
    {
        var scheduler = new TimeScheduler();
        scheduler.ScheduleAbsolute(Id("distant"), At(1_000_000_000), At(0), "test");

        var due = scheduler.DrainDue(At(1_000_000_000), 10);

        Assert.Single(due.DueTasks);
        Assert.False(due.LimitReached);
    }

    [Fact]
    public void ScheduleStormIsBoundedAndCanContinueDeterministically()
    {
        var scheduler = new TimeScheduler();
        for (var index = 0; index < 100; index++)
        {
            scheduler.ScheduleAbsolute(Id($"item-{index:D3}"), At(1), At(0), "test");
        }

        var first = scheduler.DrainDue(At(1), 25);
        var second = scheduler.DrainDue(At(1), 100);

        Assert.Equal(25, first.DueTasks.Count);
        Assert.True(first.LimitReached);
        Assert.Equal(75, second.DueTasks.Count);
        Assert.False(second.LimitReached);
        Assert.Equal(Enumerable.Range(0, 100).Select(index => $"item-{index:D3}"), first.DueTasks.Concat(second.DueTasks).Select(item => item.Id.Value));
    }

    [Fact]
    public void RestorePreservesRecurringOccurrenceAndDefensivelyCopiesMetadata()
    {
        var metadata = new Dictionary<string, string> { ["key"] = "value" };
        var scheduler = new TimeScheduler();
        scheduler.ScheduleAbsolute(Id("repeat"), At(1), At(0), "test", metadata, new WorldDuration(2));
        scheduler.DrainDue(At(1), 1);
        metadata["key"] = "changed";
        var snapshot = scheduler.ExportSnapshots();
        var restored = new TimeScheduler();

        Assert.True(restored.Restore(snapshot, At(1)).IsSuccess);
        var due = Assert.Single(restored.DrainDue(At(3), 1).DueTasks);
        Assert.Equal(1, due.Occurrence);
        Assert.Equal("value", due.Metadata["key"]);
    }

    [Fact]
    public void RestoreRejectsConflictingOrderingState()
    {
        var snapshots = new[]
        {
            new ScheduledTaskSnapshot(Id("one"), At(1), null, "test", new Dictionary<string, string>(), 0, 0),
            new ScheduledTaskSnapshot(Id("two"), At(1), null, "test", new Dictionary<string, string>(), 0, 0),
        };

        var result = new TimeScheduler().Restore(snapshots, At(0));

        Assert.False(result.IsSuccess);
        Assert.Equal(TimeErrorCodes.InvalidSnapshot, result.Error!.Code);
    }

    private static ScheduleId Id(string value) => new(value);
    private static WorldTimestamp At(long value) => new(value);
}
