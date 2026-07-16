using System.Collections.ObjectModel;

namespace Mythos.Framework.Time;

public readonly record struct ScheduleId
{
    public ScheduleId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record ScheduledTaskSnapshot(
    ScheduleId Id,
    WorldTimestamp DueAt,
    WorldDuration? RecurrenceInterval,
    string Category,
    IReadOnlyDictionary<string, string> Metadata,
    long CreationSequence,
    long NextOccurrence);

public sealed record DueScheduledTask(
    ScheduleId Id,
    WorldTimestamp DueAt,
    string Category,
    IReadOnlyDictionary<string, string> Metadata,
    long Occurrence);

public sealed record SchedulerDrainResult(IReadOnlyList<DueScheduledTask> DueTasks, bool LimitReached);

public sealed class TimeScheduler
{
    private readonly PriorityQueue<ScheduledState, (long DueAt, long Sequence)> queue = new();
    private readonly Dictionary<ScheduleId, ScheduledState> schedules = [];
    private long nextSequence;

    public int Count => schedules.Count;

    public TimeOperationResult ScheduleAbsolute(
        ScheduleId id,
        WorldTimestamp dueAt,
        WorldTimestamp currentTime,
        string category,
        IReadOnlyDictionary<string, string>? metadata = null,
        WorldDuration? recurrence = null)
    {
        if (string.IsNullOrWhiteSpace(id.Value) || schedules.ContainsKey(id))
        {
            return TimeOperationResult.Failure(TimeErrorCodes.DuplicateScheduleId, $"Schedule '{id}' already exists.");
        }

        if (dueAt.Value < currentTime.Value || string.IsNullOrWhiteSpace(category) || recurrence.HasValue && recurrence.Value.Value == 0)
        {
            return TimeOperationResult.Failure(TimeErrorCodes.InvalidSchedule, "Schedule must not be in the past and requires a category and positive recurrence.");
        }

        var copiedMetadata = new ReadOnlyDictionary<string, string>(
            metadata is null ? new Dictionary<string, string>() : new Dictionary<string, string>(metadata, StringComparer.Ordinal));
        var state = new ScheduledState(id, dueAt, recurrence, category.Trim(), copiedMetadata, nextSequence++, 0);
        schedules.Add(id, state);
        queue.Enqueue(state, (dueAt.Value, state.CreationSequence));
        return TimeOperationResult.Success();
    }

    public TimeOperationResult ScheduleAfter(
        ScheduleId id,
        WorldDuration delay,
        WorldTimestamp currentTime,
        string category,
        IReadOnlyDictionary<string, string>? metadata = null,
        WorldDuration? recurrence = null)
    {
        try
        {
            return ScheduleAbsolute(id, new WorldTimestamp(checked(currentTime.Value + delay.Value)), currentTime, category, metadata, recurrence);
        }
        catch (OverflowException)
        {
            return TimeOperationResult.Failure(TimeErrorCodes.Overflow, "Relative schedule exceeds supported world time.");
        }
    }

    public TimeOperationResult Cancel(ScheduleId id) => schedules.Remove(id)
        ? TimeOperationResult.Success()
        : TimeOperationResult.Failure(TimeErrorCodes.ScheduleNotFound, $"Schedule '{id}' was not found.");

    public SchedulerDrainResult DrainDue(WorldTimestamp through, int maximumOccurrences)
    {
        if (maximumOccurrences <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOccurrences));
        }

        var due = new List<DueScheduledTask>(Math.Min(maximumOccurrences, schedules.Count));
        while (due.Count < maximumOccurrences && TryPeekCurrent(out var state) && state.DueAt.Value <= through.Value)
        {
            queue.Dequeue();
            due.Add(new DueScheduledTask(state.Id, state.DueAt, state.Category, state.Metadata, state.Occurrence));
            if (state.RecurrenceInterval is null)
            {
                schedules.Remove(state.Id);
                continue;
            }

            try
            {
                state.DueAt = new WorldTimestamp(checked(state.DueAt.Value + state.RecurrenceInterval.Value.Value));
                state.Occurrence++;
                queue.Enqueue(state, (state.DueAt.Value, state.CreationSequence));
            }
            catch (OverflowException)
            {
                schedules.Remove(state.Id);
            }
        }

        var limited = TryPeekCurrent(out var next) && next.DueAt.Value <= through.Value;
        return new SchedulerDrainResult(due, limited);
    }

    public IReadOnlyList<ScheduledTaskSnapshot> ExportSnapshots() => schedules.Values
        .OrderBy(state => state.DueAt.Value)
        .ThenBy(state => state.CreationSequence)
        .Select(state => new ScheduledTaskSnapshot(state.Id, state.DueAt, state.RecurrenceInterval, state.Category, state.Metadata, state.CreationSequence, state.Occurrence))
        .ToArray();

    public TimeOperationResult Restore(IEnumerable<ScheduledTaskSnapshot> snapshots, WorldTimestamp currentTime)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var restored = snapshots.ToArray();
        if (restored.Any(item => item is null) ||
            restored.Select(item => item.Id).Distinct().Count() != restored.Length ||
            restored.Select(item => item.CreationSequence).Distinct().Count() != restored.Length ||
            restored.Any(item => string.IsNullOrWhiteSpace(item.Id.Value) || item.Metadata is null || item.DueAt.Value < currentTime.Value ||
                item.CreationSequence < 0 || item.NextOccurrence < 0 || string.IsNullOrWhiteSpace(item.Category) ||
                item.RecurrenceInterval.HasValue && item.RecurrenceInterval.Value.Value == 0))
        {
            return TimeOperationResult.Failure(TimeErrorCodes.InvalidSnapshot, "Scheduler snapshot contains invalid or duplicate state.");
        }

        queue.Clear();
        schedules.Clear();
        foreach (var snapshot in restored.OrderBy(item => item.CreationSequence))
        {
            var metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(snapshot.Metadata, StringComparer.Ordinal));
            var state = new ScheduledState(snapshot.Id, snapshot.DueAt, snapshot.RecurrenceInterval, snapshot.Category.Trim(), metadata, snapshot.CreationSequence, snapshot.NextOccurrence);
            schedules.Add(state.Id, state);
            queue.Enqueue(state, (state.DueAt.Value, state.CreationSequence));
        }

        nextSequence = restored.Length == 0 ? 0 : checked(restored.Max(item => item.CreationSequence) + 1);
        return TimeOperationResult.Success();
    }

    private bool TryPeekCurrent(out ScheduledState state)
    {
        while (queue.TryPeek(out var candidate, out _))
        {
            if (schedules.TryGetValue(candidate.Id, out var current) && ReferenceEquals(candidate, current))
            {
                state = candidate;
                return true;
            }

            queue.Dequeue();
        }

        state = null!;
        return false;
    }

    private sealed class ScheduledState(
        ScheduleId id,
        WorldTimestamp dueAt,
        WorldDuration? recurrenceInterval,
        string category,
        IReadOnlyDictionary<string, string> metadata,
        long creationSequence,
        long occurrence)
    {
        public ScheduleId Id { get; } = id;
        public WorldTimestamp DueAt { get; set; } = dueAt;
        public WorldDuration? RecurrenceInterval { get; } = recurrenceInterval;
        public string Category { get; } = category;
        public IReadOnlyDictionary<string, string> Metadata { get; } = metadata;
        public long CreationSequence { get; } = creationSequence;
        public long Occurrence { get; set; } = occurrence;
    }
}
