namespace Mythos.Framework.Time;

public readonly record struct SimulationLayerId
{
    public SimulationLayerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record SimulationLayerSnapshot(
    SimulationLayerId Id,
    WorldDuration Interval,
    WorldTimestamp LastProcessedAt,
    long RegistrationSequence,
    long NextTick);

public sealed record SimulationLayerDue(
    SimulationLayerId Id,
    WorldTimestamp DueAt,
    long Tick);

/// <summary>
/// Tracks deterministic progress markers for systems that share the world timeline but update at different intervals.
/// It reports due markers and never executes domain behavior.
/// </summary>
public sealed class SimulationLayerCoordinator
{
    private readonly Dictionary<SimulationLayerId, LayerState> layers = [];
    private long nextSequence;

    public TimeOperationResult Register(SimulationLayerId id, WorldDuration interval, WorldTimestamp currentTime)
    {
        if (string.IsNullOrWhiteSpace(id.Value) || interval.Value == 0)
        {
            return TimeOperationResult.Failure(TimeErrorCodes.InvalidSchedule, "Simulation layer interval must be positive.");
        }

        if (layers.ContainsKey(id))
        {
            return TimeOperationResult.Failure(TimeErrorCodes.DuplicateScheduleId, $"Simulation layer '{id}' already exists.");
        }

        layers.Add(id, new LayerState(id, interval, currentTime, nextSequence++));
        return TimeOperationResult.Success();
    }

    public IReadOnlyList<SimulationLayerDue> QueryDue(WorldTimestamp through, int maximumMarkers, out bool limitReached)
    {
        if (maximumMarkers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMarkers));
        }

        var result = new List<SimulationLayerDue>();
        while (result.Count < maximumMarkers)
        {
            var next = layers.Values
                .Where(state => through.Value >= state.Interval.Value && state.LastProcessedAt.Value <= through.Value - state.Interval.Value)
                .Select(state => (State: state, DueAt: state.LastProcessedAt.Value + state.Interval.Value))
                .OrderBy(item => item.DueAt)
                .ThenBy(item => item.State.RegistrationSequence)
                .FirstOrDefault();
            if (next.State is null)
            {
                break;
            }

            next.State.LastProcessedAt = new WorldTimestamp(next.DueAt);
            next.State.Tick++;
            result.Add(new SimulationLayerDue(next.State.Id, next.State.LastProcessedAt, next.State.Tick));
        }

        limitReached = layers.Values.Any(state => through.Value >= state.Interval.Value && state.LastProcessedAt.Value <= through.Value - state.Interval.Value);
        return result;
    }

    public IReadOnlyList<SimulationLayerSnapshot> ExportSnapshots() => layers.Values
        .OrderBy(state => state.RegistrationSequence)
        .Select(state => new SimulationLayerSnapshot(state.Id, state.Interval, state.LastProcessedAt, state.RegistrationSequence, state.Tick + 1))
        .ToArray();

    public TimeOperationResult Restore(IEnumerable<SimulationLayerSnapshot> snapshots, WorldTimestamp currentTime)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var restored = snapshots.ToArray();
        if (restored.Any(item => item is null) ||
            restored.Select(item => item.Id).Distinct().Count() != restored.Length ||
            restored.Select(item => item.RegistrationSequence).Distinct().Count() != restored.Length ||
            restored.Any(item => string.IsNullOrWhiteSpace(item.Id.Value) || item.Interval.Value == 0 || item.LastProcessedAt.Value > currentTime.Value ||
                item.RegistrationSequence < 0 || item.NextTick <= 0))
        {
            return TimeOperationResult.Failure(TimeErrorCodes.InvalidSnapshot, "Simulation layer snapshot contains invalid or duplicate state.");
        }

        layers.Clear();
        foreach (var snapshot in restored)
        {
            var state = new LayerState(snapshot.Id, snapshot.Interval, snapshot.LastProcessedAt, snapshot.RegistrationSequence)
            {
                Tick = snapshot.NextTick - 1,
            };
            layers.Add(state.Id, state);
        }

        nextSequence = restored.Length == 0 ? 0 : checked(restored.Max(item => item.RegistrationSequence) + 1);
        return TimeOperationResult.Success();
    }

    private sealed class LayerState(SimulationLayerId id, WorldDuration interval, WorldTimestamp lastProcessedAt, long registrationSequence)
    {
        public SimulationLayerId Id { get; } = id;
        public WorldDuration Interval { get; } = interval;
        public WorldTimestamp LastProcessedAt { get; set; } = lastProcessedAt;
        public long RegistrationSequence { get; } = registrationSequence;
        public long Tick { get; set; }
    }
}
