using Wiaoj.Primitives.Snowflake;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;

public class SnowflakeIdCoreTests {
    [Fact]
    public void Default_Is_Empty() {
        SnowflakeId id = default;
        Assert.Equal(SnowflakeId.Empty, id);
        Assert.Equal(0, id.Value);
    }

    [Fact]
    public void Equality_Checks_Work() {
        var id1 = new SnowflakeId(100);
        var id2 = new SnowflakeId(100);
        var id3 = new SnowflakeId(200);

        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
        Assert.True(id1 == id2);
        Assert.True(id1 != id3);
    }

    [Fact]
    public void Comparison_Checks_Work() {
        var small = new SnowflakeId(100);
        var large = new SnowflakeId(200);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.Equal(-1, small.CompareTo(large));
    }

    [Fact]
    public void Implicit_Conversions_Are_Safe() {
        long original = 123456789;
        SnowflakeId id = original; // Implicit Long -> ID
        long back = id;            // Implicit ID -> Long

        Assert.Equal(original, back);
    }

    [Fact]
    public void Int128_Conversion_Works() {
        long raw = 123456789;
        SnowflakeId id = new(raw);

        Int128 bigVal = id.ToInt128();
        Assert.Equal((Int128)raw, bigVal);

        // Implicit check
        Int128 implicitVal = id;
        Assert.Equal((Int128)raw, implicitVal);
    }
}