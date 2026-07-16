namespace Mythos.Framework.Characters;

public readonly record struct CharacterIdentity
{
    public CharacterIdentity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CharacterStatusId
{
    public CharacterStatusId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct LifeStageId
{
    public LifeStageId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>
/// Validates data-defined Character references without making the framework own their definitions.
/// </summary>
public interface ICharacterReferenceValidator
{
    bool IsKnownStatus(CharacterStatusId statusId);

    bool IsKnownLifeStage(LifeStageId lifeStageId);
}
