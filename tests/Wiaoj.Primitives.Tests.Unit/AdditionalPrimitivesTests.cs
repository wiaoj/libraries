using System.Text.Json;

namespace Wiaoj.Primitives.Tests.Unit;
public sealed class AdditionalPrimitivesTests {

    #region 1. Base62String Tests (Kritik Eksik)

    [Fact]
    public void Base62_RoundTrip_Int64() {
        long value = 1234567890123456789;
        Base62String b62 = Base62String.FromInt64(value);
        long decoded = b62.ToInt64();

        Assert.Equal(value, decoded);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(61, "z")]
    [InlineData(62, "10")]
    public void Base62_FromInt64_SimpleValues(long input, string expected) {
        Base62String b62 = Base62String.FromInt64(input);
        Assert.Equal(expected, b62.Value);
    }

    [Fact]
    public void Base62_FromBytes_ShouldWork() {
        byte[] data = { 0xFF, 0x00, 0xAA };
        Base62String b62 = Base62String.FromBytes(data);
        byte[] decoded = b62.ToBytes();

        Assert.Equal(data, decoded);
    }

    [Fact]
    public void Base62_Parse_Invalid_ShouldThrow() {
        Assert.Throws<FormatException>(() => Base62String.Parse("Inva!lid")); // '!' invalid
    }

    #endregion

    #region 2. Sha256Hash Tests

    [Fact]
    public void Sha256Hash_Equality_ShouldWork() {
        Sha256Hash hash1 = Sha256Hash.Compute("test");
        Sha256Hash hash2 = Sha256Hash.Compute("test");
        Sha256Hash hash3 = Sha256Hash.Compute("other");

        Assert.True(hash1 == hash2);
        Assert.False(hash1 == hash3);
        Assert.Equal(hash1.GetHashCode(), hash2.GetHashCode());
    }

    [Fact]
    public void Sha256Hash_Parse_Hex_ShouldWork() {
        Sha256Hash original = Sha256Hash.Compute("test");
        HexString hex = original.ToHexString();

        Sha256Hash parsed = Sha256Hash.From(hex);
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Sha256Hash_Compute_From_Secret_ShouldNotLeak() {
        using Secret<byte> secret = Secret.From("secret-data");
        Sha256Hash hash = Sha256Hash.Compute(secret);

        Sha256Hash expected = Sha256Hash.Compute("secret-data");
        Assert.Equal(expected, hash);
    }

    #endregion

    #region 3. NanoId Tests

    [Fact]
    public void NanoId_NewId_ShouldHaveDefaultLength() {
        NanoId id = NanoId.NewId();
        Assert.Equal(21, id.Value.Length);
    }

    [Fact]
    public void NanoId_CustomLength_ShouldWork() {
        NanoId id = NanoId.NewId(10);
        Assert.Equal(10, id.Value.Length);
    }

    [Fact]
    public void NanoId_CustomAlphabet_ShouldWork() {
        string alphabet = "abc";
        NanoId id = NanoId.NewId(alphabet, 5);

        Assert.Equal(5, id.Value.Length);
        foreach(char c in id.Value) {
            Assert.Contains(c, alphabet);
        }
    }

    [Fact]
    public void NanoId_Parse_Invalid_ShouldThrow() {
        // NanoId allows A-Za-z0-9_-
        Assert.Throws<FormatException>(() => NanoId.Parse("Invalid@ID"));
    }

    #endregion

    #region 4. Empty Struct Tests

    [Fact]
    public void Empty_Equality_And_Json() {
        var e1 = Empty.Default;
        Empty e2 = new();

        Assert.Equal(e1, e2);
        Assert.Equal(e1.GetHashCode(), e2.GetHashCode());

        string json = JsonSerializer.Serialize(e1);
        Assert.Equal("{}", json); // Empty struct serializes to empty object

        var deserialized = JsonSerializer.Deserialize<Empty>(json);
        Assert.Equal(e1, deserialized);
    }

    #endregion

    #region 5. OperationTimeout Tests

    [Fact]
    public void OperationTimeout_Infinite_ShouldBeDefault() {
        var timeout = OperationTimeout.Infinite;
        Assert.True(timeout.IsInfinite);
        Assert.False(timeout.IsTimeoutSet);
        Assert.False(timeout.IsCancellable);
    }

    [Fact]
    public void OperationTimeout_FromSeconds_ShouldSetDelay() {
        OperationTimeout timeout = OperationTimeout.FromSeconds(5);
        Assert.True(timeout.IsTimeoutSet);
        Assert.False(timeout.IsInfinite);
    }

    [Fact]
    public void OperationTimeout_ThrowIfExpired_ShouldThrowWhenCancelled() {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        OperationTimeout timeout = OperationTimeout.From(cts.Token);

        Assert.Throws<OperationCanceledException>(() => timeout.ThrowIfExpired());
    }

    [Fact]
    public async Task OperationTimeout_ExecuteAsync_ShouldRespectTimeout() {
        OperationTimeout timeout = OperationTimeout.FromSeconds(0.1); // 100ms

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await timeout.ExecuteAsync(async (t) => {
                await Task.Delay(500, t.Token); // Wait longer than timeout
            });
        });
    }

    #endregion
}