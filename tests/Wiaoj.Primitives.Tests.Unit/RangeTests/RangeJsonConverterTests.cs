using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit.RangeTests;

public sealed class RangeJsonConverterTests {
    private readonly JsonSerializerOptions _options;

    public RangeJsonConverterTests() {
        // Fabrikayı dahil edelim (record struct attributüyle otomatik gelse de emin olmak için opsiyona ekleyebiliriz)
        this._options = new JsonSerializerOptions();
        // [JsonConverter] attribute'u olduğu için aslında options eklemeden de çalışır.
    }

    [Fact]
    public void Int32Range_SerializeAndDeserialize_WorksCorrectly() {
        // Arrange
        Range<int> range = new(10, 20);
        string expectedJson = """{"Min":10,"Max":20}""";

        // Act
        string json = JsonSerializer.Serialize(range, this._options);
        var deserialized = JsonSerializer.Deserialize<Range<int>>(json, this._options);

        // Assert
        Assert.Equal(expectedJson, json);
        Assert.Equal(range, deserialized);
    }

    [Fact]
    public void DateTimeRange_SerializeAndDeserialize_WorksCorrectly() {
        // Arrange
        DateTime min = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime max = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Range<DateTime> range = new(min, max);

        // Act
        string json = JsonSerializer.Serialize(range, this._options);
        var deserialized = JsonSerializer.Deserialize<Range<DateTime>>(json, this._options);

        // Assert
        Assert.Equal(range.Min, deserialized.Min);
        Assert.Equal(range.Max, deserialized.Max);
    }

    [Fact]
    public void TimeSpanRange_ParseBasedConverter_SerializeAndDeserialize_WorksCorrectly() {
        // Arrange (TimeSpan string tabanlı dönüştürücüyü test eder)
        var range = new Range<TimeSpan>(TimeSpan.FromHours(1), TimeSpan.FromHours(5));
        string expectedJson = """{"Min":"01:00:00","Max":"05:00:00"}""";

        // Act
        string json = JsonSerializer.Serialize(range, _options);
        var deserialized = JsonSerializer.Deserialize<Range<TimeSpan>>(json, _options);

        // Assert
        Assert.Equal(expectedJson, json);
        Assert.Equal(range, deserialized);
    }

    [Fact]
    public void BigIntegerRange_ParseBasedConverter_SerializeAndDeserialize_WorksCorrectly() {
        // Arrange (Çok büyük sayılar string olarak tutulduğu için bu test kritiktir)
        var min = System.Numerics.BigInteger.Parse("999999999999999999999999999");
        var max = System.Numerics.BigInteger.Parse("1000000000000000000000000000");
        var range = new Range<System.Numerics.BigInteger>(min, max);

        // Act
        string json = JsonSerializer.Serialize(range, _options);
        var deserialized = JsonSerializer.Deserialize<Range<System.Numerics.BigInteger>>(json, _options);

        // Assert
        Assert.Equal(range.Min, deserialized.Min);
        Assert.Equal(range.Max, deserialized.Max);
    }

    [Fact]
    public void CustomTypeFallback_StringRange_SerializeAndDeserialize_WorksCorrectly() {
        // Arrange
        // String için fallback mekanizması (CreateGenericConverter) çalışacaktır.
        Range<string> range = new("A", "Z");
        string expectedJson = """{"Min":"A","Max":"Z"}""";

        // Act
        string json = JsonSerializer.Serialize(range, this._options);
        var deserialized = JsonSerializer.Deserialize<Range<string>>(json, this._options);

        // Assert
        Assert.Equal(expectedJson, json);
        Assert.Equal(range, deserialized);
    }

    [Fact]
    public void Deserialize_InvalidToken_ThrowsJsonException() {
        // Arrange
        string invalidJson = "[]"; // StartObject bekleniyor, ancak StartArray gönderiyoruz.

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Range<int>>(invalidJson, this._options));
    }

    [Fact]
    public void Deserialize_MissingProperties_InitializesToDefaultValues() {
        // Arrange
        // Sadece Min var, Max yok. Struct olduğu için Max default(int) yani 0 olmalı.
        // Fakat Range kurucusu (0 < 10) kontrolünden dolayı "Min > Max" diye ArgumentException atacaktır.
        string partialJson = """{"Min":10}""";

        // Act & Assert
        // Deserializer reflection ile değil de objeyi oluştururken `new Range<T>(min, max)` çağırdığı için exception fırlatır.
        Assert.ThrowsAny<ArgumentException>(() => JsonSerializer.Deserialize<Range<int>>(partialJson, this._options));
    }

    [Theory]
    [InlineData(typeof(int), 10, 20)]
    [InlineData(typeof(long), 10L, 20L)]
    [InlineData(typeof(double), 1.5, 3.5)]
    [InlineData(typeof(float), 1.5f, 3.5f)]
    [InlineData(typeof(decimal), 1.5, 3.5)] // cast edilecek
    [InlineData(typeof(byte), (byte)1, (byte)5)]
    [InlineData(typeof(short), (short)1, (short)5)]
    [InlineData(typeof(char), 'a', 'z')]
    public void AllPrimitiveTypes_SerializeAndDeserialize_WorksProperly(Type type, object min, object max) {
        // Arrange
        // decimal gibi tipler InlineData içinde direk verilemediği için convert yapıyoruz.
        min = Convert.ChangeType(min, type);
        max = Convert.ChangeType(max, type);

        Type rangeType = typeof(Range<>).MakeGenericType(type);
        object range = Activator.CreateInstance(rangeType, min, max)!;

        // Act
        string json = JsonSerializer.Serialize(range, rangeType, _options);
        object deserialized = JsonSerializer.Deserialize(json, rangeType, _options)!;

        // Assert
        Assert.Equal(range, deserialized);
    }
}