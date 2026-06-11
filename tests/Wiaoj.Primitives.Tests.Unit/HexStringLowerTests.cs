using System.Text;
using Wiaoj.Primitives.Cryptography.Hashing;
using Wiaoj.Primitives.Cryptography.Symmetric;
using Wiaoj.Primitives.Hashing;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit;

/// <summary>
/// Tests for zero-allocation lowercase hex support:
/// HexString.FromBytesLower, FromUtf8Lower, FromLower, and ToHexStringLower() across all types.
/// </summary>
public sealed class HexStringLowerTests {

    #region HexString.FromBytesLower

    [Fact]
    public void FromBytesLower_Should_Produce_Lowercase() {
        byte[] data = [0xDE, 0xAD, 0xBE, 0xEF];
        HexString hex = HexString.FromBytesLower(data);

        Assert.Equal("deadbeef", hex.Value);
    }

    [Fact]
    public void FromBytesLower_Empty_Should_Return_Empty() {
        HexString hex = HexString.FromBytesLower(ReadOnlySpan<byte>.Empty);

        Assert.Equal(string.Empty, hex.Value);
    }

    [Fact]
    public void FromBytesLower_Should_Decode_Back_To_Same_Bytes() {
        byte[] original = [0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF];
        HexString hex = HexString.FromBytesLower(original);
        byte[] decoded = hex.ToBytes();

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void FromBytesLower_Should_Equal_FromBytes_ToLower() {
        byte[] data = [0xCA, 0xFE, 0xBA, 0xBE];

        HexString fromLower = HexString.FromBytesLower(data);
        HexString fromUpperThenLower = HexString.FromBytes(data).ToLower();

        Assert.Equal(fromUpperThenLower.Value, fromLower.Value);
    }

    [Fact]
    public void FromBytesLower_ToLower_Should_Return_Same_Instance() {
        byte[] data = [0xFF, 0x00];
        HexString hex = HexString.FromBytesLower(data);

        // Since it's already lowercase, ToLower should short-circuit and return this
        HexString lower = hex.ToLower();
        Assert.Equal(hex.Value, lower.Value);
    }

    #endregion

    #region HexString.FromUtf8Lower / FromLower

    [Fact]
    public void FromUtf8Lower_Should_Produce_Lowercase() {
        HexString hex = HexString.FromUtf8Lower("hello");

        Assert.Equal("68656c6c6f", hex.Value);
    }

    [Fact]
    public void FromUtf8Lower_Empty_Should_Return_Empty() {
        HexString hex = HexString.FromUtf8Lower("");
        Assert.Equal(string.Empty, hex.Value);

        HexString hexNull = HexString.FromUtf8Lower(null!);
        Assert.Equal(string.Empty, hexNull.Value);
    }

    [Fact]
    public void FromLower_WithEncoding_Should_Produce_Lowercase() {
        HexString hex = HexString.FromLower("hello", Encoding.UTF8);
        Assert.Equal("68656c6c6f", hex.Value);
    }

    #endregion

    #region Sha256Hash.ToHexStringLower

    [Fact]
    public void Sha256Hash_ToHexStringLower_Should_Be_Lowercase() {
        Sha256Hash hash = Sha256Hash.Compute("test");
        HexString hex = hash.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(64, hex.Value.Length);
    }

    [Fact]
    public void Sha256Hash_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        Sha256Hash hash = Sha256Hash.Compute("test");

        Assert.Equal(hash.ToHexString().ToLower().Value, hash.ToHexStringLower().Value);
    }

    [Fact]
    public void Sha256Hash_ToHexStringLower_Should_RoundTrip() {
        Sha256Hash original = Sha256Hash.Compute("round-trip");
        HexString hex = original.ToHexStringLower();
        Sha256Hash parsed = Sha256Hash.From(hex);

        Assert.Equal(original, parsed);
    }

    #endregion

    #region Sha512Hash.ToHexStringLower

    [Fact]
    public void Sha512Hash_ToHexStringLower_Should_Be_Lowercase() {
        Sha512Hash hash = Sha512Hash.Compute("test");
        HexString hex = hash.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(128, hex.Value.Length);
    }

    [Fact]
    public void Sha512Hash_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        Sha512Hash hash = Sha512Hash.Compute("test");

