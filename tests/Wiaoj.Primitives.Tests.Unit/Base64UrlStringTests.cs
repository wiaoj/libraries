namespace Wiaoj.Primitives.Tests.Unit;
public sealed class Base64UrlStringTests {
    [Fact]
    public void FromBytes_Should_Not_Contain_Url_Unsafe_Chars() {
        // Bu byte dizisi normalde '+' ve '/' üretir
        byte[] unsafeBytes = { 251, 255, 191, 254 };
        Base64UrlString b64Url = Base64UrlString.FromBytes(unsafeBytes);

        Assert.DoesNotContain("+", b64Url.Value);
        Assert.DoesNotContain("/", b64Url.Value);
        Assert.DoesNotContain("=", b64Url.Value); // Padding olmamalı
        Assert.Contains("-", b64Url.Value);
        Assert.Contains("_", b64Url.Value);
    }

    [Fact]
    public void RoundTrip_Should_Match_Original_Bytes() {
        byte[] original = new byte[100];
        Random.Shared.NextBytes(original);

        Base64UrlString encoded = Base64UrlString.FromBytes(original);
        Span<byte> decoded = stackalloc byte[100];
        encoded.TryDecode(decoded, out int written);

        Assert.Equal(100, written);
        Assert.True(original.AsSpan().SequenceEqual(decoded));
    }

    [Theory]
    [InlineData("SGVsbG8h")] // Geçerli
    [InlineData("abc-def_123")] // Geçerli
    public void TryParse_Should_Accept_Valid_Strings(string input) {
        Assert.True(Base64UrlString.TryParse(input, out _));
    }
}