using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;
[Collection("SnowflakeGlobal")]
public class SnowflakeIdStaticTests : IDisposable {
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

    public void Dispose() {
        SnowflakeTestHelper.ResetGenerator();
    }
}