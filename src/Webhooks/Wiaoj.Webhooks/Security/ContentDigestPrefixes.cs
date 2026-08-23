namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Standard algorithm format prefixes and delimiters for RFC 9530 and custom webhook digest headers.
/// </summary>
public static class ContentDigestPrefixes {
    /// <summary>Prefix for 128-bit XXHash3 digest (<c>"xxh128="</c>).</summary>
    public const string XxHash128 = "xxh128=";

    /// <summary>Prefix for 64-bit XXHash3 digest (<c>"xxh3="</c>).</summary>
    public const string XxHash3 = "xxh3=";

    /// <summary>RFC 9530 prefix for SHA-256 structured field byte sequence (<c>"sha-256=:"</c>).</summary>
    public const string Sha256Prefix = "sha-256=:";

    /// <summary>RFC 9530 prefix for SHA-512 structured field byte sequence (<c>"sha-512=:"</c>).</summary>
    public const string Sha512Prefix = "sha-512=:";

    /// <summary>RFC 9530 suffix for structured field byte sequences (<c>":"</c>).</summary>
    public const string StructuredFieldSuffix = ":";

    /// <summary>Prefix for IEEE 802.3 CRC32 checksum (<c>"crc32="</c>).</summary>
    public const string Crc32 = "crc32=";
}