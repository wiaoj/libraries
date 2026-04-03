namespace Wiaoj.Primitives.Tests.Unit.UnixTimestampTests;

public sealed class UnixTimestampConversionTests {

    [Fact]
    public void From_DateTimeOffset_ShouldConvertCorrectly() {
        DateTimeOffset dto = new(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        UnixTimestamp ts = UnixTimestamp.From(dto);
        Assert.Equal(dto.ToUnixTimeMilliseconds(), ts.TotalMilliseconds);
    }

    [Fact]
    public void From_DateTime_Utc_ShouldConvertCorrectly() {
        DateTime dt = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        UnixTimestamp ts = UnixTimestamp.From(dt);
        Assert.Equal(new DateTimeOffset(dt).ToUnixTimeMilliseconds(), ts.TotalMilliseconds);
    }

    [Fact]
    public void From_DateTime_Local_ShouldConvertToUtc() {
        DateTime dtLocal = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Local);
        UnixTimestamp ts = UnixTimestamp.From(dtLocal);
        long expected = new DateTimeOffset(dtLocal.ToUniversalTime()).ToUnixTimeMilliseconds();
        Assert.Equal(expected, ts.TotalMilliseconds);
    }

    [Fact]
    public void From_DateTime_Unspecified_ShouldTreatAsUtc() {
        DateTime dtUnspec = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        UnixTimestamp ts = UnixTimestamp.From(dtUnspec);
        Assert.Equal(1672531200000, ts.TotalMilliseconds);
    }

    [Fact]
    public void ToDateTimeUtc_ShouldReturnUtcKind() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1672531200000);
        DateTime dt = ts.ToDateTimeUtc();
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
    }

    [Fact]
    public void ToDateTimeLocal_ShouldReturnLocalKind() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1672531200000);
        DateTime dt = ts.ToDateTimeLocal();
        Assert.Equal(DateTimeKind.Local, dt.Kind);
    }

    [Fact]
    public void ToDateTimeOffset_ShouldReturnOffsetZero() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1000);
        DateTimeOffset dto = ts.ToDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, dto.Offset);
    }

    [Fact]
    public void Cast_Operators_DateTimeOffset() {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        UnixTimestamp ts = (UnixTimestamp)now;
        DateTimeOffset back = ts;
        Assert.Equal(now, back);
    }

    [Fact]
    public void Extensions_ToUnixTimestamp_ShouldMapCorrectly() {
        DateTimeOffset dto = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTime dtUtc = dto.UtcDateTime;

        Assert.Equal(dto.ToUnixTimeMilliseconds(), dto.ToUnixTimestamp().TotalMilliseconds);
        Assert.Equal(dto.ToUnixTimeMilliseconds(), dtUtc.ToUnixTimestamp().TotalMilliseconds);
    }

    [Fact]
    public void ImplicitCast_FromDateTimeOffset_ShouldWorkSeamlessly() {
        DateTimeOffset dto = new(2025, 5, 20, 10, 30, 0, TimeSpan.Zero);

        // Açıkça cast etmeden doğrudan atama (Implicit Cast)
        UnixTimestamp ts = dto;

        Assert.Equal(dto.ToUnixTimeMilliseconds(), ts.TotalMilliseconds);
    }

    [Fact]
    public void ImplicitCast_ToDateTimeOffset_ShouldWorkSeamlessly() {
        UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1716193800000);

        // UnixTimestamp'i doğrudan DateTimeOffset bekleyen yere gönderme
        DateTimeOffset dto = ts;

        Assert.Equal(ts.TotalMilliseconds, dto.ToUnixTimeMilliseconds());
        Assert.Equal(TimeSpan.Zero, dto.Offset);
    }
}