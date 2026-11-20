using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;

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

        return TryDecode(value.AsSpan(), out keyBytes);
    }

    /// <summary>Decodes Base64 or Base64Url key material from a character span.</summary>
    public static bool TryDecode(ReadOnlySpan<char> value, out byte[] keyBytes) {
        ReadOnlySpan<char> text = value.Trim();
        byte[] buffer = new byte[Base64Url.GetMaxDecodedLength(text.Length)];
        try {
            if(Base64Url.DecodeFromChars(text, buffer, out _, out int written) != OperationStatus.Done &&
               !Convert.TryFromBase64Chars(text, buffer, out written)) {
                keyBytes = [];
                return false;
            }

            keyBytes = buffer.AsSpan(0, written).ToArray();
            return true;
        }
        finally {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
