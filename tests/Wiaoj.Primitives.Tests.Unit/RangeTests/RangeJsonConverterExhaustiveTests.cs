using System.Numerics;
using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit.RangeTests;

public sealed class RangeJsonConverterExhaustiveTests {
    private readonly JsonSerializerOptions _options = new();

    // Factory'nizdeki switch bloğunda bulunan TÜM tipler için birer örnek (mock) data hazırlıyoruz.
    public static TheoryData<object, Type> GetEverySupportedType() {
        TheoryData<object, Type> data = new() {
            // 1. Temel Sayısal Tipler (Reflection-Free Converters)
            { new Range<int>(10, 20), typeof(int) },
            { new Range<long>(100L, 200L), typeof(long) },
            { new Range<double>(1.5, 3.5), typeof(double) },
            { new Range<float>(1.5f, 3.5f), typeof(float) },
            { new Range<decimal>(10.5m, 20.5m), typeof(decimal) },
            { new Range<byte>(1, 10), typeof(byte) },
            { new Range<sbyte>(-5, 5), typeof(sbyte) },
            { new Range<short>(-100, 100), typeof(short) },
            { new Range<ushort>(10, 200), typeof(ushort) },
            { new Range<uint>(10u, 20u), typeof(uint) },
            { new Range<ulong>(100ul, 200ul), typeof(ulong) },
            { new Range<char>('A', 'Z'), typeof(char) },

            // 2. İleri Seviye Sayısal Tipler (Parse-Based Converters)
            { new Range<BigInteger>(BigInteger.Parse("999999999999999"), BigInteger.Parse("1000000000000000")), typeof(BigInteger) },
            { new Range<Int128>(Int128.Parse("-999999"), Int128.Parse("999999")), typeof(Int128) },
            { new Range<UInt128>(UInt128.Parse("1000"), UInt128.Parse("2000")), typeof(UInt128) },
            { new Range<Half>((Half)1.5f, (Half)3.5f), typeof(Half) }
        };

        // 3. Zaman Tipleri (Time-Based Converters)
        var dt = DateTime.UtcNow;
        data.Add(new Range<DateTime>(dt.AddDays(-1), dt), typeof(DateTime));
        data.Add(new Range<DateTimeOffset>(dt.AddDays(-1), dt), typeof(DateTimeOffset));
        data.Add(new Range<DateOnly>(new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1)), typeof(DateOnly));
        data.Add(new Range<TimeOnly>(new TimeOnly(10, 0, 0), new TimeOnly(15, 30, 0)), typeof(TimeOnly));
        data.Add(new Range<TimeSpan>(TimeSpan.FromHours(1), TimeSpan.FromHours(5)), typeof(TimeSpan));
        data.Add(new Range<UnixTimestamp>(UnixTimestamp.FromMilliseconds(100000), UnixTimestamp.FromMilliseconds(200000)), typeof(UnixTimestamp));

        // 4. Domain & Özel Primitives (Özel Converterlar)
        data.Add(new Range<Percentage>(Percentage.FromDouble(0.1), Percentage.FromDouble(0.9)), typeof(Percentage));

        // Not: SemVer, NanoId, GuidV7, SnowflakeId constructor'larını/Parse metodlarını projendeki yapısına göre düzenleyebilirsin.
        data.Add(new Range<SemVer>(SemVer.Parse("1.0.0"), SemVer.Parse("2.5.0")), typeof(SemVer));

        // Örnek kullanım olarak Parse ekliyorum. Sen kendi projende nasıl üretiyorsan öyle yarat:
        // data.Add(new Range<GuidV7>(GuidV7.NewId(), GuidV7.NewId()), typeof(GuidV7));
        // data.Add(new Range<NanoId>(NanoId.NewId(), NanoId.NewId()), typeof(NanoId));
        // data.Add(new Range<SnowflakeId>(new SnowflakeId(1000), new SnowflakeId(2000)), typeof(SnowflakeId));

        return data;
    }

    [Theory]
    [MemberData(nameof(GetEverySupportedType))]
    public void EveryExplicitTypeInFactory_ShouldSerializeAndDeserializeCorrectly(object expectedRange, Type genericType) {
        // Arrange
        Type targetRangeType = typeof(Range<>).MakeGenericType(genericType);

        // Act - Serialize
        string json = JsonSerializer.Serialize(expectedRange, targetRangeType, this._options);

        // Act - Deserialize
        object? deserializedRange = JsonSerializer.Deserialize(json, targetRangeType, this._options);

        // Assert - Objelerin birebir aynı değerlerde çıkıp çıkmadığı kontrol edilir.
        Assert.NotNull(deserializedRange);
        Assert.Equal(expectedRange, deserializedRange);
    }
}