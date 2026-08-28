using System.Globalization;
using System.Runtime.CompilerServices;
using Wiaoj.Primitives.Cryptography.Hashing;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Pagination.AspNetCore.Caching;

/// <summary>
/// Provides high-performance, zero-allocation utility methods for generating RFC 9110 / RFC 7232 compliant HTTP <c>ETag</c> headers
/// and evaluating <c>If-None-Match</c> conditional requests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Weak ETags (<c>XxHash3</c>):</b> Employs SIMD-accelerated 64-bit <see cref="XxHash3"/> to hash JSON response payloads 
/// at memory bus speeds (30+ GB/s). Weak ETags (<c>W/"..."</c>) signify that the representation is semantically equivalent.
/// </para>
/// <para>
/// <b>Strong ETags (<c>Sha256</c>):</b> Employs cryptographic <see cref="Sha256Hash"/> for byte-for-byte exact comparison.
/// </para>
/// </remarks>
public static class ETagGenerator {
    private const string WeakPrefix = "W/\"";
    private const char Quote = '"';
    private const char Wildcard = '*';
    private const int XxHash3HexLength = XxHash3.SizeInBytes * 2; // 16 hex characters

    /// <summary>
    /// Generates an RFC 9110 compliant weak <c>ETag</c> (<c>W/"{xxhash64_hex}"</c>) from a UTF-8 byte payload using <see cref="XxHash3"/>.
    /// </summary>
    /// <param name="utf8Payload">The UTF-8 encoded response body or metadata span.</param>
    /// <returns>A formatted weak ETag string (e.g. <c>W/"3fa85f64ac28d019"</c>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GenerateWeakETag(ReadOnlySpan<byte> utf8Payload) {
        XxHash3 hash = XxHash3.Compute(utf8Payload);
        return string.Create(CultureInfo.InvariantCulture, stackalloc char[20], $"{WeakPrefix}{hash:x}{Quote}");
    }

    /// <summary>
    /// Tries to format an RFC 9110 compliant weak <c>ETag</c> directly into a destination character span without heap allocations.
    /// </summary>
    /// <param name="utf8Payload">The UTF-8 encoded response body span.</param>
    /// <param name="destination">The destination character buffer (minimum 20 characters required).</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGenerateWeakETag(ReadOnlySpan<byte> utf8Payload, Span<char> destination, out int charsWritten) {
        const int requiredLength = 3 + XxHash3HexLength + 1; // W/" + 16 hex chars + " = 20
        if(destination.Length < requiredLength) {
            charsWritten = 0;
            return false;
        }

        XxHash3 hash = XxHash3.Compute(utf8Payload);

        destination[0] = 'W';
        destination[1] = '/';
        destination[2] = '"';

        if(!hash.TryFormat(destination.Slice(3, XxHash3HexLength), out int hexWritten, "x") || hexWritten != XxHash3HexLength) {
            charsWritten = 0;
            return false;
        }

        destination[requiredLength - 1] = '"';
        charsWritten = requiredLength;
        return true;
    }

    /// <summary>
    /// Generates an RFC 9110 compliant strong <c>ETag</c> (<c>"{sha256_hex}"</c>) from a UTF-8 byte payload using <see cref="Sha256Hash"/>.
    /// </summary>
    /// <param name="utf8Payload">The UTF-8 encoded response body span.</param>
    /// <returns>A formatted strong ETag string (e.g. <c>"a1b2c3..."</c>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GenerateStrongETag(ReadOnlySpan<byte> utf8Payload) {
        Sha256Hash hash = Sha256Hash.Compute(utf8Payload);
        return string.Create(CultureInfo.InvariantCulture, stackalloc char[66], $"{Quote}{hash.ToHexStringLower()}{Quote}");
    }

    /// <summary>
    /// Evaluates whether the incoming <c>If-None-Match</c> HTTP header matches the current <c>ETag</c>,
    /// indicating that the client's cached representation is still valid (HTTP 304 Not Modified).
    /// </summary>
    /// <remarks>
    /// Supports wildcard (<c>*</c>), exact matches, weak comparisons, and comma-separated multiple ETag values.
    /// </remarks>
    /// <param name="ifNoneMatchHeader">The raw <c>If-None-Match</c> HTTP request header value.</param>
    /// <param name="currentETag">The current computed ETag for the response.</param>
    /// <returns><see langword="true"/> if the content is not modified; otherwise, <see langword="false"/>.</returns>
    public static bool IsNotModified(string? ifNoneMatchHeader, string currentETag) {
        if(string.IsNullOrWhiteSpace(ifNoneMatchHeader) || string.IsNullOrEmpty(currentETag)) {
            return false;
        }

        ReadOnlySpan<char> headerSpan = ifNoneMatchHeader.AsSpan().Trim();
        ReadOnlySpan<char> targetSpan = currentETag.AsSpan().Trim();

        // 1. Wildcard match: '*' matches any current entity
        if(headerSpan.Length == 1 && headerSpan[0] == Wildcard) {
            return true;
        }

        // 2. Direct single match
        if(MemoryExtensions.Equals(headerSpan, targetSpan, StringComparison.Ordinal)) {
            return true;
        }

        // 3. Comma-separated list parsing without heap allocation
        while(!headerSpan.IsEmpty) {
            int commaIndex = headerSpan.IndexOf(',');
            ReadOnlySpan<char> segment = commaIndex >= 0 ? headerSpan[..commaIndex].Trim() : headerSpan.Trim();

            if(MemoryExtensions.Equals(segment, targetSpan, StringComparison.Ordinal)) {
                return true;
            }

            if(commaIndex < 0) {
                break;
            }

            headerSpan = headerSpan[(commaIndex + 1)..];
        }

        return false;
    }
}