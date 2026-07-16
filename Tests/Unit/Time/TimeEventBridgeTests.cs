using Mythos.Framework.Events;
using Mythos.Framework.Time;

namespace Mythos.Framework.UnitTests.Time;

public sealed class TimeEventBridgeTests
{
    [Fact]
    public void PublishesCompletedAdvanceAndDueWorkThroughEventContracts()
    {
        var bus = new EventBus();
        var bridge = TimeEventBridge.Create(bus).Value!;
        var seen = new List<string>();
        bus.Subscribe(TimeEventBridge.TimeAdvancedType, new SubscriberId("advance"), _ => seen.Add("advance"));
        bus.Subscribe(TimeEventBridge.ScheduledTaskDueType, new SubscriberId("due"), _ => seen.Add("due"));
        var clock = new WorldClock(CalendarModelTests.CreateCalendar());
        clock.Scheduler.ScheduleAbsolute(new ScheduleId("one"), new WorldTimestamp(1), clock.Timestamp, "test");

        var dispatch = bridge.Publish(clock.Advance(new WorldDuration(1)));

        Assert.Equal(["advance", "due"], seen);
        Assert.All(dispatch, result => Assert.True(result.IsSuccessful));
    }
}
