using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Time;

public sealed class CalendarModelTests
{
    [Fact]
    public void InterpretsVariablePeriodsAndWrappedSeasons()
    {
        var calendar = CreateCalendar();

        var date = calendar.Interpret(new WorldTimestamp(20));

        Assert.Equal(0, date.Year);
        Assert.Equal("second", date.PeriodId);
        Assert.Equal(2, date.DayOfPeriod);
        Assert.Equal(5, date.DayOfYear);
        Assert.Equal(0, date.UnitOfDay);
        Assert.Equal("late", date.SeasonId);
        Assert.Equal("late", calendar.Interpret(new WorldTimestamp(0)).SeasonId);
    }

    [Theory]
    [MemberData(nameof(InvalidDefinitions))]
    public void RejectsInvalidDefinitions(CalendarDefinition definition)
    {
        var result = CalendarModel.Create(definition);

        Assert.False(result.IsSuccess);
        Assert.Equal(TimeErrorCodes.InvalidCalendar, result.Error!.Code);
    }

    public static TheoryData<CalendarDefinition> InvalidDefinitions => new()
    {
        Definition(version: 0),
        Definition(unitsPerDay: 0),
        Definition(periods: []),
        Definition(periods: [new("same", 1), new("same", 2)]),
        Definition(periods: [new("bad", 0)]),
        Definition(seasons: [new("bad", 8)]),
        Definition(seasons: [new("same", 1), new("same", 2)]),
    };

    internal static CalendarModel CreateCalendar() => CalendarModel.Create(Definition()).Value!;

    private static CalendarDefinition Definition(
        int version = 1,
        long unitsPerDay = 5,
        IReadOnlyList<CalendarPeriodDefinition>? periods = null,
        IReadOnlyList<SeasonDefinition>? seasons = null) => new(
            new CalendarId("test-calendar"),
            version,
            unitsPerDay,
            periods ?? [new("first", 3), new("second", 4)],
            seasons ?? [new("early", 2), new("late", 5)]);
}
