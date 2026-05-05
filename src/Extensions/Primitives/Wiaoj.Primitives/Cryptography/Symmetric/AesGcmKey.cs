using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Primitives.Cryptography.Symmetric;

/// <summary>
/// Represents the valid AES key sizes in bytes.
/// </summary>
public enum AesGcmKeySize {
    /// <summary>128-bit AES key (16 bytes).</summary>
    Aes128 = 16,
    /// <summary>192-bit AES key (24 bytes).</summary>
    Aes192 = 24,
    /// <summary>256-bit AES key (32 bytes).</summary>
    Aes256 = 32,
}

/// <summary>
/// Represents a validated, lifecycle-managed AES-GCM symmetric key.
/// Provides type-safe encryption and decryption operations with secure memory handling.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never exposes key material.</b> Key bytes are held in a <see cref="Secret{T}"/>
/// and only accessible via the <c>Expose</c> callback pattern.
/// </para>
/// <para>
/// <b>Cipher packet layout (output of <see cref="Encrypt(ReadOnlySpan{byte})"/>):</b>
/// <code>
/// | nonce (12 B) | auth tag (16 B) | ciphertext (N B) |
/// </code>
/// </para>
/// <para>
/// <b>Key sizes:</b> AES-128 (16 B), AES-192 (24 B), AES-256 (32 B).
/// </para>
/// <para>
/// <b>Thread safety:</b> Instances are not thread-safe. Do not share across threads
/// without external synchronization.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public sealed class AesGcmKey : IDisposable {

    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Size of the AES-GCM nonce in bytes.</summary>
    public const int NonceSizeBytes = 12;

    /// <summary>Size of the AES-GCM authentication tag in bytes.</summary>
    public const int TagSizeBytes = 16;

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly Secret<byte> _key;
    private readonly DisposeState _disposeState = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    private AesGcmKey(Secret<byte> key) {
        this._key = key;
    }

    // ── Key size ──────────────────────────────────────────────────────────────

    /// <summary>The size of this key.</summary>
    public AesGcmKeySize KeySize {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));
            return (AesGcmKeySize)this._key.Expose(static span => span.Length);
        }
    }

    /// <summary>The size of this key in bits (128, 192, or 256).</summary>
    public int KeySizeInBits => (int)this.KeySize * 8;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="AesGcmKey"/> from an existing <see cref="Secret{Byte}"/>.
    /// The key takes ownership of the secret and will dispose it on <see cref="Dispose"/>.
    /// </summary>
    /// <param name="key">The key material. Must be 16, 24, or 32 bytes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the key length is not 16, 24, or 32 bytes.</exception>
    public static AesGcmKey From(Secret<byte> key) {
        Preca.ThrowIfNull(key);
        key.Expose(static span => ValidateKeySize(span.Length));
        return new(key);
    }

    /// <summary>
    /// Creates an <see cref="AesGcmKey"/> from a raw byte span.
    /// The bytes are copied into secure unmanaged memory.
    /// </summary>
    /// <param name="keyBytes">The key material. Must be 16, 24, or 32 bytes.</param>
    /// <exception cref="ArgumentException">Thrown when the span length is not 16, 24, or 32 bytes.</exception>
    public static AesGcmKey From(ReadOnlySpan<byte> keyBytes) {
        ValidateKeySize(keyBytes.Length);
        return new(Secret<byte>.From(keyBytes));
    }

    /// <summary>
    /// Creates an <see cref="AesGcmKey"/> from a hex-encoded key string.
    /// </summary>
    /// <param name="hex">The hex-encoded key. Must decode to 16, 24, or 32 bytes.</param>
    /// <exception cref="FormatException">Thrown when the decoded length is not a valid AES key size.</exception>
    public static AesGcmKey From(HexString hex) {
        int decodedLen = hex.GetDecodedLength();
        ValidateKeySize(decodedLen);

        using ValueBuffer<byte> buffer = ValueBuffer.Create32(stackalloc byte[decodedLen]);

        try {
            if(!hex.TryDecode(buffer, out int written) || written != decodedLen)
                throw new FormatException("Failed to decode hex string into key bytes.");

            return new(Secret<byte>.From(buffer[..written]));
        }
        finally {
            CryptographicOperations.ZeroMemory(buffer[..decodedLen]);
        }
    }

    /// <summary>
    /// Creates an <see cref="AesGcmKey"/> from a Base64-encoded key string.
    /// </summary>
    /// <param name="base64">The Base64-encoded key. Must decode to 16, 24, or 32 bytes.</param>
    /// <exception cref="FormatException">Thrown when the decoded length is not a valid AES key size.</exception>
    public static AesGcmKey From(Base64String base64) {
        int decodedLen = base64.GetDecodedLength();
        ValidateKeySize(decodedLen);

        using ValueBuffer<byte> buffer = ValueBuffer.Create32(stackalloc byte[decodedLen]);

        try {
            if(!base64.TryDecode(buffer, out int written) || written != decodedLen)
                throw new FormatException("Failed to decode Base64 string into key bytes.");

            return new(Secret<byte>.From(buffer[..written]));
        }
        finally {
            CryptographicOperations.ZeroMemory(buffer[..decodedLen]);
        }
    }

    /// <summary>
    /// Creates an <see cref="AesGcmKey"/> from a Base64Url-encoded key string.
    /// </summary>
    /// <param name="base64Url">The Base64Url-encoded key. Must decode to 16, 24, or 32 bytes.</param>
    /// <exception cref="FormatException">Thrown when the decoded length is not a valid AES key size.</exception>
    public static AesGcmKey From(Base64UrlString base64Url) {
        int decodedLen = base64Url.GetDecodedLength();
        ValidateKeySize(decodedLen);

        using ValueBuffer<byte> buffer = ValueBuffer.Create32(stackalloc byte[decodedLen]);

        try {
            if(!base64Url.TryDecode(buffer, out int written) || written != decodedLen)
                throw new FormatException("Failed to decode Base64Url string into key bytes.");

            return new(Secret<byte>.From(buffer[..written]));
        }
        finally {
            CryptographicOperations.ZeroMemory(buffer[..decodedLen]);
        }
    }

    /// <summary>
    /// Creates an <see cref="AesGcmKey"/> from a Base32-encoded key string.
    /// </summary>
    /// <param name="base32">The Base32-encoded key. Must decode to 16, 24, or 32 bytes.</param>
    /// <exception cref="FormatException">Thrown when the decoded length is not a valid AES key size.</exception>
    public static AesGcmKey From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[32]; // max AES key size
        if(!base32.TryDecode(buffer, out int written))
            throw new FormatException("Failed to decode Base32 string into key bytes.");

        ValidateKeySize(written);
        try {
            return new(Secret<byte>.From(buffer[..written]));
        }
        finally {
            CryptographicOperations.ZeroMemory(buffer[..written]);
        }
    }

    // ── TryFrom ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to create an <see cref="AesGcmKey"/> from a raw byte span.
    /// </summary>
    /// <returns><see langword="true"/> if the span length is a valid AES key size; otherwise <see langword="false"/>.</returns>
    public static bool TryFrom(ReadOnlySpan<byte> keyBytes, out AesGcmKey? result) {
        if(!IsValidKeySize(keyBytes.Length)) {
            result = null;
            return false;
        }
        result = new(Secret<byte>.From(keyBytes));
        return true;
    }

    /// <summary>
    /// Attempts to create an <see cref="AesGcmKey"/> from a hex-encoded key string.
    /// </summary>
    public static bool TryFrom(HexString hex, out AesGcmKey? result) {
        int decodedLen = hex.GetDecodedLength();
        if(!IsValidKeySize(decodedLen)) {
            result = null;
            return false;
        }

        Span<byte> buffer = stackalloc byte[decodedLen];
        if(!hex.TryDecode(buffer, out int written) || written != decodedLen) {
            result = null;
            return false;
        }

        try {
            result = new(Secret<byte>.From(buffer[..written]));
            return true;
        }
        finally {
            CryptographicOperations.ZeroMemory(buffer[..written]);
        }
    }

    /// <summary>
    /// Attempts to create an <see cref="AesGcmKey"/> from a Base64-encoded key string.
    /// </summary>
    public static bool TryFrom(Base64String base64, out AesGcmKey? result) {
        int decodedLen = base64.GetDecodedLength();
        if(!IsValidKeySize(decodedLen)) {
            result = null;
            return false;
        }

        Span<byte> buffer = stackalloc byte[decodedLen];
        if(!base64.TryDecode(buffer, out int written) || written != decodedLen) {
            result = null;
            return false;
        }

        try {
            result = new(Secret<byte>.From(buffer[..written]));
            return true;
        }
        finally {
            CryptographicOperations.ZeroMemory(buffer[..written]);
        }
    }

    // ── Generation ────────────────────────────────────────────────────────────

    /// <summary>Generates a cryptographically random 128-bit (16-byte) AES key.</summary>
    public static AesGcmKey Generate128() {
        return new(Secret<byte>.Generate(16));
    }

    /// <summary>Generates a cryptographically random 192-bit (24-byte) AES key.</summary>
    public static AesGcmKey Generate192() {
        return new(Secret<byte>.Generate(24));
    }

    /// <summary>Generates a cryptographically random 256-bit (32-byte) AES key.</summary>
    public static AesGcmKey Generate256() {
        return new(Secret<byte>.Generate(32));
    }

    /// <summary>Generates a cryptographically random AES key of the specified size.</summary>
    public static AesGcmKey Generate(AesGcmKeySize size) {
        return new(Secret<byte>.Generate((int)size));
    }

    private ref struct EncryptState {
        public ReadOnlySpan<byte> Plaintext;
        public Span<byte> Nonce;
        public Span<byte> Tag;
        public Span<byte> Ciphertext;
        public ReadOnlySpan<byte> AssociatedData;
    }

    private ref struct DecryptState {
        public ReadOnlySpan<byte> Nonce;
        public ReadOnlySpan<byte> Tag;
        public ReadOnlySpan<byte> Ciphertext;
        public Span<byte> Plaintext;
        public ReadOnlySpan<byte> AssociatedData;
    }

    // ── Encryption ────────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts a plaintext byte span using AES-GCM with a randomly generated nonce.
    /// </summary>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <returns>
    /// A byte array with the layout: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext) {
        return Encrypt(plaintext, default);
    }

    /// <summary>
    /// Encrypts a plaintext byte span using AES-GCM with a randomly generated nonce and optional associated data (AAD).
    /// </summary>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="associatedData">Extra data associated with this encryption, which must be provided during decryption but is not included in the ciphertext.</param>
    /// <returns>
    /// A byte array with the layout: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));

        byte[] packet = new byte[NonceSizeBytes + TagSizeBytes + plaintext.Length];

        Span<byte> nonce = packet.AsSpan(0, NonceSizeBytes);
        Span<byte> tag = packet.AsSpan(NonceSizeBytes, TagSizeBytes);
        Span<byte> ciphertext = packet.AsSpan(NonceSizeBytes + TagSizeBytes);

        RandomNumberGenerator.Fill(nonce);

        EncryptState state = new() {
            Plaintext = plaintext,
            Nonce = nonce,
            Tag = tag,
            Ciphertext = ciphertext,
            AssociatedData = associatedData
        };

        this._key.Expose(state, static (s, keySpan) => {
            using AesGcm aes = new(keySpan, TagSizeBytes);
            aes.Encrypt(s.Nonce, s.Plaintext, s.Ciphertext, s.Tag, s.AssociatedData);
        });

        return packet;
    }

    /// <summary>
    /// Encrypts a UTF-8 string using AES-GCM with optional associated data (AAD).
    /// </summary>
    /// <param name="plaintext">The string to encrypt.</param>
    /// <param name="associatedData">The optional associated data (AAD) that must match the data used during decryption.</param>
    /// <returns>
    /// A byte array with the layout: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plaintext"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception> 
    public byte[] Encrypt(string plaintext, ReadOnlySpan<byte> associatedData = default) {
        Preca.ThrowIfNull(plaintext);
        return Encrypt(plaintext, Encoding.UTF8, associatedData);
    }

    /// <summary>
    /// Encrypts a string using AES-GCM with the specified encoding and optional associated data (AAD).
    /// </summary>
    /// <param name="plaintext">The string to encrypt.</param>
    /// <param name="encoding">The encoding used to convert the string to bytes.</param>
    /// <param name="associatedData">The optional associated data (AAD) that must match the data used during decryption.</param>
    /// <returns>
    /// A byte array with the layout: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public byte[] Encrypt(string plaintext, Encoding encoding, ReadOnlySpan<byte> associatedData = default) {
        Preca.ThrowIfNull(plaintext);
        Preca.ThrowIfNull(encoding);
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));

        int maxByteCount = encoding.GetMaxByteCount(plaintext.Length);

        using ValueBuffer<byte> plainBytes = ValueBuffer.Create(maxByteCount, stackalloc byte[512]);
        try {
            int written = encoding.GetBytes(plaintext, plainBytes);
            return Encrypt(plainBytes[..written], associatedData);
        }
        finally {
            CryptographicOperations.ZeroMemory(plainBytes[..maxByteCount]);
        }
    }

    /// <summary>
    /// Encrypts the contents of a <see cref="Secret{Byte}"/> using AES-GCM.
    /// </summary>
    /// <param name="secret">The secret to encrypt.</param>
    /// <param name="associatedData">The optional associated data (AAD) that must match the data used during decryption.</param>
    /// <returns>
    /// A byte array with the layout: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public byte[] Encrypt(Secret<byte> secret, ReadOnlySpan<byte> associatedData = default) {
        Preca.ThrowIfNull(secret);
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));

        return secret.Expose(associatedData, (aad, span) => Encrypt(span, aad));
    }

    // ── Decryption ────────────────────────────────────────────────────────────

    /// <summary>
    /// Decrypts an AES-GCM cipher packet into secure unmanaged memory.
    /// The caller is responsible for disposing the returned <see cref="Secret{T}"/>.
    /// </summary>
    /// <param name="packet">
    /// The combined packet: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </param>
    /// <returns>A <see cref="Secret{T}"/> holding the decrypted plaintext bytes.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packet"/> is too short.</exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the authentication tag is invalid — data is corrupt or tampered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public Secret<byte> Decrypt(ReadOnlySpan<byte> packet) {
        return Decrypt(packet, default);
    }

    /// <summary>
    /// Decrypts an AES-GCM cipher packet into secure unmanaged memory using the provided associated data (AAD).
    /// The caller is responsible for disposing the returned <see cref="Secret{Byte}"/>.
    /// </summary>
    /// <param name="packet">
    /// The combined packet: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </param>
    /// <param name="associatedData">The exact associated data that was provided during encryption.</param>
    /// <returns>A <see cref="Secret{Byte}"/> holding the decrypted plaintext bytes.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packet"/> is too short.</exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the authentication tag is invalid — data is corrupt, tampered, or the wrong associated data was provided.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public Secret<byte> Decrypt(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> associatedData) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));

        Preca.ThrowIfLessThan(
            packet.Length,
            NonceSizeBytes + TagSizeBytes,
            (length) => new ArgumentException(
                $"Packet too short. Minimum is {NonceSizeBytes + TagSizeBytes} bytes. Got {length}.",
                nameof(packet)),
            packet.Length);

        int plainLength = packet.Length - NonceSizeBytes - TagSizeBytes;

        using ValueBuffer<byte> plainBytes = new(plainLength, stackalloc byte[1024]);
        try {
            DecryptState state = new() {
                Nonce = packet[..NonceSizeBytes],
                Tag = packet.Slice(NonceSizeBytes, TagSizeBytes),
                Ciphertext = packet[(NonceSizeBytes + TagSizeBytes)..],
                Plaintext = plainBytes[..plainLength],
                AssociatedData = associatedData
            };

            this._key.Expose(state, static (s, keySpan) => {
                using AesGcm aes = new(keySpan, TagSizeBytes);
                aes.Decrypt(s.Nonce, s.Ciphertext, s.Tag, s.Plaintext, s.AssociatedData);
            });

            return Secret<byte>.From(plainBytes[..plainLength]);
        }
        catch(AuthenticationTagMismatchException ex) {
            throw new CryptographicException(
                "AES-GCM authentication tag mismatch. The packet may be corrupt, tampered, or the associated data is incorrect.", ex);
        }
        finally {
            CryptographicOperations.ZeroMemory(plainBytes[..plainLength]);
        }
    }

    /// <summary>
    /// Decrypts an AES-GCM cipher packet and returns the plaintext as a UTF-8 string.
    /// </summary>
    /// <param name="packet">
    /// The combined packet: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </param>
    /// <param name="associatedData">The optional associated data (AAD) that must match the data used during encryption.</param>
    /// <returns>The decrypted plaintext as a <see cref="string"/>.</returns>
    /// <exception cref="CryptographicException">
    /// Thrown when the authentication tag is invalid (data is corrupt, tampered, or the associated data is incorrect).
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public string DecryptToString(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> associatedData = default) {
        return DecryptToString(packet, Encoding.UTF8, associatedData);
    }

    /// <summary>
    /// Decrypts an AES-GCM cipher packet and returns the plaintext as a string
    /// using the specified encoding.
    /// </summary>
    /// <param name="packet">
    /// The combined packet: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
    /// </param>
    /// <param name="encoding">The encoding used to interpret the decrypted bytes.</param>
    /// <param name="associatedData">The optional associated data (AAD) that must match the data used during encryption.</param>
    /// <returns>The decrypted plaintext as a <see cref="string"/>.</returns>
    /// <exception cref="CryptographicException">
    /// Thrown when the authentication tag is invalid (data is corrupt, tampered, or the associated data is incorrect).
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public string DecryptToString(ReadOnlySpan<byte> packet, Encoding encoding, ReadOnlySpan<byte> associatedData = default) {
        Preca.ThrowIfNull(encoding);

        using Secret<byte> secret = Decrypt(packet, associatedData);
        return secret.Expose(span => encoding.GetString(span));
    }

    // ── Key exposure ──────────────────────────────────────────────────────────

    /// <summary>
    /// Provides scoped, read-only access to the raw key bytes.
    /// Key material never leaves the callback scope.
    /// </summary>
    /// <param name="action">The delegate receiving the key span.</param>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public void Expose(Action<ReadOnlySpan<byte>> action) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));
        this._key.Expose(action);
    }

    /// <summary>
    /// Provides scoped, read-only access to the raw key bytes and returns a result.
    /// Key material never leaves the callback scope.
    /// </summary>
    /// <typeparam name="TResult">The return type of <paramref name="func"/>.</typeparam>
    /// <param name="func">The delegate receiving the key span.</param>
    /// <returns>The value returned by <paramref name="func"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));
        return this._key.Expose(func);
    }

    // ── Serialization helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Exports the key material as a <see cref="HexString"/>.
    /// </summary>
    /// <remarks>
    /// <b>Use with caution.</b> The result should only be written to secure storage.
    /// Never log or include in error messages.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public HexString ToHexString() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));
        return this._key.Expose(HexString.FromBytes);
    }

    /// <summary>
    /// Exports the key material as a <see cref="Base64String"/>.
    /// </summary>
    /// <remarks>
    /// <b>Use with caution.</b> The result should only be written to secure storage.
    /// Never log or include in error messages.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public Base64String ToBase64String() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));
        return this._key.Expose(Base64String.FromBytes);
    }

    /// <summary>
    /// Exports the key material as a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <remarks>
    /// <b>Use with caution.</b> The result should only be written to secure storage.
    /// Never log or include in error messages.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this key has been disposed.</exception>
    public Base64UrlString ToBase64UrlString() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(AesGcmKey));
        return this._key.Expose(Base64UrlString.FromBytes);
    }

    // ── Display ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a safe sentinel string. Key material is <b>never</b> included,
    /// making this safe for logging, exceptions, and debug watches.
    /// </summary>
    public override string ToString() {
        return $"[AES_GCM_KEY {this.KeySizeInBits}-bit]";
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Securely erases the key material from unmanaged memory and marks this instance as disposed.
    /// After disposal, all operations will throw <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._key.Dispose();
            this._disposeState.SetDisposed();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidKeySize(int byteLength) {
        return byteLength is 16 or 24 or 32;
    }

    private static void ValidateKeySize(int byteLength) {
        if(!IsValidKeySize(byteLength))
            throw new ArgumentException(
                $"AES-GCM key must be 16 (AES-128), 24 (AES-192), or 32 (AES-256) bytes. Got {byteLength} bytes ({byteLength * 8} bits).");
    }
}

