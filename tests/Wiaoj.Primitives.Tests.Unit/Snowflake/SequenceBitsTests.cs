using System;
using System.Collections.Generic;
using System.Text;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;   
public class SequenceBitsTests {
    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(22)]
    public void Constructor_Accepts_Valid_Values(byte bits) {
        var seqBits = new SequenceBits(bits);
        Assert.Equal(bits, seqBits.Value);
    }

    [Theory]
    [InlineData(0)]  // Min altı
    [InlineData(23)] // Max üstü
    [InlineData(255)]
    public void Constructor_Throws_On_Invalid_Values(byte bits) {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceBits(bits));
    }

    [Fact]
    public void Implicit_Conversion_Works() {
        SequenceBits bits = 15; // int -> SequenceBits
        Assert.Equal(15, bits.Value);

        int value = bits; // SequenceBits -> int
        Assert.Equal(15, value);
    }

    [Fact]
    public void Equality_Check() {
        var b1 = new SequenceBits(10);
        var b2 = new SequenceBits(10);
        Assert.Equal(b1, b2);
    }
}