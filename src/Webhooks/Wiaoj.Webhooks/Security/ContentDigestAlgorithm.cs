namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Specifies the cryptographic or non-cryptographic digest algorithm used to compute payload integrity hashes.
/// </summary>
public enum ContentDigestAlgorithm {
    /// <summary>No payload digest header is generated.</summary>
    None = 0,

    /// <summary>SIMD-accelerated 128-bit XXHash3 digest (Fastest, collision-resistant).</summary>
    XxHash128 = 1,

    /// <summary>Standard 64-bit XXHash3 digest.</summary>
    XxHash3 = 2,

    /// <summary>Standard SHA-256 digest (RFC 9530 compliant).</summary>
    Sha256 = 3,

    /// <summary>Standard SHA-512 digest (RFC 9530 compliant).</summary>
    Sha512 = 4,

    /// <summary>IEEE 802.3 CRC32 integrity checksum.</summary>
    Crc32 = 5
}