/// <summary>
/// Extension methods for <see cref="AesGcmKey"/>.
/// </summary>
public static partial class AesGcmKeyExtensions {
    extension(AesGcmKey aesGcmKey) {
        /// <summary>
        /// Asynchronously encrypts a stream using AES-GCM.
        /// Reads the entire stream into secure memory before encrypting.
        /// </summary> 
        /// <param name="stream">The stream to encrypt. Will be read from the beginning if seekable.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A byte array with the layout: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
        /// </returns>
        public async ValueTask<byte[]> EncryptAsync(Stream stream, CancellationToken cancellationToken = default) {
            return await aesGcmKey.EncryptAsync(stream, default, cancellationToken);
        }

        /// <summary>
        /// Asynchronously encrypts a stream using AES-GCM with associated data (AAD).
        /// Reads the entire stream into secure memory before encrypting.
        /// </summary> 
        /// <param name="stream">The stream to encrypt. Will be read from the beginning if seekable.</param>
        /// <param name="associatedData">The associated data (AAD) that must match the data used during decryption.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A byte array with the layout: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
        /// </returns>
        public async ValueTask<byte[]> EncryptAsync(Stream stream,
                                                    ReadOnlyMemory<byte> associatedData,
                                                    CancellationToken cancellationToken = default) {
            Preca.ThrowIfNull(stream);

            if(stream.CanSeek) stream.Position = 0;

            if(stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> segment)) {
                return aesGcmKey.Encrypt(segment.AsSpan(), associatedData.Span);
            }

