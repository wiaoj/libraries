using System.Buffers.Text;

namespace Wiaoj.Security;

/// <summary>
/// Reads a master key written as Base64 or as Base64Url.
/// Public so that a provider of one's own — a KMS, a file, a secret store — reads a key the same
/// way the ones here do.
/// </summary>
/// <remarks>
/// <para>
/// Both, because both turn up and the difference is invisible until something fails. The tool
/// everyone reaches for — <c>openssl rand -base64 32</c> — produces standard Base64, and the
/// library's own wire format is Base64Url; a provider that took one and not the other rejected a
/// key generated exactly as instructed.
/// </para>
/// <para>
/// They were also not the same between providers: configuration took Base64 and the environment
/// took Base64Url, so one key stopped working when it moved from one to the other. Reading both
/// here is what makes a master key a master key wherever it is kept.
/// </para>
/// <para>
/// This is input only. What the library <em>writes</em> — a wrapped key ring — stays Base64Url, so
/// it is safe in a URL, a header and a JSON document without escaping.
/// </para>
/// </remarks>
public static class MasterKeyEncoding {
    /// <summary>
    /// Decodes a key, or returns <see langword="false"/> when the text is neither encoding.
    /// </summary>
    public static bool TryDecode(string value, out byte[] keyBytes) {
        ArgumentNullException.ThrowIfNull(value);

        string trimmed = value.Trim();

        // Base64Url first: it is what this library writes, so it is the more likely of the two here.
        if (TryDecodeBase64Url(trimmed, out keyBytes)) {
            return true;
        }

        return TryDecodeBase64(trimmed, out keyBytes);
    }

    private static bool TryDecodeBase64Url(string value, out byte[] keyBytes) {
        byte[] buffer = new byte[Base64Url.GetMaxDecodedLength(value.Length)];

        if (Base64Url.DecodeFromChars(value, buffer, out _, out int written, isFinalBlock: true)
            is System.Buffers.OperationStatus.Done) {
            keyBytes = buffer[..written];
            return true;
        }

        keyBytes = [];
        return false;
    }

    private static bool TryDecodeBase64(string value, out byte[] keyBytes) {
        byte[] buffer = new byte[((value.Length + 3) / 4) * 3];

        if (Convert.TryFromBase64String(value, buffer, out int written)) {
            keyBytes = buffer[..written];
            return true;
        }

        keyBytes = [];
        return false;
    }
}
