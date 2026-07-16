namespace Mythos.Framework.Time;

public readonly record struct CalendarId
{
    public CalendarId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record CalendarPeriodDefinition(string Id, int LengthInDays);
public sealed record SeasonDefinition(string Id, int StartDayOfYear);

public sealed record CalendarDefinition(
    CalendarId Id,
    int Version,
    long UnitsPerDay,
    IReadOnlyList<CalendarPeriodDefinition> Periods,
    IReadOnlyList<SeasonDefinition> Seasons);

public sealed record CalendarDate(
    long Year,
    string PeriodId,
    int PeriodIndex,
    int DayOfPeriod,
    int DayOfYear,
    long UnitOfDay,
    string? SeasonId);

public sealed class CalendarModel
{
    private readonly CalendarDefinition definition;
    private readonly long daysPerYear;

    private CalendarModel(CalendarDefinition definition, long daysPerYear)
    {
        this.definition = definition;
        this.daysPerYear = daysPerYear;
    }

    public CalendarDefinition Definition => definition;

    public static CalendarModelResult Create(CalendarDefinition? definition)
    {
        if (definition is null || string.IsNullOrWhiteSpace(definition.Id.Value) || definition.Version <= 0 ||
            definition.UnitsPerDay <= 0 || definition.Periods is null || definition.Seasons is null || definition.Periods.Count == 0)
        {
            return CalendarModelResult.Failure("Calendar requires a positive version, positive units per day, and at least one period.");
        }

        if (definition.Periods.Any(period => string.IsNullOrWhiteSpace(period.Id) || period.LengthInDays <= 0) ||
            definition.Periods.Select(period => period.Id).Distinct(StringComparer.Ordinal).Count() != definition.Periods.Count)
        {
            return CalendarModelResult.Failure("Calendar periods require unique non-empty IDs and positive lengths.");
        }

        long daysPerYear;
        try
        {
            daysPerYear = definition.Periods.Aggregate(0L, (total, period) => checked(total + period.LengthInDays));
            _ = checked(daysPerYear * definition.UnitsPerDay);
        }
        catch (OverflowException)
        {
            return CalendarModelResult.Failure("Calendar year length exceeds supported world time.");
        }

        if (definition.Seasons.Any(season => string.IsNullOrWhiteSpace(season.Id) || season.StartDayOfYear < 1 || season.StartDayOfYear > daysPerYear) ||
            definition.Seasons.Select(season => season.Id).Distinct(StringComparer.Ordinal).Count() != definition.Seasons.Count ||
            definition.Seasons.Select(season => season.StartDayOfYear).Distinct().Count() != definition.Seasons.Count)
        {
            return CalendarModelResult.Failure("Seasons require unique IDs and unique start days within the calendar year.");
        }

        var copy = definition with
        {
            Periods = Array.AsReadOnly(definition.Periods.ToArray()),
            Seasons = Array.AsReadOnly(definition.Seasons.OrderBy(season => season.StartDayOfYear).ToArray()),
        };
        return CalendarModelResult.Success(new CalendarModel(copy, daysPerYear));
    }

    public CalendarDate Interpret(WorldTimestamp timestamp)
    {
        var unitsPerYear = checked(daysPerYear * definition.UnitsPerDay);
        var year = timestamp.Value / unitsPerYear;
        var unitOfYear = timestamp.Value % unitsPerYear;
        var zeroBasedDay = unitOfYear / definition.UnitsPerDay;
        var dayOfYear = checked((int)zeroBasedDay + 1);
        var remainingDay = zeroBasedDay;
        var periodIndex = 0;
        while (remainingDay >= definition.Periods[periodIndex].LengthInDays)
        {
            remainingDay -= definition.Periods[periodIndex++].LengthInDays;
        }

        var season = definition.Seasons.LastOrDefault(item => item.StartDayOfYear <= dayOfYear)
            ?? definition.Seasons.LastOrDefault();
        return new CalendarDate(
            year,
            definition.Periods[periodIndex].Id,
            periodIndex,
            checked((int)remainingDay + 1),
            dayOfYear,
            unitOfYear % definition.UnitsPerDay,
            season?.Id);
    }
}

public sealed record CalendarModelResult(CalendarModel? Value, TimeError? Error)
{
    public bool IsSuccess => Error is null;
    public static CalendarModelResult Success(CalendarModel value) => new(value, null);
    public static CalendarModelResult Failure(string message) => new(null, new TimeError(TimeErrorCodes.InvalidCalendar, message));
}
