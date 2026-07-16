namespace Mythos.Framework.Time;

public readonly record struct WorldTimestamp
{
    public WorldTimestamp(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "World timestamp cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WorldDuration
{
    public WorldDuration(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "World duration cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }
}

public readonly record struct TimeScale
{
    public TimeScale(long numerator, long denominator = 1)
    {
        if (numerator < 0 || denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), "Time scale requires a non-negative numerator and positive denominator.");
        }

        var divisor = GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public long Numerator { get; }
    public long Denominator { get; }
    public static TimeScale Normal => new(1);

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left == 0 ? 1 : left;
    }
}

public readonly record struct PauseReason
{
    public PauseReason(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public static class TimeErrorCodes
{
    public const string InvalidAdvance = "time.invalid_advance";
    public const string Overflow = "time.overflow";
    public const string Paused = "time.paused";
    public const string DuplicatePauseReason = "time.duplicate_pause_reason";
    public const string PauseReasonNotFound = "time.pause_reason_not_found";
    public const string InvalidCalendar = "time.invalid_calendar";
    public const string DuplicateScheduleId = "time.duplicate_schedule_id";
    public const string ScheduleNotFound = "time.schedule_not_found";
    public const string InvalidSchedule = "time.invalid_schedule";
    public const string CatchUpLimitReached = "time.catch_up_limit_reached";
    public const string InvalidSnapshot = "time.invalid_snapshot";
}

public sealed record TimeError(string Code, string Message);

public readonly record struct TimeOperationResult(TimeError? Error)
{
    public bool IsSuccess => Error is null;
    public static TimeOperationResult Success() => new(null);
    public static TimeOperationResult Failure(string code, string message) => new(new TimeError(code, message));
}
