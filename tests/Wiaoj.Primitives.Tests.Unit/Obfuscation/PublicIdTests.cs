using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Obfuscation;
public sealed class PublicIdTests {
    private const string TestSeed = "Wiaoj-Unit-Test-Secret-2026";

    public PublicIdTests() => PublicId.Configure(TestSeed);

    [Fact]
    public void Empty_Should_Always_Format_As_Literal_Zero() {
        var empty = PublicId.Empty;
        Assert.Equal(0, empty.Value);
        Assert.Equal("0", empty.ToString());
    }

    [Fact]
    public void SnowflakeId_RoundTrip_Integrity_Check() {
        SnowflakeId original = 280025760192794624L;
        PublicId publicId = original;

        string encoded = publicId.ToString();
        PublicId decoded = PublicId.Parse(encoded);

        Assert.NotEqual(original.ToString(), encoded); // Maskelenmiş olmalı
        Assert.Equal(original, (SnowflakeId)decoded);
        Assert.True(decoded.Is64Bit);
    }

    [Fact]
    public void Guid_RoundTrip_Integrity_Check() {
        Guid original = Guid.NewGuid();
        PublicId publicId = original;

        string encoded = publicId.ToString();
        PublicId decoded = PublicId.Parse(encoded);

        Assert.Equal(original, (Guid)decoded);
    }

    [Fact]
    public void Different_Seeds_Should_Produce_Distinct_Outputs() {
        PublicId pid = new(999_888_777L);
        PublicId.Configure("Seed-Alpha");
        string outputA = pid.ToString();

        PublicId.Configure("Seed-Beta");
        string outputB = pid.ToString();

        Assert.NotEqual(outputA, outputB);
    }

    [Fact]
    public void Equality_And_HashCode_Should_Be_Stable() {
        PublicId id1 = new(12345L);
        PublicId id2 = new(12345L);
        PublicId id3 = new(200L);

        Assert.True(id1 == id2);
        Assert.True(id1.Equals(id2));
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
        Assert.False(id1 == id3);
    }
}