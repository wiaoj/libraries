using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Shared DER↔PEM conversion helpers used by all asymmetric key types.
/// </summary>
/// <remarks>
/// <para>
/// <b>Public path:</b> Callers exporting public key material use <see cref="WriteToPemString"/>, which
/// returns a <see cref="PemString"/> — safe for logging, serialization, and JWKS-adjacent publication.
/// </para>
/// <para>
/// <b>Private path:</b> Callers exporting private key material use <see cref="WriteToSecret"/>, which
/// returns a <see cref="Secret{Char}"/> held in GC-immune memory. The intermediate DER buffer and the
/// managed PEM char array are both zeroed before this method returns.
/// </para>
/// <para>
/// <b>Labels:</b> All entry points accept a strongly-typed <see cref="PemLabel"/> rather than a raw
/// <see cref="string"/>, so call sites cannot introduce label typos and every asymmetric key type shares
/// the same well-known label constants (<see cref="PemLabel.PublicKey"/>, <see cref="PemLabel.PrivateKey"/>).
/// </para>
/// </remarks>
internal static class PemCodec {
    /// <summary>
    /// Encodes DER bytes as a <see cref="PemString"/> (for public key / non-secret material).
    /// </summary>
    /// <param name="label">The PEM label (e.g. <see cref="PemLabel.PublicKey"/>).</param>
    /// <param name="der">The raw DER-encoded payload.</param>
    /// <returns>A <see cref="PemString"/> instance.</returns>
    public static PemString WriteToPemString(PemLabel label, ReadOnlySpan<byte> der) {
        return PemString.Create(label, der);
    }

    /// <summary>
    /// Encodes DER bytes as PEM text held in secure unmanaged memory (for private key material).
    /// The caller must dispose the returned <see cref="Secret{Char}"/>.
    /// </summary>
    /// <param name="label">The PEM label (e.g. <see cref="PemLabel.PrivateKey"/>).</param>
    /// <param name="der">The raw DER-encoded payload. Not zeroed by this method — the caller owns that buffer's lifetime.</param>
    /// <returns>A <see cref="Secret{Char}"/> containing the PEM text.</returns>
    public static Secret<char> WriteToSecret(PemLabel label, ReadOnlySpan<byte> der) {
        char[] pem = PemEncoding.Write(label.Value, der);
        try {
            return Secret<char>.From(pem);
        }
        finally {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(pem.AsSpan()));
        }
    }

    /// <summary>
    /// Decodes a PEM character span into DER bytes, validating the expected label.
    /// </summary>
    /// <param name="pem">The PEM character span to decode.</param>
    /// <param name="expectedLabel">The label the PEM block must declare.</param>
    /// <returns>The decoded DER byte array.</returns>
    /// <exception cref="FormatException">Thrown when the PEM label does not match <paramref name="expectedLabel"/>.</exception>
    public static byte[] ReadDer(ReadOnlySpan<char> pem, PemLabel expectedLabel) {
        PemFields fields = PemEncoding.Find(pem);
        ReadOnlySpan<char> label = pem[fields.Label];

        if(!label.SequenceEqual(expectedLabel.Value)) {
            throw new FormatException($"Expected PEM label '{expectedLabel.Value}' but found '{label}'.");
        }

        return Base64String.Decode(pem[fields.Base64Data]);
    }
}