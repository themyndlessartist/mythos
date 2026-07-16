namespace Mythos.Framework.Entities;

public readonly record struct EntityCategory
{
    public EntityCategory(string value)
    {
        Value = RequireValue(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}

public readonly record struct EntityTag
{
    public EntityTag(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ComponentTypeId
{
    public ComponentTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
