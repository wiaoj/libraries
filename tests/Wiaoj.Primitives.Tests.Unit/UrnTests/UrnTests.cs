using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.UrnTests;
public class UrnTests {
    [Fact]
    public void Should_Create_Valid_Urn() {
        Urn urn = Urn.Create("user", "12345");
        Assert.Equal("urn:user:12345", urn.ToString());
        Assert.Equal("user", urn.Namespace.ToString());
        Assert.Equal("12345", urn.Identity.ToString());
    }

    [Fact]
    public void Should_Create_From_SnowflakeId() {
        SnowflakeId id = new(999);
        Urn urn = Urn.Create("order", id);

        Assert.Equal("urn:order:999", urn.ToString());
    }

    [Fact]
    public void Parse_Should_Validate_Format() {
        Assert.Throws<FormatException>(() => Urn.Parse("invalid"));
        Assert.Throws<FormatException>(() => Urn.Parse("urn:"));
        Assert.Throws<FormatException>(() => Urn.Parse("urn:empty:"));
        Assert.Throws<FormatException>(() => Urn.Parse("http://google.com"));
    }

    [Fact]
    public void TryParse_Should_Handle_Invalid_Inputs() {
        Assert.False(Urn.TryParse("just-text", null, out _));
        Assert.False(Urn.TryParse("urn:invalid-nid!:123", null, out _)); // Special char in NID

        Assert.True(Urn.TryParse("urn:isbn:978-0-123", null, out var result));
        Assert.Equal("isbn", result.Namespace.ToString());
    }

    [Fact]
    public void Equality_Check() {
        Urn u1 = Urn.Create("a", "1");
        Urn u2 = Urn.Parse("urn:a:1");

        Assert.Equal(u1, u2);
    }
}