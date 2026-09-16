using Wiaoj.Primitives.Cryptography.Asymmetric;

namespace Wiaoj.Primitives.Tests.Unit.Cryptography;

public sealed class Ed25519PublicKeyTests {
    private static byte[] RawKey() {
        byte[] raw = new byte[Ed25519PublicKey.KeySizeInBytes];
        for(int i = 0; i < raw.Length; i++) {
            raw[i] = (byte)(i * 7 + 1);
        }

        return raw;
    }

    [Fact]
    public void From_Bytes_StoresTheKeyAsBase64Url_AndRoundTrips() {
        byte[] raw = RawKey();

        Ed25519PublicKey key = Ed25519PublicKey.From(raw);

        Assert.Equal(Base64UrlString.FromBytes(raw), key.X);
        Assert.Equal(raw, key.ToByteArray());
        Assert.False(key.IsEmpty);
    }

    [Fact]
    public void From_Base64Url_KeepsTheValue_AndEqualsTheKeyFromBytes() {
        byte[] raw = RawKey();
        Base64UrlString x = Base64UrlString.FromBytes(raw);

        Ed25519PublicKey key = Ed25519PublicKey.From(x);

        Assert.Equal(x, key.X);
        Assert.Equal(Ed25519PublicKey.From(raw), key);
        Assert.Equal(Ed25519PublicKey.From(raw).GetHashCode(), key.GetHashCode());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void From_Bytes_Throws_UnlessExactly32Bytes(int length) {
        Assert.ThrowsAny<ArgumentException>(() => Ed25519PublicKey.From(new byte[length]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(33)]
    public void From_Base64Url_Throws_WhenItDoesNotDecodeTo32Bytes(int length) {
        Base64UrlString x = Base64UrlString.FromBytes(new byte[length]);

        Assert.ThrowsAny<ArgumentException>(() => Ed25519PublicKey.From(x));
    }

    [Fact]
    public void From_Base64Url_Throws_WhenEmpty() {
        Assert.ThrowsAny<ArgumentException>(() => Ed25519PublicKey.From(Base64UrlString.Empty));
    }

    [Fact]
    public void CopyTo_WritesTheKey_AndLeavesTheRestOfALargerDestinationUntouched() {
        byte[] raw = RawKey();
        Ed25519PublicKey key = Ed25519PublicKey.From(raw);
        byte[] destination = new byte[40];
        Array.Fill(destination, (byte)0xAA);

        key.CopyTo(destination);

        Assert.Equal(raw, destination[..32]);
        Assert.All(destination[32..], b => Assert.Equal(0xAA, b));
    }

    [Fact]
    public void CopyTo_Throws_WhenTheDestinationIsTooShort() {
        Ed25519PublicKey key = Ed25519PublicKey.From(RawKey());

        Assert.ThrowsAny<ArgumentException>(() => key.CopyTo(new byte[31]));
    }

    [Fact]
    public void CopyTo_Throws_ForAnEmptyKey() {
        Assert.Throws<InvalidOperationException>(() => default(Ed25519PublicKey).CopyTo(new byte[32]));
    }

    [Fact]
    public void TryCopyTo_ReturnsFalse_WhenTheDestinationIsTooShort_AndTrueOtherwise() {
        byte[] raw = RawKey();
        Ed25519PublicKey key = Ed25519PublicKey.From(raw);
        byte[] destination = new byte[32];

        Assert.False(key.TryCopyTo(new byte[31]));
        Assert.True(key.TryCopyTo(destination));
        Assert.Equal(raw, destination);
    }

    [Fact]
    public void CopyTo_And_FromBase64Url_DoNotAllocate() {
        Ed25519PublicKey key = Ed25519PublicKey.From(RawKey());
        Base64UrlString x = key.X;
        Span<byte> destination = stackalloc byte[32];
        key.CopyTo(destination);
        _ = Ed25519PublicKey.From(x);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for(int i = 0; i < 100; i++) {
            key.CopyTo(destination);
            _ = Ed25519PublicKey.From(x);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
