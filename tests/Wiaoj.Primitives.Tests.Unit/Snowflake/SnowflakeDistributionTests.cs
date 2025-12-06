using Microsoft.Extensions.Time.Testing;
using Wiaoj.Primitives.Snowflake;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;

public class SnowflakeDistributionTests {
    [Fact]
    public void Different_Nodes_Should_Generate_Unique_Ids_At_Same_Timestamp() {
        // AMAÇ: İki farklı sunucu (Node 1 ve Node 2) TAMAMEN AYNI ANDA ID üretirse
        // NodeID farkından dolayı ID'ler farklı olmalıdır.

        // Arrange
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // Generator 1 (Node 1)
        var gen1 = new SnowflakeGenerator(new SnowflakeOptions {
            NodeId = 1,
            TimeProvider = fakeTime
        });

        // Generator 2 (Node 2)
        var gen2 = new SnowflakeGenerator(new SnowflakeOptions {
            NodeId = 2,
            TimeProvider = fakeTime
        });

        // Act
        var id1 = gen1.NextId();
        var id2 = gen2.NextId();

        // Assert
        Assert.NotEqual(id1, id2);

        // ID yapısını manuel kontrol et
        // Timestamp aynı olmalı
        long ts1 = id1.Value >> 22;
        long ts2 = id2.Value >> 22;
        Assert.Equal(ts1, ts2);

        // Node ID farklı olmalı (Bit shifting ile kontrol)
        // Varsayılan SequenceBits=12 ise NodeID 12 bit sola kaydırılmıştır.
        // NodeID maskesi (Sequence 12 bit atla, Node 10 bit al)
        long nodeMask = 1023; // 10 bit max
        long node1Extracted = (id1.Value >> 12) & nodeMask;
        long node2Extracted = (id2.Value >> 12) & nodeMask;

        Assert.Equal(1, node1Extracted);
        Assert.Equal(2, node2Extracted);
    }

    [Fact]
    public void Sequence_Should_Reset_On_Time_Moving_Forward() {
        // AMAÇ: Zaman ilerlediğinde Sequence'in 0'a (veya başlangıca) döndüğünü doğrulamak.

        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var gen = new SnowflakeGenerator(new SnowflakeOptions { TimeProvider = fakeTime });

        // T0 anında: Seq 1, Seq 2
        gen.NextId();
        var lastIdAtT0 = gen.NextId();
        long seqAtT0 = lastIdAtT0.Value & 4095;
        Assert.True(seqAtT0 > 0);

        // Zamanı 1 saat ileri al
        fakeTime.Advance(TimeSpan.FromHours(1));

        // T1 anında: Seq sıfırlanmalı
        var firstIdAtT1 = gen.NextId();
        long seqAtT1 = firstIdAtT1.Value & 4095;

        // Kodundaki "if (now > current) nextSequence = 0" mantığına göre 0 olmalı.
        Assert.Equal(0, seqAtT1);
    }
}