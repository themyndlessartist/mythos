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
        Assert.NotNull(before.Schedules);
        Assert.NotNull(after.Schedules);
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
        var source = Clock().CreateSnapshot();
        var snapshot = Copy(source, calendarVersion: 99);

        var result = WorldClock.Restore(snapshot, CalendarModelTests.CreateCalendar());

        Assert.False(result.IsSuccess);
        Assert.Equal(TimeErrorCodes.InvalidSnapshot, result.Error!.Code);
    }

    [Fact]
    public void RestoreRejectsInvalidPauseIdentifiers()
    {
        var source = Clock().CreateSnapshot();

        var defaultReason = WorldClock.Restore(Copy(source, pauseReasons: [default]), CalendarModelTests.CreateCalendar());

        Assert.False(defaultReason.IsSuccess);
        Assert.Equal(TimeErrorCodes.InvalidSnapshot, defaultReason.Error!.Code);
    }

    [Fact]
    public void RestoreRejectsMalformedNestedStateWithoutThrowing()
    {
        var source = Clock().CreateSnapshot();
        var invalidSchedule = new ScheduledTaskSnapshot(
            new ScheduleId("invalid"),
            new WorldTimestamp(1),
            null,
            "test",
            new Dictionary<string, string>(),
            long.MaxValue,
            0);
        var invalidLayer = new SimulationLayerSnapshot(
            new SimulationLayerId("invalid"),
            new WorldDuration(1),
            new WorldTimestamp(0),
            long.MaxValue,
            1);

        var scheduleResult = WorldClock.Restore(Copy(source, schedules: [invalidSchedule]), CalendarModelTests.CreateCalendar());
        var layerResult = WorldClock.Restore(Copy(source, simulationLayers: [invalidLayer]), CalendarModelTests.CreateCalendar());

        Assert.False(scheduleResult.IsSuccess);
        Assert.False(layerResult.IsSuccess);
        Assert.Equal(TimeErrorCodes.InvalidSnapshot, scheduleResult.Error!.Code);
        Assert.Equal(TimeErrorCodes.InvalidSnapshot, layerResult.Error!.Code);
    }

    [Fact]
    public void SnapshotCollectionsAreDefensiveAndReadOnly()
    {
        var pauses = new[] { new PauseReason("menu") };
        var schedules = Array.Empty<ScheduledTaskSnapshot>();
        var layers = Array.Empty<SimulationLayerSnapshot>();
        var source = Clock().CreateSnapshot();
        var snapshot = new WorldClockSnapshot(
            source.Version,
            source.Timestamp,
            source.Scale,
            source.ScaleRemainder,
            pauses,
            source.CalendarId,
            source.CalendarVersion,
            schedules,
            layers);
        pauses[0] = new PauseReason("changed");

        Assert.Equal("menu", Assert.Single(snapshot.PauseReasons!).Value);
        Assert.Throws<NotSupportedException>(() => ((IList<PauseReason>)snapshot.PauseReasons!)[0] = new PauseReason("blocked"));
        Assert.Throws<NotSupportedException>(() => ((IList<ScheduledTaskSnapshot>)snapshot.Schedules!).Add(null!));
        Assert.Throws<NotSupportedException>(() => ((IList<SimulationLayerSnapshot>)snapshot.SimulationLayers!).Add(null!));
    }

    private static WorldClockSnapshot Copy(
        WorldClockSnapshot source,
        IReadOnlyList<PauseReason>? pauseReasons = null,
        int? calendarVersion = null,
        IReadOnlyList<ScheduledTaskSnapshot>? schedules = null,
        IReadOnlyList<SimulationLayerSnapshot>? simulationLayers = null) => new(
        source.Version,
        source.Timestamp,
        source.Scale,
        source.ScaleRemainder,
        pauseReasons ?? source.PauseReasons,
        source.CalendarId,
        calendarVersion ?? source.CalendarVersion,
        schedules ?? source.Schedules,
        simulationLayers ?? source.SimulationLayers);

    private static WorldClock Clock(WorldTimestamp timestamp = default) => new(CalendarModelTests.CreateCalendar(), timestamp);
}
