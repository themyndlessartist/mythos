using Mythos.Framework;
using Mythos.Framework.Time;

if (FrameworkAssembly.Name != "Mythos.Framework")
{
    Console.Error.WriteLine("Framework assembly identity is invalid.");
    return 1;
}

var calendar = CalendarModel.Create(new CalendarDefinition(
    new CalendarId("smoke"),
    1,
    1,
    [new CalendarPeriodDefinition("period", 1)],
    [])).Value!;
var clock = new WorldClock(calendar);
clock.Scheduler.ScheduleAfter(new ScheduleId("smoke-task"), new WorldDuration(1), clock.Timestamp, "smoke");
var advance = clock.Advance(new WorldDuration(1));
if (!advance.IsSuccess || advance.CurrentTimestamp.Value != 1 || advance.DueTasks.Count != 1)
{
    Console.Error.WriteLine("Time Framework smoke validation failed.");
    return 1;
}

Console.WriteLine("Mythos framework smoke test passed.");
return 0;
