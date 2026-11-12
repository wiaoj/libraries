using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Wiaoj.Webhooks.Signing;

/// <summary>
/// The <see href="https://www.standardwebhooks.com/">Standard Webhooks</see> signature: HMAC-SHA256 over
/// <c>{webhook-id}.{webhook-timestamp}.{body}</c>, sent as <c>v1,{base64}</c> in <c>webhook-signature</c>.
/// </summary>
/// <remarks>
/// <para>
/// It differs from <see cref="HmacSha256WebhookSigner"/> in what is signed and how it is written: the message id is part
/// of the signed content, so a receiver that keys its deduplication on <c>webhook-id</c> knows the id was not swapped,
/// and the signature is base64 behind a version prefix rather than <c>t=…,v1=hex</c>. The receiving libraries the
/// specification lists (Svix's among them) verify it as they are.
/// </para>
/// <para>
/// The key is the secret's bytes after <c>whsec_</c>, base64-decoded, as the specification writes a secret; a secret
/// without the prefix is used as it is. <see cref="FormatSecret"/> writes a key in that form, to be shown once.
/// </para>
/// </remarks>
public static class StandardWebhooksSignature {
    /// <summary>The prefix of a secret written as the specification writes it.</summary>
    public const string SecretPrefix = "whsec_";

    /// <summary>The prefix of a version-1 (HMAC-SHA256) signature.</summary>
    public const string Version1 = "v1";

    /// <summary>The header carrying the message id.</summary>
    public const string IdHeader = "webhook-id";

    /// <summary>The header carrying the Unix time in seconds the message was signed at.</summary>
    public const string TimestampHeader = "webhook-timestamp";

    /// <summary>The header carrying the signatures, space separated.</summary>
    public const string SignatureHeader = "webhook-signature";

    /// <summary>Writes <paramref name="key"/> as a secret a receiver can be given: <c>whsec_{base64}</c>.</summary>
    public static string FormatSecret(ReadOnlySpan<byte> key) {
        Preca.ThrowIfEmpty(key);
        return SecretPrefix + Convert.ToBase64String(key);
    }

    /// <summary>The HMAC key a secret stands for: the bytes after <c>whsec_</c> decoded, or the secret as it is.</summary>
    public static byte[] KeyOf(ReadOnlySpan<byte> secret) {
        Preca.ThrowIfEmpty(secret);
        ReadOnlySpan<byte> prefix = "whsec_"u8;
        if(!secret.StartsWith(prefix)) {
            return secret.ToArray();
        }

        ReadOnlySpan<byte> encoded = secret[prefix.Length..];
        byte[] key = new byte[Base64.GetMaxDecodedFromUtf8Length(encoded.Length)];
        OperationStatus status = Base64.DecodeFromUtf8(encoded, key, out _, out int written);
        if(status is not OperationStatus.Done || written is 0) {
            throw new FormatException("A secret written with the 'whsec_' prefix must be base64 after it.");
        }

        return key[..written];
    }

    /// <summary>Signs one message: <c>v1,{base64 of HMAC-SHA256(key, id.timestamp.body)}</c>.</summary>
    public static string Sign(string messageId, long timestamp, ReadOnlySpan<byte> body, ReadOnlySpan<byte> key) {
        Preca.ThrowIfNullOrWhiteSpace(messageId);
        Preca.ThrowIfEmpty(key);

        byte[] prefix = Encoding.UTF8.GetBytes($"{messageId}.{timestamp}.");
        byte[] content = ArrayPool<byte>.Shared.Rent(prefix.Length + body.Length);
        try {
            prefix.CopyTo(content, 0);
            body.CopyTo(content.AsSpan(prefix.Length));
            byte[] hash = HMACSHA256.HashData(key, content.AsSpan(0, prefix.Length + body.Length));
            return $"{Version1},{Convert.ToBase64String(hash)}";
        }
        finally {
            ArrayPool<byte>.Shared.Return(content, clearArray: true);
        }
    }

    /// <summary>
    /// Whether <paramref name="signatureHeader"/> carries a version-1 signature of this message under
    /// <paramref name="key"/>, and <paramref name="timestamp"/> is within <paramref name="tolerance"/> of
    /// <paramref name="now"/>. Compared in constant time; any other version in the header is ignored.
    /// </summary>
    public static bool Verify(
        string messageId, long timestamp, ReadOnlySpan<byte> body, string signatureHeader, ReadOnlySpan<byte> key,
        TimeSpan tolerance, DateTimeOffset now) {

        if(string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(signatureHeader) || key.IsEmpty) {
            return false;
        }

        if(Math.Abs(now.ToUnixTimeSeconds() - timestamp) > tolerance.TotalSeconds) {
            return false;
        }

        byte[] expected = Encoding.ASCII.GetBytes(Sign(messageId, timestamp, body, key));
        bool matched = false;
        foreach(string candidate in signatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            if(!candidate.StartsWith(Version1 + ",", StringComparison.Ordinal)) {
                continue;
            }

            matched |= CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), expected);
        }

        return matched;
    }
}
