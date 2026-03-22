using System.ComponentModel;
using System.Text.Json;
using Wiaoj.Primitives;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit;
public class NanoIdTests {
    #region Generation Tests

    [Fact]
    public void NewId_WithDefaultParameters_ReturnsCorrectLength() {
        // Act
        var id = NanoId.NewId();

        // Assert
        Assert.Equal(21, id.Value.Length);
        Assert.False(id.IsEmpty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(21)]
    [InlineData(128)]
    public void NewId_WithCustomLength_ReturnsRequestedLength(int length) {
        // Act
        var id = NanoId.NewId(length);

        // Assert
        Assert.Equal(length, id.Value.Length);
    }

    [Fact]
    public void NewId_DefaultAlphabet_ContainsNoVowels() {
        // Act - Çok sayıda ID üretip sesli harf kontrolü yapalım (Profanity-safe check)
        var vowels = new[] { 'a', 'e', 'i', 'o', 'u', 'A', 'E', 'I', 'O', 'U' };

        for(int i = 0; i < 100; i++) {
            var id = NanoId.NewId();
            Assert.DoesNotContain(id.Value, c => vowels.Contains(c));
        }
    }

    [Fact]
    public void NewId_WithCustomAlphabet_UsesOnlyThoseCharacters() {
        // Arrange
        const string alphabet = "0123456789";
        const int length = 15;

        // Act
        var id = NanoId.NewId(alphabet, length);

        // Assert
        Assert.Equal(length, id.Value.Length);
        Assert.All(id.Value, c => Assert.Contains(c, alphabet));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(129)] // MaxAllowedLength + 1
    public void NewId_InvalidLength_ThrowsArgumentOutOfRangeException(int invalidLength) {
        Assert.Throws<ArgumentOutOfRangeException>(() => NanoId.NewId(invalidLength));
    }

    [Fact]
    public void NewId_CustomAlphabet_InvalidChars_ThrowsArgumentException() {
        // 'ç' URL-safe alfabede (ValidChars) yok.
        Assert.Throws<ArgumentException>(() => NanoId.NewId("abcç123", 10));
    }

    #endregion

    #region Parsing Tests

    [Theory]
    [InlineData("0123456789abcdefghij_")]
    [InlineData("A-Z_a-z_0-9")]
    [InlineData("_____________________")]
    public void Parse_ValidString_ReturnsCorrectNanoId(string input) {
        // Act
        var id = NanoId.Parse(input);

        // Assert
        Assert.Equal(input, id.Value);
    }

    [Theory]
    [InlineData("abc@123")] // Geçersiz karakter
    [InlineData("id with space")] // Boşluk yasak
    [InlineData("")] // Boş string
    public void Parse_InvalidString_ThrowsFormatException(string input) {
        Assert.Throws<FormatException>(() => NanoId.Parse(input));
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue() {
        const string input = "valid-nanoid-123456789";
        bool success = NanoId.TryParse(input, out var result);

        Assert.True(success);
        Assert.Equal(input, result.Value);
    }

    [Fact]
    public void TryParse_SpanInput_ReturnsTrue() {
        ReadOnlySpan<char> input = "valid-span-nanoid-123".AsSpan();
        bool success = NanoId.TryParse(input, out var result);

        Assert.True(success);
        Assert.Equal(input.ToString(), result.Value);
    }

    [Fact]
    public void TryParse_NullOrEmpty_ReturnsFalse() {
        Assert.False(NanoId.TryParse(null, out _));
        Assert.False(NanoId.TryParse(string.Empty, out _));
    }

    #endregion

    #region Formatting Tests

    [Fact]
    public void ToString_ReturnsUnderlyingValue() {
        var id = NanoId.NewId();
        Assert.Equal(id.Value, id.ToString());
    }

    [Fact]
    public void TryFormat_BufferTooSmall_ReturnsFalse() {
        var id = NanoId.NewId(21);
        Span<char> buffer = stackalloc char[20]; // 21 lazım

        bool success = id.TryFormat(buffer, out int written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryFormat_ValidBuffer_WritesValue() {
        var id = NanoId.NewId(10);
        Span<char> buffer = stackalloc char[10];

        bool success = id.TryFormat(buffer, out int written);

        Assert.True(success);
        Assert.Equal(10, written);
        Assert.Equal(id.Value, buffer.ToString());
    }

    #endregion

    #region Equality & Comparison Tests

    [Fact]
    public void Equals_SameValue_ReturnsTrue() {
        var id1 = NanoId.Parse("abc-123");
        var id2 = NanoId.Parse("abc-123");

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.True(id1.Equals(id2));
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse() {
        var id1 = NanoId.NewId();
        var id2 = NanoId.NewId();

        Assert.NotEqual(id1, id2);
        Assert.True(id1 != id2);
        Assert.False(id1.Equals(id2));
    }

    [Fact]
    public void CompareTo_WorksCorrectly() {
        var idA = NanoId.Parse("aaaaa");
        var idB = NanoId.Parse("bbbbb");

        Assert.True(idA.CompareTo(idB) < 0);
        Assert.True(idB.CompareTo(idA) > 0);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void JsonSerializer_SerializesAsString() {
        var id = NanoId.NewId();
        var json = JsonSerializer.Serialize(id);

        Assert.Equal($"\"{id.Value}\"", json);
    }

    [Fact]
    public void JsonSerializer_DeserializesCorrectly() {
        const string raw = "custom-id-123";
        string json = $"\"{raw}\"";

        var result = JsonSerializer.Deserialize<NanoId>(json);

        Assert.Equal(raw, result.Value);
    }

    [Fact]
    public void TypeConverter_StringConversion_Works() {
        var converter = TypeDescriptor.GetConverter(typeof(NanoId));
        const string input = "converted-id-456";

        var result = (NanoId)converter.ConvertFrom(input)!;

        Assert.Equal(input, result.Value);
    }

    #endregion

    #region Implicit/Explicit Operator Tests

    [Fact]
    public void ImplicitOperator_ToString_Works() {
        var id = NanoId.Parse("my-id");
        string s = id;

        Assert.Equal("my-id", s);
    }

    [Fact]
    public void ExplicitOperator_FromString_Works() {
        const string s = "explicit-id";
        var id = (NanoId)s;

        Assert.Equal(s, id.Value);
    }

    #endregion
}