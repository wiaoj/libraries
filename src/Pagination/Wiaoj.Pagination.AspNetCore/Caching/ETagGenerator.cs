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
    private const char WeakIndicatorChar = 'W';
    private const char SlashChar = '/';
    private const char QuoteChar = '"';
    private const char WildcardChar = '*';
    private const string WeakPrefix = "W/\"";
    private const string LowercaseHexFormat = "x";

    private const int XxHash3HexLength = XxHash3.SizeInBytes * 2; // 16 hex characters

    // W/" + 16 hex chars + " = 20
    private const int WeakETagPrefixLength = 3;
    private const int WeakETagTotalLength = WeakETagPrefixLength + XxHash3HexLength + 1;

    /// <summary>
    /// Generates an RFC 9110 compliant weak <c>ETag</c> (<c>W/"{xxhash64_hex}"</c>) from a UTF-8 byte payload using <see cref="XxHash3"/>.
    /// </summary>
    /// <param name="utf8Payload">The UTF-8 encoded response body or metadata span.</param>
    /// <returns>A formatted weak ETag string (e.g. <c>W/"3fa85f64ac28d019"</c>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GenerateWeakETag(ReadOnlySpan<byte> utf8Payload) {
        return FormatWeakETag(XxHash3.Compute(utf8Payload));
    }

    /// <summary>
    /// Formats a precomputed <see cref="XxHash3"/> hash into an RFC 9110 compliant weak <c>ETag</c> string
    /// (<c>W/"{xxhash64_hex}"</c>). Use this overload when the hash has already been computed elsewhere
    /// (e.g. via streaming serialization) to avoid hashing the payload twice.
    /// </summary>
    /// <param name="hash">A previously computed <see cref="XxHash3"/> hash.</param>
    /// <returns>A formatted weak ETag string (e.g. <c>W/"3fa85f64ac28d019"</c>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatWeakETag(XxHash3 hash) {
        return string.Create(CultureInfo.InvariantCulture, stackalloc char[WeakETagTotalLength], $"{WeakPrefix}{hash:x}{QuoteChar}");
    }

    /// <summary>
    /// Tries to format an RFC 9110 compliant weak <c>ETag</c> directly into a destination character span without heap allocations.
    /// </summary>
    /// <param name="utf8Payload">The UTF-8 encoded response body span.</param>
    /// <param name="destination">The destination character buffer (minimum <see cref="WeakETagTotalLength"/> characters required).</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGenerateWeakETag(ReadOnlySpan<byte> utf8Payload, Span<char> destination, out int charsWritten) {
        return TryFormatWeakETag(XxHash3.Compute(utf8Payload), destination, out charsWritten);
    }

    /// <summary>
    /// Tries to format a precomputed <see cref="XxHash3"/> hash directly into a destination character span
    /// without heap allocations. Use this overload when the hash has already been computed elsewhere
    /// (e.g. via streaming serialization) to avoid hashing the payload twice.
    /// </summary>
    /// <param name="hash">A previously computed <see cref="XxHash3"/> hash.</param>
    /// <param name="destination">The destination character buffer (minimum <see cref="WeakETagTotalLength"/> characters required).</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatWeakETag(XxHash3 hash, Span<char> destination, out int charsWritten) {
        if(destination.Length < WeakETagTotalLength) {
            charsWritten = 0;
            return false;
        }

        destination[0] = WeakIndicatorChar;
        destination[1] = SlashChar;
        destination[2] = QuoteChar;

        if(!hash.TryFormat(destination.Slice(WeakETagPrefixLength, XxHash3HexLength), out int hexWritten, LowercaseHexFormat)
            || hexWritten != XxHash3HexLength) {
            charsWritten = 0;
            return false;
        }

        destination[WeakETagTotalLength - 1] = QuoteChar;
        charsWritten = WeakETagTotalLength;
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
        return string.Create(CultureInfo.InvariantCulture, stackalloc char[66], $"{QuoteChar}{hash.ToHexStringLower()}{QuoteChar}");
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
        if(headerSpan.Length == 1 && headerSpan[0] == WildcardChar) {
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