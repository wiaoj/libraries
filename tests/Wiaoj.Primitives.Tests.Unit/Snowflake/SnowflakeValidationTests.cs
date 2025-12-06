using Wiaoj.Primitives.Snowflake;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;
public class SnowflakeValidationTests {

    [Fact]
    public void Parse_Should_Throw_On_Invalid_Format() {
        Assert.Throws<FormatException>(() => SnowflakeId.Parse("invalid-text", null));
        Assert.Throws<FormatException>(() => SnowflakeId.Parse("", null));
    }

    [Fact]
    public void TryParse_Should_Return_False_On_Invalid_Format() {
        bool result = SnowflakeId.TryParse("not-a-number", null, out var id);
        Assert.False(result);
        Assert.Equal(SnowflakeId.Empty, id);
    }

    [Fact]
    public void FromBytes_Should_Throw_On_Small_Buffer() {
        // 8 byte gerekli, 5 byte veriyoruz.
        byte[] smallBuffer = new byte[5];

        Assert.Throws<ArgumentException>(() => SnowflakeId.FromBytes(smallBuffer));
    }

    [Fact]
    public void CompareTo_Should_Throw_On_Wrong_Type() {
        var id = SnowflakeId.NewId();
        object wrongType = "string-object";

        Assert.Throws<ArgumentException>(() => id.CompareTo(wrongType));
    }

    [Fact]
    public void CompareTo_Null_Should_Return_Positive() {
        var id = SnowflakeId.NewId();
        // IComparable kuralı: Null ile kıyaslanan nesne her zaman büyüktür.
        int result = id.CompareTo(null);
        Assert.True(result > 0);
    }
}