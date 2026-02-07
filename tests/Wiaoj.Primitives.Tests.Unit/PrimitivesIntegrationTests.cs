using System.Text;
using Wiaoj.Primitives;

namespace Wiaoj.Primitives.Tests.Unit;
public class PrimitivesIntegrationTests {
    [Fact]
    public void EndToEnd_Secret_Flow_With_Different_Encodings() {
        // 1. Arrange
        string rawKey = "SuperSecretKey123!";
        var base64Key = Base64String.FromBytes(Encoding.UTF8.GetBytes(rawKey));

        // 2. Act
        // Secret.FromMilliseconds(Base64) bize Secret<byte> döner.
        using var secret = Secret.From(base64Key);

        // DÜZELTME: Encoding parametresi kaldırıldı.
        var hash = Sha256Hash.Compute(secret);
        var hex = hash.ToHexString();

        // 3. Assert
        var expectedHash = Sha256Hash.Compute(rawKey);
        Assert.Equal(expectedHash.ToHexString(), hex);
    }

    [Fact]
    public void Base32_To_Secret_To_Hash() {
        var rawData = Encoding.ASCII.GetBytes("Hello");
        var base32 = Base32String.FromBytes(rawData);

        // Secret.FromMilliseconds(Base32) bize Secret<byte> döner.
        using var secret = Secret.From(base32);

        // DÜZELTME: Encoding parametresi kaldırıldı.
        var hash = Sha256Hash.Compute(secret);
        var expectedHash = Sha256Hash.Compute(rawData);

        Assert.Equal(expectedHash, hash);
    }

    [Fact]
    public void Hex_To_Secret_Conversion() {
        var rawBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        HexString hex = HexString.FromBytes(rawBytes);

        using Secret<byte> secret = Secret.From(hex);

        secret.Expose(span => {
            Assert.True(rawBytes.AsSpan().SequenceEqual(span));
        });
    }

    [Fact]
    public void HexString_FromText_Should_Work() {
        // "hello" -> Hex: "68656C6C6F"
        var hex = HexString.FromUtf8("hello");
        Assert.Equal("68656C6C6F", hex.Value);
    }

    [Fact]
    public void Base32String_FromText_Should_Work() {
        // "hello" -> Base32: "NBSWY3DP" (RFC 4648)
        var b32 = Base32String.FromUtf8("hello");
        Assert.Equal("NBSWY3DP", b32.Value);
    }
}