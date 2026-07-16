namespace Mythos.Framework.Time;

public sealed record WorldClockSnapshot
{
    public WorldClockSnapshot(
        int version,
        WorldTimestamp timestamp,
        TimeScale scale,
        long scaleRemainder,
        IReadOnlyList<PauseReason>? pauseReasons,
        CalendarId calendarId,
        int calendarVersion,
        IReadOnlyList<ScheduledTaskSnapshot>? schedules,
        IReadOnlyList<SimulationLayerSnapshot>? simulationLayers)
    {
        Version = version;
        Timestamp = timestamp;
        Scale = scale;
        ScaleRemainder = scaleRemainder;
        PauseReasons = pauseReasons is null ? null : Array.AsReadOnly(pauseReasons.ToArray());
        CalendarId = calendarId;
        CalendarVersion = calendarVersion;
        Schedules = schedules is null ? null : Array.AsReadOnly(schedules.ToArray());
        SimulationLayers = simulationLayers is null ? null : Array.AsReadOnly(simulationLayers.ToArray());
    }

    public int Version { get; }
    public WorldTimestamp Timestamp { get; }
    public TimeScale Scale { get; }
    public long ScaleRemainder { get; }
    public IReadOnlyList<PauseReason>? PauseReasons { get; }
    public CalendarId CalendarId { get; }
    public int CalendarVersion { get; }
    public IReadOnlyList<ScheduledTaskSnapshot>? Schedules { get; }
    public IReadOnlyList<SimulationLayerSnapshot>? SimulationLayers { get; }
}

public sealed record TimeAdvanceResult(
    bool IsSuccess,
    WorldTimestamp PreviousTimestamp,
    WorldTimestamp CurrentTimestamp,
    IReadOnlyList<DueScheduledTask> DueTasks,
    IReadOnlyList<SimulationLayerDue> DueSimulationLayers,
    bool CatchUpLimitReached,
    TimeError? Error);

public sealed class WorldClock
{
    public const int SnapshotVersion = 1;
    public const int DefaultMaximumDueOccurrences = 10_000;

    private readonly HashSet<PauseReason> pauseReasons = [];
    private long scaleRemainder;

    public WorldClock(
        CalendarModel calendar,
        WorldTimestamp initialTimestamp = default,
        TimeScheduler? scheduler = null,
        SimulationLayerCoordinator? simulationLayers = null)
    {
        Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        Timestamp = initialTimestamp;
        Scheduler = scheduler ?? new TimeScheduler();
        SimulationLayers = simulationLayers ?? new SimulationLayerCoordinator();
        Scale = TimeScale.Normal;
    }

    public WorldTimestamp Timestamp { get; private set; }
    public TimeScale Scale { get; private set; }
    public CalendarModel Calendar { get; }
    public TimeScheduler Scheduler { get; }
    public SimulationLayerCoordinator SimulationLayers { get; }
    public bool IsPaused => pauseReasons.Count > 0;
    public IReadOnlyList<PauseReason> PauseReasons => Array.AsReadOnly(pauseReasons.OrderBy(reason => reason.Value, StringComparer.Ordinal).ToArray());

    public TimeOperationResult SetScale(TimeScale scale)
    {
        if (scale.Numerator < 0 || scale.Denominator <= 0)
        {
            return TimeOperationResult.Failure(TimeErrorCodes.InvalidAdvance, "Time scale is invalid.");
        }

        Scale = scale;
        scaleRemainder = 0;
        return TimeOperationResult.Success();
    }

    public TimeOperationResult Pause(PauseReason reason) => string.IsNullOrWhiteSpace(reason.Value)
        ? TimeOperationResult.Failure(TimeErrorCodes.DuplicatePauseReason, "Pause reason is invalid.")
        : pauseReasons.Add(reason)
        ? TimeOperationResult.Success()
        : TimeOperationResult.Failure(TimeErrorCodes.DuplicatePauseReason, $"Pause reason '{reason}' is already active.");

    public TimeOperationResult Resume(PauseReason reason) => pauseReasons.Remove(reason)
        ? TimeOperationResult.Success()
        : TimeOperationResult.Failure(TimeErrorCodes.PauseReasonNotFound, $"Pause reason '{reason}' is not active.");

