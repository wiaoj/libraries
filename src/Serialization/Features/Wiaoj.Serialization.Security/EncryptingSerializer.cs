using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives;
using Wiaoj.Security;

namespace Wiaoj.Serialization.Security;

/// <summary>
/// Encrypts what an inner serializer writes with the <see cref="ISecretProtector{TContext}"/> of
/// <typeparamref name="TContext"/>, so the data follows that context's key ring and its rotation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bytes</b> (<c>byte[]</c>, buffer writer, sequence, stream) are the key version as a 4-byte big-endian integer
/// followed by the ciphertext of the inner serializer's bytes.
/// </para>
/// <para>
/// <b>Text</b> is the <see cref="EncryptedSecret{TContext}"/> compact string (<c>v{version}.{blob}</c>) of the inner
/// serializer's text, so every string overload reads what every other string overload writes.
/// </para>
/// <para>
/// Data written under an older key stays readable for as long as the key ring holds that key. Anything that cannot be
/// decrypted — tampered, truncated, written under another context or a key the ring does not have — throws
/// <see cref="DecryptionFailedException"/>.
/// </para>
/// </remarks>
internal sealed class EncryptingSerializer<TKey, TContext>(ISerializer<TKey> inner, ISecretProtector<TContext> protector)
    : ISerializer<TKey>
    where TKey : ISerializerKey
    where TContext : ISecretContext {

    private const int VersionLength = sizeof(int);

    // --- Bytes ---

    public byte[] Serialize<TValue>(TValue value) => this.Encrypt(inner.Serialize(value));

    public void Serialize<TValue>(IBufferWriter<byte> writer, TValue value) {
        Preca.ThrowIfNull(writer);
        writer.Write(this.Serialize(value));
    }

    public TValue? Deserialize<TValue>(byte[] data) {
        Preca.ThrowIfNull(data);
        return inner.Deserialize<TValue>(this.Decrypt(data));
    }

    public object? Deserialize(byte[] data, Type type) {
        Preca.ThrowIfNull(data);
        Preca.ThrowIfNull(type);
        return inner.Deserialize(this.Decrypt(data), type);
    }

    public TValue? Deserialize<TValue>(in ReadOnlySequence<byte> sequence) => this.Deserialize<TValue>(sequence.ToArray());

    public object? Deserialize(in ReadOnlySequence<byte> sequence, Type type) => this.Deserialize(sequence.ToArray(), type);

    // --- Text ---

    public string SerializeToString<TValue>(TValue value) => this.ProtectText(inner.SerializeToString(value));

    public string SerializeToString<TValue>(TValue value, Type type) => this.ProtectText(inner.SerializeToString(value, type));

    public TValue? DeserializeFromString<TValue>(string data) => inner.DeserializeFromString<TValue>(this.UnprotectText(data));

    public object? DeserializeFromString(string data, Type type) {
        Preca.ThrowIfNull(type);
        return inner.DeserializeFromString(this.UnprotectText(data), type);
    }

    // --- Try ---

    public bool TryDeserialize<TValue>(byte[] data, out TValue? result) =>
        TryRead(() => this.Deserialize<TValue>(data), out result);

    public bool TryDeserialize<TValue>(in ReadOnlySequence<byte> sequence, out TValue? result) {
        byte[] data = sequence.ToArray();
        return TryRead(() => this.Deserialize<TValue>(data), out result);
    }

    public bool TryDeserializeFromString<TValue>(string data, out TValue? result) =>
        TryRead(() => this.DeserializeFromString<TValue>(data), out result);

    private static bool TryRead<TValue>(Func<TValue?> read, out TValue? result) {
        try {
            result = read();
            return true;
        }
        catch(WiaojSerializationException) {
            result = default;
            return false;
        }
    }

    // --- Streams (the bytes format) ---

    public async Task SerializeAsync<TValue>(Stream stream, TValue value, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(stream);
        await stream.WriteAsync(this.Serialize(value), cancellationToken).ConfigureAwait(false);
    }

    public async Task SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(stream);
        Preca.ThrowIfNull(type);

        using MemoryStream plain = new();
        await inner.SerializeAsync(plain, value, type, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(this.Encrypt(plain.ToArray()), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TValue?> DeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) {
        byte[] encrypted = await ReadAllAsync(stream, cancellationToken).ConfigureAwait(false);
        return this.Deserialize<TValue>(encrypted);
    }

    public async ValueTask<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(type);

        byte[] encrypted = await ReadAllAsync(stream, cancellationToken).ConfigureAwait(false);
        using MemoryStream plain = new(this.Decrypt(encrypted), writable: false);
        return await inner.DeserializeAsync(plain, type, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<(bool Success, TValue? Value)> TryDeserializeAsync<TValue>(Stream stream, CancellationToken cancellationToken = default) {
        try {
            return (true, await this.DeserializeAsync<TValue>(stream, cancellationToken).ConfigureAwait(false));
        }
        catch(WiaojSerializationException) {
            return (false, default);
        }
    }

    // --- Envelope ---

    private byte[] Encrypt(ReadOnlySpan<byte> plain) {
        EncryptedSecret<TContext> sealedValue = protector.Protect(plain);
        byte[] cipher = Base64UrlString.Parse(sealedValue.Blob.RawBase64Url).ToBytes();

        byte[] envelope = new byte[VersionLength + cipher.Length];
        BinaryPrimitives.WriteInt32BigEndian(envelope, sealedValue.KeyVersion.Value);
        cipher.CopyTo(envelope, VersionLength);
        return envelope;
    }

    private byte[] Decrypt(byte[] envelope) {
        try {
            if(envelope.Length <= VersionLength) {
                throw new ArgumentException("The data is shorter than an encrypted envelope.", nameof(envelope));
            }

            KeyVersion version = KeyVersion.Of(BinaryPrimitives.ReadInt32BigEndian(envelope));
            CipherBlob blob = CipherBlob.From(Base64UrlString.FromBytes(envelope.AsSpan(VersionLength)));
            return this.Unprotect(EncryptedSecret<TContext>.Create(blob, version));
        }
        catch(Exception ex) when(IsDecryptionFailure(ex)) {
            throw Failed(ex);
        }
    }

    private string ProtectText(string plainText) => protector.Protect(plainText).ToCompactString();

    private string UnprotectText(string data) {
        Preca.ThrowIfNull(data);
        try {
            return Encoding.UTF8.GetString(this.Unprotect(EncryptedSecret<TContext>.Parse(data)));
        }
        catch(Exception ex) when(IsDecryptionFailure(ex)) {
            throw Failed(ex);
        }
    }

    private byte[] Unprotect(in EncryptedSecret<TContext> encrypted) {
        using Secret<byte> plain = protector.Unprotect(encrypted);
        return plain.Expose(static span => span.ToArray());
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(stream);

        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static bool IsDecryptionFailure(Exception ex) =>
        ex is CryptographicException or KeyNotFoundException or ArgumentException or FormatException;

    private static DecryptionFailedException Failed(Exception ex) => new(
        $"The data could not be decrypted for {typeof(TContext).Name}: it is corrupt, was tampered with, or was written " +
        "under a key this protector does not hold.", ex);
}
