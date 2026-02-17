using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.UrnTests; 
public sealed class UrnFactoryTests {
    [Theory]
    [InlineData("user", "12345", "urn:user:12345")]
    [InlineData("system-core", "service.v1", "urn:system-core:service.v1")]
    [InlineData("A1", "B2", "urn:A1:B2")]
    public void Create_WithValidInputs_ShouldSucceed(string nid, string nss, string expected) {
        Urn urn = Urn.Create(nid, nss);
        Assert.Equal(expected, urn.ToString());
        Assert.Equal(nid, urn.Namespace.ToString());
        Assert.Equal(nss, urn.Identity.ToString());
    }

    [Fact]
    public void Create_WithGuid_ShouldFormatCorrect() {
        Guid guid = Guid.NewGuid();
        Urn urn = Urn.Create("session", guid);

        Assert.Equal($"urn:session:{guid}", urn.ToString());
        Assert.EndsWith(guid.ToString(), urn.Value);
    }

    [Fact]
    public void Create_WithSnowflakeId_ShouldFormatCorrect() {
        SnowflakeId id = new(1234567890);
        Urn urn = Urn.Create("order", id);

        Assert.Equal("urn:order:1234567890", urn.ToString());
    }

    [Theory]
    [InlineData("order", "2024", "10", "urn:order:2024:10")]
    [InlineData("app", "logs", "error", "urn:app:logs:error")]
    public void Create_Hierarchical_ShouldJoinWithColons(string nid, string s1, string s2, string expected) {
        Urn urn = Urn.Create(nid, s1, s2);
        Assert.Equal(expected, urn.ToString());
    }

    [Fact]
    public void Create_Params_ShouldHandleManySegments() {
        Urn urn = Urn.Create("tenant", "id1", "sub", "resource", "action");
        Assert.Equal("urn:tenant:id1:sub:resource:action", urn.ToString());
    }

    [Theory]
    [InlineData("id:withcolon")] // Segment içinde kolon yasak
    [InlineData("")]             // Boş segment yasak
    [InlineData(" ")]            // Whitespace yasak
    public void Create_InvalidSegments_ShouldThrow(string invalidSegment) {
        Assert.ThrowsAny<ArgumentException>(() => Urn.Create("nid", "valid", invalidSegment));
    }

    [Theory]
    [InlineData("nid_invalid")] // NID sadece alfanümerik ve tire olabilir
    [InlineData("nid!")]
    [InlineData("nid.sub")]
    public void Create_InvalidNid_ShouldThrow(string invalidNid) {
        Assert.ThrowsAny<ArgumentException>(() => Urn.Create(invalidNid, "nss"));
    }
}