using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography;

/// <summary>
/// Represents an immutable, strongly-typed binary ciphertext payload.
/// </summary>
/// <remarks>
/// <para>
/// <b>Type Safety:</b> Replaces raw <c>byte[]</c> to eliminate primitive obsession and clearly delineate encrypted data from plaintext across API boundaries.
/// </para>
/// <para>
/// <b>Log Safety:</b> Overrides <see cref="ToString"/> to return a safe sentinel (<c>[CIPHERTEXT (N bytes)]</c>), preventing ciphertext dumps from polluting application logs.
/// </para>
/// <para>
/// <b>Memory Pooling:</b> Supports wrapping standard managed byte arrays or renting buffers from <see cref="ArrayPool{Byte}"/> via <see cref="Rent"/> to eliminate heap allocations in high-throughput pipelines.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct Ciphertext : IDisposable, IEquatable<Ciphertext> {
    private readonly byte[]? _bytes;
    private readonly int _length;
    private readonly bool _isRented;

    /// <summary>Gets the length of the ciphertext in bytes.</summary>
    public int Length => this._length;

    /// <summary>Gets a value indicating whether this ciphertext is empty or uninitialized.</summary>
    public bool IsEmpty => this._length == 0;

    /// <summary>Gets an instance representing an empty <see cref="Ciphertext"/>.</summary>
    public static Ciphertext Empty => default;

    private Ciphertext(byte[]? bytes, int length, bool isRented) {
        this._bytes = bytes;
        this._length = length;
        this._isRented = isRented;
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="Ciphertext"/> instance wrapping an existing managed byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing ciphertext.</param>
    /// <returns>A new <see cref="Ciphertext"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
    public static Ciphertext From(byte[] bytes) {
        Preca.ThrowIfNull(bytes);
        return new Ciphertext(bytes, bytes.Length, isRented: false);
    }

    /// <summary>
    /// Creates a <see cref="Ciphertext"/> instance by copying bytes from a read-only span.
    /// </summary>
    /// <param name="bytes">The byte span containing ciphertext.</param>
    /// <returns>A new <see cref="Ciphertext"/> instance, or <see cref="Empty"/> if the span is empty.</returns>
    public static Ciphertext From(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty) return default;
        return new Ciphertext(bytes.ToArray(), bytes.Length, isRented: false);
    }

    /// <summary>
    /// Creates a <see cref="Ciphertext"/> backed by a rented <see cref="ArrayPool{Byte}"/> buffer.
    /// The caller MUST dispose this instance to return the rented buffer to the pool.
    /// </summary>
    /// <param name="length">The exact required length of the ciphertext payload in bytes.</param>
    /// <param name="destination">When this method returns, contains the writable span inside the rented buffer.</param>
    /// <returns>A pooled <see cref="Ciphertext"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative or zero.</exception>
    public static Ciphertext Rent(int length, out Span<byte> destination) {
        Preca.ThrowIfNegativeOrZero(length);

        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        destination = rented.AsSpan(0, length);
        return new Ciphertext(rented, length, isRented: true);
    }

    // ── Data Access & Conversions ─────────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Byte}"/> view over the ciphertext bytes without heap allocations.
    /// </summary>
    /// <returns>A read-only span representing the ciphertext.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsSpan() {
        if(this._bytes is null || this._length == 0) return [];
        return this._bytes.AsSpan(0, this._length);
    }

    /// <summary>
    /// Encodes the ciphertext bytes into a type-safe, URL-safe <see cref="Base64UrlString"/>.
    /// </summary>
    /// <returns>A <see cref="Base64UrlString"/> representation of the ciphertext.</returns>
    public Base64UrlString ToBase64UrlString() {
        return Base64UrlString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the ciphertext bytes into a standard <see cref="Base64String"/>.
    /// </summary>
    /// <returns>A <see cref="Base64String"/> representation of the ciphertext.</returns>
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the ciphertext bytes into an uppercase <see cref="HexString"/>.
    /// </summary>
    /// <returns>An uppercase <see cref="HexString"/> representation of the ciphertext.</returns>
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the ciphertext bytes into a lowercase <see cref="HexString"/> without string allocations.
    /// </summary>
    /// <returns>A lowercase <see cref="HexString"/> representation of the ciphertext.</returns>
    public HexString ToHexStringLower() {
        return HexString.FromBytesLower(AsSpan());
    }

    /// <summary>
    /// Copies the ciphertext bytes into the specified destination span.
    /// </summary>
    /// <param name="destination">The destination span. Must be at least <see cref="Length"/> bytes long.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <see cref="Length"/>.</exception>
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < this._length) {
            throw new ArgumentException($"Destination span must be at least {this._length} bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Attempts to copy the ciphertext bytes into the specified destination span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <returns><see langword="true"/> if the copy succeeded; otherwise, <see langword="false"/> if destination is too short.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < this._length) return false;
        AsSpan().CopyTo(destination);
        return true;
    }

    // ── Equality & Display ────────────────────────────────────────────────────

    /// <summary>
    /// Determines whether this <see cref="Ciphertext"/> is equal to another <see cref="Ciphertext"/> by comparing byte content.
    /// </summary>
    /// <param name="other">The other ciphertext to compare with.</param>
    /// <returns><see langword="true"/> if both instances have identical byte sequences; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Ciphertext other) {
        return AsSpan().SequenceEqual(other.AsSpan());
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is Ciphertext other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this.IsEmpty ? 0 : this._length;
    }

    /// <summary>Determines whether two <see cref="Ciphertext"/> instances are equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Ciphertext left, Ciphertext right) {
        return left.Equals(right);
    }

    /// <summary>Determines whether two <see cref="Ciphertext"/> instances are not equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Ciphertext left, Ciphertext right) {
        return !left.Equals(right);
    }

    /// <summary>
    /// Implicitly converts a <see cref="Ciphertext"/> instance to a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(Ciphertext ciphertext) {
        return ciphertext.AsSpan();
    }

    /// <summary>
    /// Returns a log-safe sentinel string. The underlying ciphertext is never dumped.
    /// </summary>
    /// <returns><c>"[CIPHERTEXT (N bytes)]"</c></returns>
    public override string ToString() {
        return $"[CIPHERTEXT ({this._length} bytes)]";
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Securely clears and returns the rented buffer to the shared <see cref="ArrayPool{Byte}"/> if applicable.
    /// Safe to call multiple times.
    /// </summary>
    public void Dispose() {
        if(this._isRented && this._bytes is not null) {
            CryptographicOperations.ZeroMemory(this._bytes.AsSpan(0, this._length));
            ArrayPool<byte>.Shared.Return(this._bytes);
        }
    }
}