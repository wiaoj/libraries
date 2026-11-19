using Wiaoj.Primitives.Cryptography;

namespace Wiaoj.Primitives.Tests.Unit.Cryptography;

public sealed class CiphertextTests {
    [Fact]
    public void Dispose_RentedCopies_ShouldOnlyReturnBufferOnce() {
        Ciphertext original = Ciphertext.Rent(32, out Span<byte> originalDestination);
        Ciphertext copy = original;
        originalDestination.Fill(0xA5);
        original.Dispose();

        using Ciphertext next = Ciphertext.Rent(32, out Span<byte> nextDestination);
        Assert.True(originalDestination.Overlaps(nextDestination));
        Assert.Equal(-1, nextDestination.IndexOfAnyExcept((byte)0));
        nextDestination.Fill(0x5A);

        original.Dispose();
        copy.Dispose();

        Assert.Equal(-1, next.AsSpan().IndexOfAnyExcept((byte)0x5A));
        Assert.Throws<ObjectDisposedException>(() => { original.AsSpan(); });
        Assert.Throws<ObjectDisposedException>(() => { copy.AsSpan(); });
    }

    [Fact]
    public void Dispose_WrappedArray_ShouldPreserveData() {
        Ciphertext ciphertext = Ciphertext.From(new byte[] { 1, 2, 3 });

        ciphertext.Dispose();

        Assert.True(ciphertext.AsSpan().SequenceEqual(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void GetHashCode_ShouldHashContentsConsistentlyAcrossBackingStorage() {
        ReadOnlySpan<byte> bytes = "ciphertext"u8;
        using Ciphertext rented = Ciphertext.Rent(bytes.Length, out Span<byte> destination);
        bytes.CopyTo(destination);
        Ciphertext owned = Ciphertext.From(bytes);

        HashCode expected = new();
        expected.AddBytes(bytes);

        Assert.Equal(owned, rented);
        Assert.Equal(expected.ToHashCode(), owned.GetHashCode());
        Assert.Equal(owned.GetHashCode(), rented.GetHashCode());
    }
}
