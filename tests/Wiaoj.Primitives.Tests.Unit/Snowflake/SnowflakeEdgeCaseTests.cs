using System.Collections.Concurrent;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Snowflake;
using Xunit.Abstractions;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;

public class SnowflakeEdgeCaseTests {
    private readonly ITestOutputHelper _output;

    public SnowflakeEdgeCaseTests(ITestOutputHelper output) {
        this._output = output;
    }

    [Fact]
    public void Should_Be_Thread_Safe_Under_Heavy_Load() {
        SnowflakeOptions options = new() { NodeId = 1, SequenceBits = 12 };
        SnowflakeGenerator generator = new(options);

        int threadCount = 20;
        int idsPerThread = 1000;
        ConcurrentBag<SnowflakeId> results = new();

        Parallel.For(0, threadCount, _ => {
            for (int i = 0; i < idsPerThread; i++) {
                results.Add(generator.NextId());
            }
        });

        Assert.Equal(threadCount * idsPerThread, results.Count);
        Assert.Equal(results.Count, results.Distinct().Count());
    }

    [Fact]
    public void Should_Throw_If_NodeId_Is_Too_Large() {
        // SequenceBits = 12 ise (Varsayılan)
        // Timestamp(41) + NodeId(?) + Sequence(12) + Sign(1) = 64
        // NodeIdBits = 63 - 41 - 12 = 10 bit.
        // 2^10 = 1024. Max NodeId = 1023 (0..1023).

        SnowflakeOptions options = new() {
            SequenceBits = 12,
            NodeId = 1024 // Sınırın 1 fazlası
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeGenerator(options));
        this._output.WriteLine($"Caught expected error: {ex.Message}");
    }

    [Fact]
    public void Should_Throw_If_Epoch_Is_In_Future() {
        var futureEpoch = DateTimeOffset.UtcNow.AddDays(1);
        SnowflakeOptions options = new() { Epoch = futureEpoch };

        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeGenerator(options));
    }

    [Fact]
    public void Should_Handle_Clock_Rollback_Gracefully() {
        // Arrange
        DateTimeOffset startTime = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        ControllableTimeProvider timeProvider = new(startTime);

        // YENİ: MaxDriftMs'i 10 saniye yapalım ki testimiz (5sn rollback) patlamasın.
        SnowflakeOptions options = new() {
            NodeId = 1,
            TimeProvider = timeProvider,
            MaxDriftMs = 10_000 // 10 Saniye tolerans
        };
        SnowflakeGenerator generator = new(options);

        // Act
        var id1 = generator.NextId();

        timeProvider.SetUtcNow(startTime.AddSeconds(-5));

        var id2 = generator.NextId();

        // Assert
        Assert.True(id2 > id1);

        // id2'nin timestamp'i, id1'in timestamp'inden küçük olmamalı (sanal zaman korundu)
        long ts1 = id1.Value >> 22;
        long ts2 = id2.Value >> 22;
        Assert.True(ts2 >= ts1);

        this._output.WriteLine($"Rollback handled. TS1: {ts1}, TS2: {ts2}. MaxDrift setting worked.");
    }

    [Fact]
    public void Should_Generate_KSorted_Ids() {
        // Snowflake ID'leri zamana göre sıralı (k-sorted) olmalıdır.

        FakeTimeProvider fakeTime = new();
        fakeTime.SetUtcNow(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero));
        SnowflakeGenerator gen = new(new SnowflakeOptions { TimeProvider = fakeTime });

        List<SnowflakeId> list = new() {
            gen.NextId()
        };
        fakeTime.Advance(TimeSpan.FromMilliseconds(100));
        list.Add(gen.NextId());
        fakeTime.Advance(TimeSpan.FromMilliseconds(50));
        list.Add(gen.NextId());

        // Liste zaten sıralı olmalı
        List<SnowflakeId> sortedList = list.OrderBy(x => x).ToList();

        Assert.True(list.SequenceEqual(sortedList));
    }

    [Fact]
    public void Should_Throw_If_MaxDrift_Is_Negative() {
        var options = new SnowflakeOptions {
            MaxDriftMs = -1
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeGenerator(options));
    }
}