using System.ComponentModel;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;

public class NanoIdTests {
    #region Generation Tests

    [Fact]
    public void NewId_WithDefaultParameters_ReturnsCorrectLength() {
        // Act
        NanoId id = NanoId.NewId();

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
        NanoId id = NanoId.NewId(length);

        // Assert
        Assert.Equal(length, id.Value.Length);
    }

    [Fact]
    public void NewId_DefaultAlphabet_ContainsOnlyUrlSafeCharacters() {
        // Arrange
        const int sampleCount = 100;
        const int expectedLength = 21;
        string allowedChars = NanoId.Alphabets.UrlSafe;

        // Act
        NanoId[] generatedIds = new NanoId[sampleCount];
        for(int i = 0; i < sampleCount; i++) {
            generatedIds[i] = NanoId.NewId();
        }

        // Assert
        Assert.All(generatedIds, id => {
            Assert.Equal(expectedLength, id.Value.Length);
            Assert.All(id.Value, c => Assert.Contains(c, allowedChars));
        });
    }

    [Fact]
    public void NewId_WithCustomAlphabet_UsesOnlyThoseCharacters() {
        // Arrange
        const string alphabet = "0123456789";
        const int length = 15;

        // Act
        NanoId id = NanoId.NewId(alphabet, length);

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
        NanoId id = NanoId.Parse(input);

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
        Assert.False(NanoId.TryParse((string?)null, out _));
        Assert.False(NanoId.TryParse(string.Empty, out _));
    }

    #endregion

    #region Formatting Tests

    [Fact]
    public void ToString_ReturnsUnderlyingValue() {
        NanoId id = NanoId.NewId();
        Assert.Equal(id.Value, id.ToString());
    }

    [Fact]
    public void TryFormat_BufferTooSmall_ReturnsFalse() {
        NanoId id = NanoId.NewId(21);
        Span<char> buffer = stackalloc char[20]; // 21 lazım

        bool success = id.TryFormat(buffer, out int written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryFormat_ValidBuffer_WritesValue() {
        NanoId id = NanoId.NewId(10);
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
        NanoId id1 = NanoId.Parse("abc-123");
        NanoId id2 = NanoId.Parse("abc-123");

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.True(id1.Equals(id2));
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse() {
        NanoId id1 = NanoId.NewId();
        NanoId id2 = NanoId.NewId();

        Assert.NotEqual(id1, id2);
        Assert.True(id1 != id2);
        Assert.False(id1.Equals(id2));
    }

    [Fact]
    public void CompareTo_WorksCorrectly() {
        NanoId idA = NanoId.Parse("aaaaa");
        NanoId idB = NanoId.Parse("bbbbb");

        Assert.True(idA.CompareTo(idB) < 0);
        Assert.True(idB.CompareTo(idA) > 0);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void JsonSerializer_SerializesAsString() {
        NanoId id = NanoId.NewId();
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

        NanoId result = (NanoId)converter.ConvertFrom(input)!;

        Assert.Equal(input, result.Value);
    }

    #endregion

    #region Implicit/Explicit Operator Tests

    [Fact]
    public void ImplicitOperator_ToString_Works() {
        NanoId id = NanoId.Parse("my-id");
        string s = id;

        Assert.Equal("my-id", s);
    }

    [Fact]
    public void ExplicitOperator_FromString_Works() {
        const string s = "explicit-id";
        NanoId id = (NanoId)s;

        Assert.Equal(s, id.Value);
    }

    #endregion

    [Fact]
    public void Digit_Alphabet_Is_Uniform() {
        const int Iterations = 100_000;
        const int Length = 20;

        var counts = new int[10];

        for(int i = 0; i < Iterations; i++) {
            foreach(var c in NanoId.NewId(NanoId.Alphabets.Numeric, Length).Value)
                counts[c - '0']++;
        }

        double expected = Iterations * Length / 10.0D;

        foreach(int c in counts) {
            Assert.InRange(c, expected * .99, expected * 1.01);
        }
    }
}