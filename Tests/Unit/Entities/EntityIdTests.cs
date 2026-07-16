using Mythos.Framework.Entities;

namespace Mythos.Framework.UnitTests.Entities;

public sealed class EntityIdTests
{
    [Fact]
    public void StringRoundTripPreservesIdentity()
    {
        var original = new EntityId(Guid.CreateVersion7());

        var parsed = EntityId.TryParse(original.ToString(), out var restored);

        Assert.True(parsed);
        Assert.Equal(original, restored);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void InvalidIdentityCannotBeParsed(string? value)
    {
        Assert.False(EntityId.TryParse(value, out _));
    }
}
