using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Time;

public sealed class WorldClockTests
{
    [Fact]
    public void AdvancesAuthoritativeClockWithExactRationalScale()
    {
        var clock = Clock();
        clock.SetScale(new TimeScale(3, 2));

        var first = clock.Advance(new WorldDuration(1));
        var second = clock.Advance(new WorldDuration(1));

        Assert.Equal(1, first.CurrentTimestamp.Value);
        Assert.Equal(3, second.CurrentTimestamp.Value);
    }

    [Fact]
    public void InvalidPrimitiveTimeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldTimestamp(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldDuration(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeScale(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeScale(1, 0));
        Assert.Equal(TimeErrorCodes.InvalidAdvance, Clock().SetScale(default).Error!.Code);
    }

    [Fact]
    public void MultiplePauseReasonsMustAllResumeBeforeAdvancement()
    {
        var clock = Clock();
        var menu = new PauseReason("menu");
        var modal = new PauseReason("modal");
        Assert.True(clock.Pause(menu).IsSuccess);
        Assert.True(clock.Pause(modal).IsSuccess);
        Assert.Equal(TimeErrorCodes.DuplicatePauseReason, clock.Pause(menu).Error!.Code);

        Assert.False(clock.Advance(new WorldDuration(5)).IsSuccess);
        Assert.True(clock.Resume(menu).IsSuccess);
        Assert.False(clock.Advance(new WorldDuration(5)).IsSuccess);
        Assert.True(clock.Resume(modal).IsSuccess);
        Assert.Equal(5, clock.Advance(new WorldDuration(5)).CurrentTimestamp.Value);
        Assert.Equal(TimeErrorCodes.PauseReasonNotFound, clock.Resume(modal).Error!.Code);
    }

    [Fact]
    public void OverflowDoesNotMutateClock()
    {
        var clock = Clock(new WorldTimestamp(long.MaxValue));

        var result = clock.Advance(new WorldDuration(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(TimeErrorCodes.Overflow, result.Error!.Code);
        Assert.Equal(long.MaxValue, clock.Timestamp.Value);
    }

    [Fact]
    public void SnapshotRestorePreservesScaleRemainderPauseAndSchedules()
    {
        var clock = Clock();
        clock.SetScale(new TimeScale(3, 2));
        clock.Advance(new WorldDuration(1));
        clock.Scheduler.ScheduleAfter(new ScheduleId("repeat"), new WorldDuration(4), clock.Timestamp, "test", recurrence: new WorldDuration(2));
        clock.Pause(new PauseReason("menu"));

        var restored = WorldClock.Restore(clock.CreateSnapshot(), CalendarModelTests.CreateCalendar());

        Assert.True(restored.IsSuccess);
        var before = clock.CreateSnapshot();
        var after = restored.Value!.CreateSnapshot();
        Assert.Equal(before.Timestamp, after.Timestamp);
        Assert.Equal(before.Scale, after.Scale);
        Assert.Equal(before.ScaleRemainder, after.ScaleRemainder);
        Assert.Equal(before.PauseReasons, after.PauseReasons);
        var beforeSchedule = Assert.Single(before.Schedules);
        var afterSchedule = Assert.Single(after.Schedules);
        Assert.Equal(beforeSchedule.Id, afterSchedule.Id);
        Assert.Equal(beforeSchedule.DueAt, afterSchedule.DueAt);
        Assert.Equal(beforeSchedule.RecurrenceInterval, afterSchedule.RecurrenceInterval);
        Assert.Equal(beforeSchedule.CreationSequence, afterSchedule.CreationSequence);
        Assert.Equal(beforeSchedule.NextOccurrence, afterSchedule.NextOccurrence);
        Assert.Equal(beforeSchedule.Metadata, afterSchedule.Metadata);
        Assert.True(restored.Value.IsPaused);
        restored.Value.Resume(new PauseReason("menu"));
        Assert.Equal(3, restored.Value.Advance(new WorldDuration(1)).CurrentTimestamp.Value);
    }

    [Fact]
    public void RestoreRejectsCalendarMismatchAndOverdueSchedule()
    {
        var snapshot = Clock().CreateSnapshot() with { CalendarVersion = 99 };

        var result = WorldClock.Restore(snapshot, CalendarModelTests.CreateCalendar());

        Assert.False(result.IsSuccess);
        Assert.Equal(TimeErrorCodes.InvalidSnapshot, result.Error!.Code);
    }

    private static WorldClock Clock(WorldTimestamp timestamp = default) => new(CalendarModelTests.CreateCalendar(), timestamp);
}
