namespace Mythos.Framework.Events;

public readonly record struct EventId(Guid Value)
{
    public override string ToString() => Value.ToString("D");
}

public interface IEventIdGenerator
{
    EventId Create();
}

public sealed class Version7EventIdGenerator : IEventIdGenerator
{
    public EventId Create() => new(Guid.CreateVersion7());
}

public readonly record struct EventType
{
    public EventType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct SubscriberId
{
    public SubscriberId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
