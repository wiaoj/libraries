using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers;

/// <summary>
/// Writes an identifier as <c>prefix_</c>, a key version character and 22 base62 characters of its value encrypted with
/// AES — revealing nothing about the value, and refusing text that was not written under the same key and prefix.
/// </summary>
/// <remarks>
/// <para>
/// The value is encrypted as one 128-bit AES block: eight bytes of an HMAC-SHA256 tag of the prefix, then the value.
/// Decryption recomputes the tag and compares it in constant time, so:
/// </para>
/// <list type="bullet">
/// <item><description>a made-up or altered identifier is refused, except with a probability of 2⁻⁶⁴;</description></item>
/// <item><description>a <c>usr_</c> identifier cannot be passed off as an <c>org_</c> one by changing its prefix;</description></item>
/// <item><description>text written under another key or version is refused rather than read as some other value.</description></item>
/// </list>
/// <para>
/// The same value always gives the same text (the encryption is deterministic), which is what an identifier needs. The
/// AES and HMAC keys are derived from the configured key with HKDF-SHA256, so the configured key is never used directly.
/// </para>
/// <para>
/// <b>Changing the key</b> changes every identifier's text. The version character lets a later key be introduced while
/// identifiers written under the earlier one are still read; this codec reads its own version only.
/// </para>
/// </remarks>
public sealed class AesIdCodec : IdCodec {
    /// <summary>The shortest key accepted: 128 bits.</summary>
    public const int MinimumKeyLength = 16;

    private const int PayloadLength = 1 + Base62.UInt128Length;
    private const int TagLength = 8;
    private const int BlockLength = 16;

    private static readonly byte[] AesKeyInfo = "wiaoj.identifiers.aes.v1"u8.ToArray();
    private static readonly byte[] TagKeyInfo = "wiaoj.identifiers.tag.v1"u8.ToArray();

    private readonly byte[] _aesKey;
    private readonly byte[] _tagKey;
    private readonly ConcurrentBag<Aes> _ciphers = [];
    private readonly ConcurrentDictionary<string, byte[]> _tags = new(StringComparer.Ordinal);

    /// <summary>Creates the codec.</summary>
    /// <param name="key">The secret key, at least <see cref="MinimumKeyLength"/> bytes of random data.</param>
    /// <param name="version">
    /// The key's version, written into every identifier: an ASCII letter or digit. Defaults to <c>'1'</c>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is shorter than <see cref="MinimumKeyLength"/> bytes.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not an ASCII letter or digit.</exception>
    public AesIdCodec(ReadOnlySpan<byte> key, char version = '1') {
        if(key.Length < MinimumKeyLength) {
            throw new ArgumentException($"The key must be at least {MinimumKeyLength} bytes of random data.", nameof(key));
        }

        if(!char.IsAsciiLetterOrDigit(version)) {
            throw new ArgumentOutOfRangeException(nameof(version), version, "The version must be an ASCII letter or digit.");
        }

        this.Version = version;
        this._aesKey = new byte[BlockLength];
        this._tagKey = new byte[32];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, key, this._aesKey, salt: [], AesKeyInfo);
        HKDF.DeriveKey(HashAlgorithmName.SHA256, key, this._tagKey, salt: [], TagKeyInfo);
    }

    /// <summary>Gets the key version character written into every identifier.</summary>
    public char Version { get; }

    /// <inheritdoc/>
    /// <remarks>Equivalent when the version and the derived keys are equal, compared in constant time.</remarks>
    public override bool IsEquivalentTo(IdCodec other) {
        return other is AesIdCodec aes
               && aes.Version == this.Version
               && CryptographicOperations.FixedTimeEquals(aes._aesKey, this._aesKey)
               && CryptographicOperations.FixedTimeEquals(aes._tagKey, this._tagKey);
    }

    /// <inheritdoc/>
    public override int GetMaxEncodedLength(string prefix) {
        Preca.ThrowIfNull(prefix);
        return prefix.Length + 1 + PayloadLength;
    }

    /// <inheritdoc/>
    public override bool TryEncode(string prefix, SnowflakeId value, Span<char> destination, out int charsWritten) {
        Preca.ThrowIfNull(prefix);

        if(!TryWritePrefix(prefix, destination, out Span<char> payload) || payload.Length < PayloadLength) {
            charsWritten = 0;
            return false;
        }

        Span<byte> block = stackalloc byte[BlockLength];
        this.TagFor(prefix).CopyTo(block);
        BinaryPrimitives.WriteInt64BigEndian(block[TagLength..], value.Value);

        Span<byte> encrypted = stackalloc byte[BlockLength];
        this.Transform(block, encrypted, encrypt: true);

        payload[0] = this.Version;
        Base62.WriteUInt128(BinaryPrimitives.ReadUInt128BigEndian(encrypted), payload[1..]);

        charsWritten = prefix.Length + 1 + PayloadLength;
        return true;
    }

    /// <inheritdoc/>
    public override bool TryDecode(string prefix, ReadOnlySpan<char> text, out SnowflakeId value) {
        Preca.ThrowIfNull(prefix);
        value = default;

        ReadOnlySpan<char> payload = PayloadAfterPrefix(prefix, text, out bool matched);
        if(!matched || payload.Length != PayloadLength || payload[0] != this.Version
           || !Base62.TryReadUInt128(payload[1..], out UInt128 cipherValue)) {
            return false;
        }

        Span<byte> encrypted = stackalloc byte[BlockLength];
        BinaryPrimitives.WriteUInt128BigEndian(encrypted, cipherValue);

        Span<byte> block = stackalloc byte[BlockLength];
        this.Transform(encrypted, block, encrypt: false);

        if(!CryptographicOperations.FixedTimeEquals(block[..TagLength], this.TagFor(prefix))) {
            return false;
        }

        value = new SnowflakeId(BinaryPrimitives.ReadInt64BigEndian(block[TagLength..]));
        return true;
    }

    /// <summary>The first eight bytes of HMAC-SHA256(tag key, prefix), computed once per prefix.</summary>
    private byte[] TagFor(string prefix) {
        return this._tags.GetOrAdd(prefix, static (name, tagKey) => {
            Span<byte> mac = stackalloc byte[32];
            HMACSHA256.HashData(tagKey, Encoding.UTF8.GetBytes(name), mac);
            return mac[..TagLength].ToArray();
        }, this._tagKey);
    }

    /// <summary>
    /// Encrypts or decrypts exactly one block. Each operation rents its own <see cref="Aes"/> instance: the instances are
    /// not documented as safe to share between threads.
    /// </summary>
    private void Transform(ReadOnlySpan<byte> input, Span<byte> output, bool encrypt) {
        if(!this._ciphers.TryTake(out Aes? aes)) {
            aes = Aes.Create();
            aes.Key = this._aesKey;
        }

        try {
            int written = encrypt
                ? aes.EncryptEcb(input, output, PaddingMode.None)
                : aes.DecryptEcb(input, output, PaddingMode.None);

            if(written != BlockLength) {
                throw new CryptographicException("AES did not transform exactly one block.");
            }
        }
        finally {
            this._ciphers.Add(aes);
        }
    }
}
