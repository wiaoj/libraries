using Wiaoj.Primitives.Cryptography.Hashing;
using Wiaoj.Primitives.Obfuscation;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Obfuscation;
public sealed class OpaqueIdTests { 
    public OpaqueIdTests() {
        OpaqueId.Configure(new FeistelBase62Obfuscator(new() { Seed = "1234567890123456"u8.ToArray() }));
    }
     
    [Fact]
    public void Empty_Should_Always_Format_As_Literal_Zero() {
        var empty = OpaqueId.Empty;
        Assert.Equal(0, empty.Value);
        Assert.Equal("0", empty.ToString());
    }

    [Fact]
    public void SnowflakeId_RoundTrip_Integrity_Check() {
        SnowflakeId original = 280025760192794624L;
        OpaqueId publicId = original;

        string encoded = publicId.ToString();
        OpaqueId decoded = OpaqueId.Parse(encoded);

        Assert.NotEqual(original.ToString(), encoded); // Maskelenmiş olmalı
        Assert.Equal(original, (SnowflakeId)decoded);
        Assert.True(decoded.Is64Bit);
    }

    [Fact]
    public void Guid_RoundTrip_Integrity_Check() {
        Guid original = Guid.NewGuid();
        OpaqueId publicId = original;

        string encoded = publicId.ToString();
        OpaqueId decoded = OpaqueId.Parse(encoded);

        Assert.Equal(original, (Guid)decoded);
    }

    [Fact]
    public void Different_Seeds_Should_Produce_Distinct_Outputs() {
        OpaqueId pid = new(999_888_777L);
        OpaqueId.Configure(new FeistelBase62Obfuscator(new() { Seed = "Seed-Alpha-0000-0000-0000-0000"u8.ToArray() }));
        string outputA = pid.ToString();
          
        OpaqueId.Configure(new FeistelBase62Obfuscator(new() { Seed = "Seed-Beta-0000-0000-0000-0000"u8.ToArray() }));
        string outputB = pid.ToString();

        Assert.NotEqual(outputA, outputB);
    }

    [Fact]
    public void Equality_And_HashCode_Should_Be_Stable() {
        OpaqueId id1 = new(12345L);
        OpaqueId id2 = new(12345L);
        OpaqueId id3 = new(200L);

        Assert.True(id1 == id2);
        Assert.True(id1.Equals(id2));
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
        Assert.False(id1 == id3);
    }
}