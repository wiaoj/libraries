using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;
public sealed class PercentageTests {
    #region 1. Construction & Constants

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void FromDouble_WithValidValues_ShouldCreatePercentage(double value) {
        // Act
        Percentage p = Percentage.FromDouble(value);

        // Assert
        Assert.Equal(value, p.Value);
    }

    [Theory]
    [InlineData(-0.000001)]
    [InlineData(1.000001)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void FromDouble_WithInvalidValues_ShouldThrowException(double value) {
        // Act
        Action act = () => Percentage.FromDouble(value);

        // Assert
        Assert.ThrowsAny<ArgumentOutOfRangeException>(act);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(50, 0.5)]
    [InlineData(100, 1.0)]
    public void FromInt_WithValidValues_ShouldCreatePercentage(int input, double expected) {
        // Act
        Percentage p = Percentage.FromInt(input);

        // Assert
        Assert.Equal(expected, p.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void FromInt_WithInvalidValues_ShouldThrowException(int value) {
        // Act
        Action act = () => Percentage.FromInt(value);

        // Assert
        Assert.ThrowsAny<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Constants_ShouldHaveCorrectValues() {
        Assert.Equal(0.0, Percentage.Zero.Value);
        Assert.Equal(0.5, Percentage.Half.Value);
        Assert.Equal(1.0, Percentage.Full.Value);

        Assert.Equal(Percentage.Zero, GetMinValueHelper<Percentage>());
        Assert.Equal(Percentage.Full, GetMaxValueHelper<Percentage>());
    }

    #endregion

    #region 2. Math Operations (Clamping & Logic)

    [Fact]
    public void AddClamped_ShouldCapAtOne() {
        Percentage p1 = Percentage.FromDouble(0.6);
        Percentage p2 = Percentage.FromDouble(0.5);

        Percentage result = p1.AddClamped(p2);

        // 0.6 + 0.5 = 1.1, clamps to 1.0
        Assert.Equal(1.0, result.Value);
        Assert.True(result.IsOne);
    }

    [Fact]
    public void SubtractClamped_ShouldFloorAtZero() {
        Percentage p1 = Percentage.FromDouble(0.2);
        Percentage p2 = Percentage.FromDouble(0.5);

        Percentage result = p1.SubtractClamped(p2);

        // 0.2 - 0.5 = -0.3, clamps to 0.0
        Assert.Equal(0.0, result.Value);
        Assert.True(result.IsZero);
    }

    [Fact]
    public void Remaining_ShouldCalculateComplement() {
        Percentage p = Percentage.FromDouble(0.25);
        Assert.Equal(0.75, p.Remaining.Value);
    }

    [Fact]
    public void ApplyTo_ShouldScaleValue() {
        Percentage p = Percentage.FromInt(50); // 0.5
        Assert.Equal(100.0, p.ApplyTo(200.0));
    }

    #endregion

    #region 3. Operators (Comparisons & Arithmetic)

    [Fact]
    public void ComparisonOperators_PercentageVsPercentage_ShouldWork() {
        Percentage low = Percentage.FromDouble(0.2);
        Percentage high = Percentage.FromDouble(0.8);

        Assert.True(low < high);
        Assert.True(low <= high);
        Assert.True(high > low);
        Assert.True(high >= low);
        Assert.False(low == high);
        Assert.True(low != high);
    }

    [Fact]
    public void ComparisonOperators_PercentageVsDouble_ShouldWork() {
        Percentage p = Percentage.FromDouble(0.5);

        Assert.True(p > 0.4);
        Assert.True(p < 0.6);
        Assert.Equal(0.5, p);
        Assert.NotEqual(0.9, p);

        // Reverse order (double vs Percentage)
        Assert.True(0.4 < p);
        Assert.Equal(0.5, p);
    }

    [Fact]
    public void ComparisonOperators_PercentageVsInt_ShouldWork() {
        // Int comparison compares against the raw value (0.0 - 1.0).
        Percentage full = Percentage.Full; // 1.0
        Percentage zero = Percentage.Zero; // 0.0

        Assert.True(full == 1);
        Assert.True(zero == 0);

        Assert.True(full > 0);
        Assert.True(zero < 1);
    }

    [Fact]
    public void ArithmeticOperators_ShouldWorkCorrectly() {
        Percentage p50 = Percentage.FromDouble(0.5);

        // Multiply double
        Assert.Equal(50.0, p50 * 100.0);
        Assert.Equal(100.0, 200.0 * p50);

        // Multiply TimeSpan
        TimeSpan span = TimeSpan.FromHours(10);
        Assert.Equal(TimeSpan.FromHours(5), p50 * span);
        Assert.Equal(TimeSpan.FromHours(5), span * p50);

        // Multiply Percentage (0.5 * 0.5 = 0.25)
        Assert.Equal(0.25, (p50 * p50).Value);

        // Divide Percentage by Percentage (0.5 / 0.25 = 2.0)
        Percentage p25 = Percentage.FromDouble(0.25);
        Assert.Equal(2.0, p50 / p25);

        // Divide Percentage by Scalar (0.5 / 2 = 0.25)
        Assert.Equal(0.25, (p50 / 2.0).Value);
    }

    [Fact]
    public void Division_ByZero_ShouldThrow() {
        Percentage p = Percentage.FromDouble(0.5);
        Action act1 = () => { var x = p / Percentage.Zero; };
        Action act2 = () => { Percentage x = p / 0.0; };

        Assert.Throws<DivideByZeroException>(act1);
        Assert.Throws<DivideByZeroException>(act2);
    }

    #endregion

    #region 4. Casting

    [Fact]
    public void ImplicitCast_ToDouble_ShouldReturnRawValue() {
        Percentage p = Percentage.FromDouble(0.123);
        double d = p;
        Assert.Equal(0.123, d);
    }

    [Fact]
    public void ExplicitCast_FromDouble_ShouldCreatePercentage() {
        double d = 0.75;
        Percentage p = (Percentage)d;
        Assert.Equal(0.75, p.Value);
    }

    [Fact]
    public void ExplicitCast_InvalidDouble_ShouldThrow() {
        double d = 1.5;
        Action act = () => { Percentage p = (Percentage)d; };
        Assert.ThrowsAny<ArgumentOutOfRangeException>(act);
    }

    #endregion

    #region 5. Formatting & ToString

    [Fact]
    public void ToString_ShouldFormatAsPercentage_CurrentCulture() {
        // Force US culture for predictable output test
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Percentage p = Percentage.FromDouble(0.5);
            Assert.Equal("50%", p.ToString());
        }
        finally {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ToString_WithFormatProvider_ShouldRespectIt() {
        Percentage p = Percentage.FromDouble(0.125);
        CultureInfo trCulture = CultureInfo.GetCultureInfo("tr-TR");

        // P2 -> 2 decimal places. Turkish uses comma as decimal separator
        Assert.Equal("%12,50", p.ToString("P2", trCulture));
    }

    [Fact]
    public void TryFormat_ShouldWork() {
        Percentage p = Percentage.FromDouble(0.5);
        Span<char> buffer = stackalloc char[10];

        // Fix: Use en-US to avoid "50 %" (with space) which happens in some InvariantCulture implementations
        bool success = p.TryFormat(buffer, out int written, "P0", CultureInfo.GetCultureInfo("en-US"));

        Assert.True(success);
        Assert.Equal("50%", buffer[..written].ToString());
    }

    [Fact]
    public void TryFormat_BufferTooSmall_ShouldReturnFalse() {
        Percentage p = Percentage.FromDouble(0.5);

        // "50%" için en az 3 karakter lazım. Biz 1 karakterlik yer veriyoruz.
        Span<char> smallBuffer = stackalloc char[1];

        bool success = p.TryFormat(smallBuffer, out int charsWritten, "P0", CultureInfo.InvariantCulture);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }
    #endregion

    #region 6. Explicit Parsing (String & Span)

    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("1", 1.0)]
    [InlineData("0", 0.0)]
    public void Parse_String_ValidInputs_ShouldSucceed(string input, double expected) {
        // Act (Generic Helper for interface test)
        Percentage result = ParseHelper<Percentage>(input, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("50%")] // No longer supported
    [InlineData("1.1")] // Out of range
    [InlineData("-0.1")] // Out of range
    public void Parse_String_InvalidInputs_ShouldThrow(string input) {
        // Act
        Action act = () => ParseHelper<Percentage>(input, CultureInfo.InvariantCulture);

        if(input == null)
            Assert.ThrowsAny<ArgumentException>(act);
        else
            Assert.ThrowsAny<FormatException>(act);
    }

    [Fact]
    public void TryParse_String_ShouldReturnFalseForInvalid() {
        // Act
        bool success = TryParseHelper<Percentage>("invalid", CultureInfo.InvariantCulture, out Percentage result);

        // Assert
        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Fact]
    public void TryParse_Span_ShouldWork() {
        ReadOnlySpan<char> input = "0.25".AsSpan(); // Changed from "25%" to "0.25"

        // Act
        bool success = TryParseHelper<Percentage>(input.ToString(), CultureInfo.InvariantCulture, out Percentage result);

        // Assert
        Assert.True(success);
        Assert.Equal(0.25, result.Value);
    }

    #endregion

    #region 7. JSON Serialization

    [Fact]
    public void JsonSerialize_ShouldWriteRawNumber() {
        var model = new { Rate = Percentage.FromDouble(0.5) };
        string json = JsonSerializer.Serialize(model);

        // We expect raw number in JSON, not string "50%"
        Assert.Contains(":0.5", json);
    }

    [Fact]
    public void JsonDeserialize_Number_ShouldWork() {
        string json = "{ \"Rate\": 0.75 }";
        PercentageWrapper? obj = JsonSerializer.Deserialize<PercentageWrapper>(json);

        Assert.NotNull(obj);
        Assert.Equal(0.75, obj!.Rate.Value);
    }

    [Fact]
    public void JsonDeserialize_StringWithPercent_ShouldThrow() {
        // This should now FAIL because strict mode prohibits '%'
        string json = "{ \"Rate\": \"25%\" }";

        Action act = () => JsonSerializer.Deserialize<PercentageWrapper>(json);

        Assert.Throws<JsonException>(act);
    }

    [Fact]
    public void JsonDeserialize_StringRawNumber_ShouldWork() {
        string json = "{ \"Rate\": \"0.25\" }";
        PercentageWrapper? obj = JsonSerializer.Deserialize<PercentageWrapper>(json);

        Assert.NotNull(obj);
        Assert.Equal(0.25, obj!.Rate.Value);
    }

    [Fact]
    public void JsonDeserialize_InvalidValue_ShouldThrow() {
        string json = "{ \"Rate\": 1.5 }"; // Out of range number
        Action act = () => JsonSerializer.Deserialize<PercentageWrapper>(json);

        Assert.Throws<JsonException>(act);
    }

    [Fact]
    public void JsonDeserialize_InvalidString_ShouldThrow() {
        string json = "{ \"Rate\": \"invalid\" }";
        Action act = () => JsonSerializer.Deserialize<PercentageWrapper>(json);

        Assert.Throws<JsonException>(act);
    }

    // Helper class for JSON tests
    private class PercentageWrapper {
        public Percentage Rate { get; set; }
    }

    #endregion

    #region 8. Equality (IEquatable)

    [Fact]
    public void Equals_ShouldBeTrueForSameValues() {
        Percentage p1 = Percentage.FromDouble(0.33);
        Percentage p2 = Percentage.FromDouble(0.33);

        Assert.True(p1.Equals(p2));
        Assert.True(p1.Equals((object)p2));
        Assert.True(p1 == p2);
        Assert.False(p1 != p2);
    }

    [Fact]
    public void GetHashCode_ShouldBeSameForSameValues() {
        Percentage p1 = Percentage.FromDouble(0.5);
        Percentage p2 = Percentage.FromDouble(0.5);

        Assert.Equal(p1.GetHashCode(), p2.GetHashCode());
    }

    [Fact]
    public void CompareTo_ShouldSortCorrectly() {
        List<Percentage> list = [
            Percentage.Full,
            Percentage.Zero,
            Percentage.Half
        ];

        list.Sort();

        Assert.Equal(Percentage.Zero, list[0]);
        Assert.Equal(Percentage.Half, list[1]);
        Assert.Equal(Percentage.Full, list[2]);
    }

    [Fact]
    public void CompareTo_Object_ShouldThrowForInvalidType() {
        Percentage p = Percentage.Zero;
        Action act = () => ((IComparable)p).CompareTo("not a percentage");
        Assert.Throws<ArgumentException>(act);
    }

    #endregion

    #region 9. Missing Scenarios (UTF-8, NaN, Object Equality)

    [Fact]
    public void FromDouble_NaN_ShouldThrow() {
        // Since double.NaN < 0 is false and double.NaN > 1 is false, 
        // the FromDouble method has explicit NaN check.
        double nan = double.NaN;

        Action act = () => Percentage.FromDouble(nan);

        Assert.ThrowsAny<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void FromDouble_Infinity_ShouldThrow() {
        // PositiveInfinity > 1.0
        Action act = () => Percentage.FromDouble(double.PositiveInfinity);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(act);

        // NegativeInfinity < 0.0
        Action act2 = () => Percentage.FromDouble(double.NegativeInfinity);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(act2);
    }

    [Fact]
    public void Equals_Object_Null_ShouldReturnFalse() {
        Percentage p = Percentage.FromDouble(0.5);
        Assert.False(p.Equals(null));
    }

    [Fact]
    public void Equals_Object_DifferentType_ShouldReturnFalse() {
        Percentage p = Percentage.FromDouble(0.5);
        Assert.False(p.Equals("0.5")); // String
        Assert.False(p.Equals(0.5));   // Double
    }

    [Fact]
    public void Int_Comparison_Semantics_Check() {
        Percentage half = Percentage.Half; // 0.5

        // 0.5 == 1 -> False
        Assert.False(half == 1);

        // 0.5 < 1 -> True
        Assert.True(half < 1);

        // Percentage.Full (1.0) == 1 -> True
        Assert.True(Percentage.Full == 1);
    }

    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("1", 1.0)]
    public void Parse_Utf8_ValidInputs_ShouldWork(string inputString, double expected) {
        byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(inputString);

        // Act (Generic Helper for UTF8)
        Percentage result = ParseUtf8Helper<Percentage>(utf8Bytes, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void TryParse_Utf8_ValidInputs_ShouldWork() {
        byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes("0.25"); // Changed from "25%"

        bool success = TryParseUtf8Helper<Percentage>(utf8Bytes, CultureInfo.InvariantCulture, out Percentage result);

        Assert.True(success);
        Assert.Equal(0.25, result.Value);
    }

    [Fact]
    public void TryFormat_Utf8_ShouldWork() {
        Percentage p = Percentage.FromDouble(0.75);
        Span<byte> buffer = stackalloc byte[10];

        // Fix: Use en-US to ensure "75%" (no space) output
        bool success = p.TryFormat(buffer, out int written, "P0", CultureInfo.GetCultureInfo("en-US"));

        Assert.True(success);

        var resultString = System.Text.Encoding.UTF8.GetString(buffer[..written]);
        Assert.Equal("75%", resultString);
    }

    #endregion

    [Fact]
    public void Percentage_Parse_Should_Be_Culture_Invariant() {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try {
            // Virgül kullanan bir kültüre geçelim
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");

            // Noktalı string her zaman çalışmalı
            Assert.True(Percentage.TryParseInternal("0.5", CultureInfo.InvariantCulture, out var p));
            Assert.Equal(0.5, p.Value);
        }
        finally {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    #region Helpers for Static Interface Members

    // Helper method to access static abstract interface members (Parse)
    private static T ParseHelper<T>(string s, IFormatProvider? provider) where T : IParsable<T> {
        return T.Parse(s, provider);
    }

    // Helper method to access static abstract interface members (TryParse)
    private static bool TryParseHelper<T>(string? s, IFormatProvider? provider, out T? result) where T : IParsable<T> {
        return T.TryParse(s, provider, out result);
    }

    // Helper method to access static abstract interface members (MinValue)
    private static T GetMinValueHelper<T>() where T : IMinMaxValue<T> {
        return T.MinValue;
    }

    // Helper method to access static abstract interface members (MaxValue)
    private static T GetMaxValueHelper<T>() where T : IMinMaxValue<T> {
        return T.MaxValue;
    }

    private static T ParseUtf8Helper<T>(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) where T : IUtf8SpanParsable<T> {
        return T.Parse(utf8Text, provider);
    }

    private static bool TryParseUtf8Helper<T>(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out T result) where T : IUtf8SpanParsable<T> {
        return T.TryParse(utf8Text, provider, out result);
    }

    #endregion
}