        Assert.Equal(hash.ToHexString().ToLower().Value, hash.ToHexStringLower().Value);
    }

    #endregion

    #region Md5Hash.ToHexStringLower

    [Fact]
    public void Md5Hash_ToHexStringLower_Should_Be_Lowercase() {
        Md5Hash hash = Md5Hash.Compute("test");
        HexString hex = hash.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(32, hex.Value.Length);
    }

    [Fact]
    public void Md5Hash_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        Md5Hash hash = Md5Hash.Compute("test");

        Assert.Equal(hash.ToHexString().ToLower().Value, hash.ToHexStringLower().Value);
    }

    #endregion

    #region HmacSha256Hash.ToHexStringLower

    [Fact]
    public void HmacSha256Hash_ToHexStringLower_Should_Be_Lowercase() {
        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        HmacSha256Hash hash = HmacSha256Hash.Compute(key, "test"u8);
        HexString hex = hash.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(64, hex.Value.Length);
    }

    [Fact]
    public void HmacSha256Hash_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        HmacSha256Hash hash = HmacSha256Hash.Compute(key, "test"u8);

        Assert.Equal(hash.ToHexString().ToLower().Value, hash.ToHexStringLower().Value);
    }

    #endregion

    #region HmacSha512Hash.ToHexStringLower

    [Fact]
    public void HmacSha512Hash_ToHexStringLower_Should_Be_Lowercase() {
        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        HmacSha512Hash hash = HmacSha512Hash.Compute(key, "test"u8);
        HexString hex = hash.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(128, hex.Value.Length);
    }

    #endregion

    #region XxHash3.ToHexStringLower

    [Fact]
    public void XxHash3_ToHexStringLower_Should_Be_Lowercase() {
        XxHash3 hash = XxHash3.Compute("test");
        HexString hex = hash.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(16, hex.Value.Length);
    }

    [Fact]
    public void XxHash3_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        XxHash3 hash = XxHash3.Compute("test");

        Assert.Equal(hash.ToHexString().ToLower().Value, hash.ToHexStringLower().Value);
    }

    #endregion

    #region Crc32Hash.ToHexStringLower

    [Fact]
    public void Crc32Hash_ToHexStringLower_Should_Be_Lowercase() {
        Crc32Hash hash = Crc32Hash.Compute("test");
        HexString hex = hash.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(8, hex.Value.Length);
    }

    [Fact]
    public void Crc32Hash_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        Crc32Hash hash = Crc32Hash.Compute("test");

        Assert.Equal(hash.ToHexString().ToLower().Value, hash.ToHexStringLower().Value);
    }

    #endregion

    #region GuidV7.ToHexStringLower

    [Fact]
    public void GuidV7_ToHexStringLower_Should_Be_Lowercase() {
        GuidV7 id = GuidV7.Create();
        HexString hex = id.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(32, hex.Value.Length);
    }

    [Fact]
    public void GuidV7_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        GuidV7 id = GuidV7.Create();

        Assert.Equal(id.ToHexString().ToLower().Value, id.ToHexStringLower().Value);
    }

    [Fact]
    public void GuidV7_ToHexStringLower_Should_RoundTrip_Via_Bytes() {
        GuidV7 id = GuidV7.Create();
        byte[] fromUpper = id.ToHexString().ToBytes();
        byte[] fromLower = id.ToHexStringLower().ToBytes();

        Assert.Equal(fromUpper, fromLower);
    }

    #endregion

    #region SnowflakeId.ToHexStringLower

    [Fact]
    public void SnowflakeId_ToHexStringLower_Should_Be_Lowercase() {
        SnowflakeId id = SnowflakeId.NewId();
        HexString hex = id.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(16, hex.Value.Length);
    }

    [Fact]
    public void SnowflakeId_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        SnowflakeId id = SnowflakeId.NewId();

        Assert.Equal(id.ToHexString().ToLower().Value, id.ToHexStringLower().Value);
    }

    #endregion

    #region AesGcmKey.ToHexStringLower

    [Fact]
    public void AesGcmKey_ToHexStringLower_Should_Be_Lowercase() {
        using AesGcmKey key = AesGcmKey.Generate256();
        HexString hex = key.ToHexStringLower();

        Assert.Equal(hex.Value, hex.Value.ToLowerInvariant());
        Assert.Equal(64, hex.Value.Length); // 32 bytes = 64 hex chars
    }

    [Fact]
    public void AesGcmKey_ToHexStringLower_Should_Match_ToHexString_ToLower() {
        using AesGcmKey key = AesGcmKey.Generate128();

        Assert.Equal(key.ToHexString().ToLower().Value, key.ToHexStringLower().Value);
    }

    #endregion
}
