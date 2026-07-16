namespace Mythos.Framework.Entities;

/// <summary>
/// Stable identity for a persistent world entity.
/// </summary>
public readonly record struct EntityId(Guid Value)
{
    public static bool TryParse(string? value, out EntityId entityId)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            entityId = new EntityId(parsed);
            return true;
        }

        entityId = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public interface IEntityIdGenerator
{
    EntityId Create();
}

public sealed class Version7EntityIdGenerator : IEntityIdGenerator
{
    public EntityId Create() => new(Guid.CreateVersion7());
}