    public TimeAdvanceResult Advance(WorldDuration requestedDuration, int maximumDueOccurrences = DefaultMaximumDueOccurrences)
    {
        var previous = Timestamp;
        if (maximumDueOccurrences <= 0)
        {
            return Failure(previous, TimeErrorCodes.InvalidAdvance, "Catch-up bound must be positive.");
        }

        if (IsPaused)
        {
            return Failure(previous, TimeErrorCodes.Paused, "World time cannot advance while pause reasons are active.");
        }

        try
        {
            var scaledNumerator = checked(requestedDuration.Value * Scale.Numerator + scaleRemainder);
            var elapsed = scaledNumerator / Scale.Denominator;
            var nextTimestamp = new WorldTimestamp(checked(Timestamp.Value + elapsed));
            scaleRemainder = scaledNumerator % Scale.Denominator;
            Timestamp = nextTimestamp;
        }
        catch (OverflowException)
        {
            return Failure(previous, TimeErrorCodes.Overflow, "Time advancement exceeds supported world time.");
        }

        var drained = Scheduler.DrainDue(Timestamp, maximumDueOccurrences);
        var layers = SimulationLayers.QueryDue(Timestamp, maximumDueOccurrences, out var layerLimitReached);
        return new TimeAdvanceResult(true, previous, Timestamp, drained.DueTasks, layers, drained.LimitReached || layerLimitReached, null);
    }

    public CalendarDate GetCalendarDate() => Calendar.Interpret(Timestamp);

    public WorldClockSnapshot CreateSnapshot() => new(
        SnapshotVersion,
        Timestamp,
        Scale,
        scaleRemainder,
        PauseReasons,
        Calendar.Definition.Id,
        Calendar.Definition.Version,
        Scheduler.ExportSnapshots(),
        SimulationLayers.ExportSnapshots());

    public static WorldClockRestoreResult Restore(WorldClockSnapshot? snapshot, CalendarModel? calendar)
    {
        if (snapshot is null || calendar is null || snapshot.PauseReasons is null || snapshot.Schedules is null || snapshot.SimulationLayers is null ||
            snapshot.Version != SnapshotVersion || snapshot.CalendarId != calendar.Definition.Id ||
            snapshot.CalendarVersion != calendar.Definition.Version || snapshot.Scale.Denominator <= 0 || snapshot.Scale.Numerator < 0 ||
            snapshot.ScaleRemainder < 0 || snapshot.ScaleRemainder >= snapshot.Scale.Denominator ||
            snapshot.PauseReasons.Any(reason => string.IsNullOrWhiteSpace(reason.Value)) ||
            snapshot.PauseReasons.Distinct().Count() != snapshot.PauseReasons.Count)
        {
            return WorldClockRestoreResult.Failure("Clock snapshot is invalid or incompatible with the supplied calendar.");
        }

        var scheduler = new TimeScheduler();
        var schedulerResult = scheduler.Restore(snapshot.Schedules, snapshot.Timestamp);
        if (!schedulerResult.IsSuccess)
        {
            return WorldClockRestoreResult.Failure(schedulerResult.Error!.Message);
        }

        var layers = new SimulationLayerCoordinator();
        var layerResult = layers.Restore(snapshot.SimulationLayers, snapshot.Timestamp);
        if (!layerResult.IsSuccess)
        {
            return WorldClockRestoreResult.Failure(layerResult.Error!.Message);
        }

        var clock = new WorldClock(calendar, snapshot.Timestamp, scheduler, layers)
        {
            Scale = snapshot.Scale,
            scaleRemainder = snapshot.ScaleRemainder,
        };
        foreach (var reason in snapshot.PauseReasons)
        {
            clock.pauseReasons.Add(reason);
        }

        return WorldClockRestoreResult.Success(clock);
    }

    private static TimeAdvanceResult Failure(WorldTimestamp timestamp, string code, string message) =>
        new(false, timestamp, timestamp, [], [], false, new TimeError(code, message));
}

public sealed record WorldClockRestoreResult(WorldClock? Value, TimeError? Error)
{
    public bool IsSuccess => Error is null;
    public static WorldClockRestoreResult Success(WorldClock value) => new(value, null);
    public static WorldClockRestoreResult Failure(string message) => new(null, new TimeError(TimeErrorCodes.InvalidSnapshot, message));
}
