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
    [InlineData("abc-def_1234")] // Geçerli
    public void TryParse_Should_Accept_Valid_Strings(string input) {
        Assert.True(Base64UrlString.TryParse(input, out _));
    }

    [Theory]
    [InlineData("")]             // Boş (Empty)
    [InlineData("YQ")]           // "a" karakterinin encode hali (2 char)
    [InlineData("YWI")]          // "ab" karakterlerinin encode hali (3 char)
    [InlineData("YWJj")]         // "abc" karakterlerinin encode hali (4 char)
    [InlineData("YWJjZA")]       // "abcd" karakterlerinin encode hali (6 char)
    public void TryParse_Should_Accept_Mathematically_Correct_Strings(string input) {
        // Act
        bool result = Base64UrlString.TryParse(input, out var base64Url);

        // Assert
        Assert.True(result);
        if(!string.IsNullOrEmpty(input)) {
            Assert.Equal(input, base64Url.ToString());
        }
    }

    [Theory]
    [InlineData("a")]          // 1 karakter (İmkansız: 6 bit, 1 byte bile etmiyor)
    [InlineData("abcde")]      // 5 karakter (5 * 6 = 30 bit. 8'e tam bölünmez, artık bit kalır)
    [InlineData("abc-def_123")] // 11 karakter (Senin hata aldığın durum)
    public void TryParse_Should_Return_False_For_Structurally_Invalid_Strings(string input) {
        // Act
        bool result = Base64UrlString.TryParse(input, out _);

        // Assert
        Assert.False(result, $"Input '{input}' is structurally invalid and should not pass.");
    }
}