            int length;
            if(stream.CanSeek && stream.Length <= int.MaxValue) {
                length = (int)stream.Length;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
                try {
                    await stream.ReadExactlyAsync(buffer.AsMemory(0, length), cancellationToken);
                    return aesGcmKey.Encrypt(buffer.AsSpan(0, length), associatedData.Span);
                }
                finally {
                    CryptographicOperations.ZeroMemory(buffer.AsSpan(0, length));
                    ArrayPool<byte>.Shared.Return(buffer);
                    if(stream.CanSeek)
                        stream.Position = 0;
                }
            }

            using MemoryStream memoryStream = new();
            await stream.CopyToAsync(memoryStream, cancellationToken);

            byte[] internalBuffer = memoryStream.GetBuffer();
            length = (int)memoryStream.Length;
            try {
                return aesGcmKey.Encrypt(internalBuffer.AsSpan(0, length), associatedData.Span);
            }
            finally {
                CryptographicOperations.ZeroMemory(internalBuffer.AsSpan(0, length));
            }
        }

        /// <summary>
        /// Asynchronously decrypts a cipher packet and writes the plaintext to a stream.
        /// </summary> 
        /// <param name="packet">
        /// The combined packet: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
        /// </param>
        /// <param name="destination">The stream to write the plaintext into.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public ValueTask DecryptToAsync(byte[] packet, Stream destination, CancellationToken cancellationToken = default) {
            return aesGcmKey.DecryptToAsync(packet, destination, default, cancellationToken);
        }

        /// <summary>
        /// Asynchronously decrypts a cipher packet using associated data (AAD) and writes the plaintext to a stream.
        /// </summary> 
        /// <param name="packet">
        /// The combined packet: <c>| nonce (12 B) | tag (16 B) | ciphertext (N B) |</c>
        /// </param>
        /// <param name="destination">The stream to write the plaintext into.</param>
        /// <param name="associatedData">The associated data (AAD) that must match the data used during encryption.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async ValueTask DecryptToAsync(byte[] packet,
                                              Stream destination,
                                              ReadOnlyMemory<byte> associatedData,
                                              CancellationToken cancellationToken = default) {
            Preca.ThrowIfNull(destination);

            using Secret<byte> secret = aesGcmKey.Decrypt(packet.AsSpan(), associatedData.Span);

            await secret.ExposeAsync(destination.WriteAsync, cancellationToken);
        }
    }
}