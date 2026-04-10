using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit; 
public sealed class CronExpressionTests {
    [Theory]
    [Trait("Category", "Parsing")]
    // Standart Linux (5 Alan)
    [InlineData("* * * * *")]
    [InlineData("0 0 * * *")]
    [InlineData("*/5 * * * *")]
    [InlineData("0 12 15 1 *")]
    [InlineData("0 0 * * MON")] // 3 harfli alias
    [InlineData("0 0 L * *")]   // L sembolü
    [InlineData("0 0 * * 5L")]
    // Quartz Standartları (6 ve 7 Alan)
    [InlineData("0 0 12 * * ?")]      // Saniye dahil (6 alan)
    [InlineData("0 0 12 * * ? 2026")] // Saniye ve Yıl dahil (7 alan)
    [InlineData("15 30 10 * * ?")]
    public void Parse_ValidCronStrings_ShouldSucceed(string validCron) {
        CronExpression cron = CronExpression.Parse(validCron);
        Assert.Equal(validCron, cron.Value);
    }

    [Theory]
    [Trait("Category", "Parsing")]
    [InlineData("* * * * *")]
    [InlineData("0 0 * * *")]
    [InlineData("0 0 12 * * ? 2026")]
    public void TryParse_ValidCronStrings_ShouldReturnTrue(string validCron) {
        bool isValid = CronExpression.TryParse(validCron, out var cron);
        Assert.True(isValid);
        Assert.Equal(validCron, cron.Value);
    }

    [Theory]
    [Trait("Category", "Parsing")]
    [InlineData("10,20,30 * * * *")]    // Liste kullanımı
    [InlineData("1-5 * * * *")]         // Aralık kullanımı
    [InlineData("*/15 * * * *")]        // Bölme kullanımı
    [InlineData("0 0 LW * *")]          // Last Weekday
    [InlineData("0 0 * * 5#3")]         // Ayın 3. Cuma günü
    public void Parse_ComplexStructuralSegments_ShouldParseSuccessfully(string complexCron) {
        var cron = CronExpression.Parse(complexCron);
        Assert.Equal(complexCron, cron.Value);
    }

    [Fact]
    [Trait("Category", "Parsing")]
    public void Parse_WithExtraSpaces_ShouldNormalizeToSingleSpaces() {
        string messyCron = "0    0     *   *   *";  // Tab ve fazla boşluklar
        string expected = "0 0 * * *";

        CronExpression cron = CronExpression.Parse(messyCron);

        Assert.Equal(expected, cron.Value);
    }

    [Theory]
    [Trait("Category", "Parsing")]
    [InlineData("0 0 * * mon")] // Hepsi küçük
    [InlineData("0 0 * * MON")] // Hepsi büyük
    [InlineData("0 0 * * mOn")] // Karışık
    [InlineData("0 0 1 jaN *")] // Karışık Ay
    public void Parse_MixedCaseAliases_ShouldParseSuccessfully(string mixedCaseCron) {
        var cron = CronExpression.Parse(mixedCaseCron);
        Assert.Equal(mixedCaseCron, cron.Value);
    }

