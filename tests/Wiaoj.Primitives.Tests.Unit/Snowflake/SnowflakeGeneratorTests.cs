using Microsoft.Extensions.Time.Testing;
using Wiaoj.Primitives.Snowflake;
using Xunit;
using Xunit.Abstractions;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;
public class SnowflakeGeneratorTests {
    private readonly ITestOutputHelper _output;

    public SnowflakeGeneratorTests(ITestOutputHelper output) {
        _output = output;
    }

    [Fact]
    public void Should_Generate_Unique_Ids_Across_Time() {
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var options = new SnowflakeOptions { NodeId = 1, TimeProvider = fakeTime };
        var generator = new SnowflakeGenerator(options);

        var id1 = generator.NextId();
        fakeTime.Advance(TimeSpan.FromMilliseconds(1));
        var id2 = generator.NextId();

        Assert.NotEqual(id1, id2);
        Assert.True(id2 > id1);
    }

    [Fact]
    public void Should_Borrow_Time_On_Sequence_Overflow_In_Burst_Mode() {
        // ARRANGE
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var options = new SnowflakeOptions {
            NodeId = 1,
            SequenceBits = 12, // Max 4096 ID per ms
            TimeProvider = fakeTime
        };
        var generator = new SnowflakeGenerator(options);

        // ACT
        var ids = new List<SnowflakeId>();
        // 4096 kapasiteyi doldur + 1 tane taşır (Toplam 4097)
        for (int i = 0; i < 4097; i++) {
            ids.Add(generator.NextId());
        }

        // ASSERT
        Assert.Equal(4097, ids.Count);

        // İlk ID'nin Timestamp'i
        long firstTs = ids[0].Value >> 22;

        // 4096. ID (Index 4095) aynı milisaniyede olmalı
        long lastInBatchTs = ids[4095].Value >> 22;
        Assert.Equal(firstTs, lastInBatchTs);

        // 4097. ID (Index 4096) sequence dolduğu için BİR SONRAKİ milisaniyeye (sanal olarak) geçmeli
        // Not: FakeTime hiç ilerlemedi, ama generator sanal olarak ilerledi.
        var overflowId = ids[4096];
        long overflowTs = overflowId.Value >> 22;
        long overflowSeq = overflowId.Value & 4095;

        Assert.Equal(firstTs + 1, overflowTs);  
        Assert.Equal(0, overflowSeq);         

        _output.WriteLine("Burst mode worked: Logical time advanced by 1ms without blocking.");
    }
}