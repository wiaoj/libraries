using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;
[Collection("SnowflakeGlobal")]
public class SnowflakeIdStaticTests : IDisposable {
    [Fact]
    public void Default_Generator_And_Empty_Options_Should_Use_Node_Zero() {
        Assert.Equal(0L, SnowflakeGenerator.Default.NodeId);

        SnowflakeId.Configure(new SnowflakeOptions());
        Assert.Equal(0L, (SnowflakeId.NewId().Value >> 12) & 1023);
    }

    [Fact]
    public void Configure_Should_Update_Shared_Generator() {
        SnowflakeId.Configure(new SnowflakeOptions { NodeId = 1, SequenceBits = 12 });
        SnowflakeId id1 = SnowflakeId.NewId();

        SnowflakeId.Configure(new SnowflakeOptions { NodeId = 5, SequenceBits = 12 });
        SnowflakeId id2 = SnowflakeId.NewId();

        // Verification:
        // [Timestamp] [NodeId] [Sequence]
        long nodeId1 = (id1.Value >> 12) & 1023; // 1023 = 2^10 - 1 (10 bit node id)
        long nodeId2 = (id2.Value >> 12) & 1023;

        Assert.Equal(1, nodeId1);
        Assert.Equal(5, nodeId2);
    }

    [Fact]
    public async Task Invalid_Configuration_Should_Allow_Another_Thread_To_Configure() {
        SnowflakeId.Configure(7);
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowflakeId.Configure(1024));
        Assert.Equal(7L, (SnowflakeId.NewId().Value >> 12) & 1023);

        await Task.Factory.StartNew(() => SnowflakeId.Configure(8), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(8L, (SnowflakeId.NewId().Value >> 12) & 1023);
    }

    public void Dispose() {
        SnowflakeTestHelper.ResetGenerator();
    }
}
