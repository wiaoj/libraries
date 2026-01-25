using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.Tests.Unit; 
public sealed class PooledBitArrayTests {
    [Fact]
    public void Set_And_Get_ShouldWorkCorrectly() {
        using PooledBitArray bits = new(1000);

        bool changed = bits.Set(500);
        bool exists = bits.Get(500);
        bool notExists = bits.Get(501);

        Assert.True(changed);
        Assert.True(exists);
        Assert.False(notExists);
    }

    [Fact]
    public void PopCount_ShouldBeAccurate() {
        using PooledBitArray bits = new(1000);
        bits.Set(10);
        bits.Set(20);
        bits.Set(20); // Tekrar set etme sayılmamalı

        Assert.Equal(2, bits.GetPopCount());
    }

    [Fact]
    public void Checksum_ShouldChange_WhenDataChanges() {
        using PooledBitArray bits = new(1000);
        var checksum1 = bits.CalculateChecksum();

        bits.Set(100);
        var checksum2 = bits.CalculateChecksum();

        Assert.NotEqual(checksum1, checksum2);
    }
}