    [Theory]
    [Trait("Category", "Validation")]
    // Yapısal Hatalar
    [InlineData("* * * *")]           // 4 alan (Eksik)
    [InlineData("* * * * * * * *")]   // 8 alan (Fazla)
    [InlineData("! * * * *")]         // Geçersiz karakter (!)
    [InlineData("abc def ghi jkl mno")] // Geçersiz 3+ karakterli harf kümeleri
    [InlineData("0 0 * * Monday")]    // Monday 3 harfli değil
    // Limit Aşımları (5 Alanlı Linux Mantığı - Logical Index + 1 kayar)
    [InlineData("0 60 * * *")]        // Dakika 60 olamaz (0-59)
    [InlineData("0 25 * * *")]        // Saat 25 olamaz (0-23)
    [InlineData("* * 32 * *")]        // Gün 32 olamaz (1-31)
    [InlineData("0 0 1 13 *")]        // Ay 13 olamaz (1-12)
    [InlineData("0 0 * * 8")]         // Haftanın Günü 8 olamaz (0-7)
    // Limit Aşımları (6-7 Alanlı Quartz Mantığı - Logical Index kaymaz)
    [InlineData("60 0 0 * * ?")]      // Saniye 60 olamaz (0-59)
    [InlineData("0 0 0 1 1 ? 2100")]  // Yıl 2100 olamaz (1970-2099)
    public void Parse_InvalidCronStrings_ShouldThrowFormatException(string invalidCron) {
        var ex = Assert.Throws<FormatException>(() => CronExpression.Parse(invalidCron));
        Assert.Contains("Invalid Cron expression", ex.Message);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void TryParse_InvalidCronString_ShouldReturnFalse() {
        string invalidCron = "99 99 99 99 99";
        bool isValid = CronExpression.TryParse(invalidCron, out var cron);

        Assert.False(isValid);
        Assert.Equal(string.Empty, cron.Value);
        Assert.Equal(CronExpression.Empty, cron);
    }

    [Theory]
    [Trait("Category", "Validation")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void TryParse_EmptyOrWhiteSpace_ShouldReturnEmptyExpression(string emptyCron) {
        bool isValid = CronExpression.TryParse(emptyCron, out var cron);

        Assert.True(isValid);
        Assert.Equal(string.Empty, cron.Value);
        Assert.Equal(CronExpression.Empty, cron);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void TryParse_NullString_ShouldReturnFalse() {
        bool isValid = CronExpression.TryParse((string?)null, out var cron);

        Assert.False(isValid);
        Assert.Equal(CronExpression.Empty, cron);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void TryParse_Utf8ByteSpan_ShouldParseCorrectly() {
        string cronStr = "0 0 12 * * ?";
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(cronStr);
        ReadOnlySpan<byte> span = utf8Bytes;

        bool isValid = CronExpression.TryParse(span, out var cron);

        Assert.True(isValid);
        Assert.Equal(cronStr, cron.Value);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void Parse_CharSpan_ShouldParseCorrectly() {
        ReadOnlySpan<char> span = "*/5 * * * *".AsSpan();
        CronExpression cron = CronExpression.Parse(span);
        Assert.Equal("*/5 * * * *", cron.Value);
    }

    [Fact]
    [Trait("Category", "Core")]
    public void Equals_SameExpressions_ShouldReturnTrue_EvenWithCaseDifference() {
        CronExpression cron1 = CronExpression.Parse("0 0 * * MON");
        CronExpression cron2 = CronExpression.Parse("0 0 * * mon");

        Assert.True(cron1.Equals(cron2));
        Assert.Equal(cron1.GetHashCode(), cron2.GetHashCode());
        Assert.True(cron1 == cron2);
    }

    [Fact]
    [Trait("Category", "Core")]
    public void ImplicitCastToString_ShouldReturnUnderlyingValue() {
        var cron = CronExpression.Daily;
        string strValue = cron;

        Assert.Equal("0 0 * * *", strValue);
    }

    [Fact]
    [Trait("Category", "Core")]
    public void ExplicitCastFromString_ShouldParseCorrectly() {
        CronExpression cron = (CronExpression)"0 12 * * *";
        Assert.Equal("0 12 * * *", cron.Value);
    }

    [Fact]
    [Trait("Category", "Core")]
    public void FactoryConstants_ShouldHaveCorrectExpectedValues() {
        Assert.Equal("* * * * *", CronExpression.Minutely.Value);
        Assert.Equal("0 * * * *", CronExpression.Hourly.Value);
        Assert.Equal("0 0 * * *", CronExpression.Daily.Value);
        Assert.Equal("0 0 * * 0", CronExpression.Weekly.Value);
        Assert.Equal("0 0 1 * *", CronExpression.Monthly.Value);
        Assert.Equal("0 0 1 1 *", CronExpression.Yearly.Value);
    }

    [Fact]
    [Trait("Category", "Serialization")]
    public void JsonSerialize_ShouldWriteAsStringValue() {
        var cron = CronExpression.Daily;
        JsonSerializerOptions options = new();

        string json = JsonSerializer.Serialize(cron, options);

        Assert.Equal("\"0 0 * * *\"", json);
    }

    [Fact]
    [Trait("Category", "Serialization")]
    public void JsonDeserialize_ValidString_ShouldReturnCronExpression() {
        string json = "\"*/5 * * * *\"";
        var cron = JsonSerializer.Deserialize<CronExpression>(json);

        Assert.Equal("*/5 * * * *", cron.Value);
    }

    [Fact]
    [Trait("Category", "Serialization")]
    public void JsonDeserialize_InvalidString_ShouldThrowJsonException() {
        string json = "\"99 99 99 99 99\"";
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CronExpression>(json));

        Assert.Contains("not a valid CronExpression", ex.Message);
    }

    [Fact]
    [Trait("Category", "Serialization")]
    public void TypeConverter_CanConvertFromString_ShouldReturnTrue() {
        var converter = TypeDescriptor.GetConverter(typeof(CronExpression));

        Assert.True(converter.CanConvertFrom(typeof(string)));

        var result = converter.ConvertFrom("0 0 * * *");
        Assert.IsType<CronExpression>(result);
        Assert.Equal("0 0 * * *", ((CronExpression)result).Value);
    }

    [Fact]
    [Trait("Category", "Serialization")]
    public void TypeConverter_CanConvertToString_ShouldReturnTrue() {
        var converter = TypeDescriptor.GetConverter(typeof(CronExpression));
        var cron = CronExpression.Weekly;

        Assert.True(converter.CanConvertTo(typeof(string)));

        var result = converter.ConvertTo(cron, typeof(string));
        Assert.IsType<string>(result);
        Assert.Equal("0 0 * * 0", result);
    }
}