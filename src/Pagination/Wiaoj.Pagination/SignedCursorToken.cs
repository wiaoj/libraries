using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Pagination.JsonConverters;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Pagination;

/// <summary>
/// Represents a cryptographically signed, tamper-evident keyset cursor token using HMAC-SHA256 and an issuance timestamp.
/// </summary>
/// <remarks>
/// <para>
/// <b>Wire Format:</b> Formatted as a dot-delimited string sequence: <c>Payload.Timestamp.Signature</c>, where all components are URL-safe.
/// </para>
/// <para>
/// <b>Security Guarantees:</b>
/// <list type="bullet">
///   <item><description><b>Tamper Resistance:</b> Modifying the token payload or timestamp invalidates the HMAC signature.</description></item>
///   <item><description><b>Timing-Attack Resistance:</b> Verification uses constant-time comparisons (<see cref="CryptographicOperations.FixedTimeEquals"/>).</description></item>
///   <item><description><b>Replay &amp; Stale Data Defense:</b> Issuance timestamps allow optional time-to-live (TTL) expiration enforcement via <c>maxAge</c>.</description></item>
/// </list>
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(SignedCursorTokenJsonConverter))]
public readonly record struct SignedCursorToken :
    IEquatable<SignedCursorToken>,
    ISpanParsable<SignedCursorToken>,
    IUtf8SpanParsable<SignedCursorToken>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<SignedCursorToken, SignedCursorToken, bool> {

    private const char SegmentDelimiter = '.';
    private const int MinimumSecretKeyLengthInBytes = 32;

    /// <summary>
    /// Represents an empty or uninitialized signed cursor token.
    /// </summary>
    public static readonly SignedCursorToken Empty = default;

    /// <summary>
    /// Gets the underlying cursor payload token.
    /// </summary>
    public CursorToken Token { get; }

    /// <summary>
    /// Gets the issuance timestamp representing the moment this token was cryptographically signed.
    /// </summary>
    public UnixTimestamp Timestamp { get; }

    /// <summary>
    /// Gets the 32-byte HMAC-SHA256 cryptographic signature verifying the authenticity of the token and timestamp.
    /// </summary>
    public HmacSha256Hash Signature { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is empty or uninitialized.
    /// </summary>
    public bool IsEmpty => this.Token.IsEmpty && this.Signature == HmacSha256Hash.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignedCursorToken"/> struct with explicit components.
    /// </summary>
    /// <param name="token">The underlying cursor token payload.</param>
    /// <param name="timestamp">The timestamp recording when the token was signed.</param>
    /// <param name="signature">The HMAC-SHA256 signature calculated over the token payload and timestamp.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SignedCursorToken(CursorToken token, UnixTimestamp timestamp, HmacSha256Hash signature) {
        this.Token = token;
        this.Timestamp = timestamp;
        this.Signature = signature;
    }

    #region Cryptographic Signing & Verification

    /// <summary>
    /// Cryptographically signs a <see cref="CursorToken"/> using HMAC-SHA256 and the current UTC system timestamp (<see cref="UnixTimestamp.Now"/>).
    /// </summary>
    /// <param name="token">The cursor token payload to sign.</param>
    /// <param name="secretKey">The cryptographic secret key. Must be at least 32 bytes in length.</param>
    /// <returns>A new <see cref="SignedCursorToken"/> containing the payload, timestamp, and signature.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secretKey"/> is empty or shorter than 32 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SignedCursorToken Sign(CursorToken token, ReadOnlySpan<byte> secretKey) {
        return Sign(token, secretKey, UnixTimestamp.Now);
    }

    /// <summary>
    /// Cryptographically signs a <see cref="CursorToken"/> using HMAC-SHA256 and a specified creation timestamp.
    /// </summary>
    /// <param name="token">The cursor token payload to sign.</param>
    /// <param name="secretKey">The cryptographic secret key. Must be at least 32 bytes in length.</param>
    /// <param name="timestamp">The explicit issuance timestamp to bind into the signature.</param>
    /// <returns>A new <see cref="SignedCursorToken"/> containing the payload, timestamp, and signature.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secretKey"/> is empty or shorter than 32 bytes.</exception>
    public static SignedCursorToken Sign(CursorToken token, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfEmpty(secretKey);
        Preca.ThrowIfLessThan(
            secretKey.Length,
            MinimumSecretKeyLengthInBytes,
            static () => new ArgumentException($"Secret key must be at least {MinimumSecretKeyLengthInBytes} bytes long.", nameof(secretKey)));

        if(token.IsEmpty) {
            return Empty;
        }

        HmacSha256Hash signature = ComputeSignature(token, timestamp, secretKey);
        return new SignedCursorToken(token, timestamp, signature);
    }

    /// <summary>
    /// Cryptographically signs a <see cref="CursorToken"/> using HMAC-SHA256 and a timestamp resolved from the provided <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="token">The cursor token payload to sign.</param>
    /// <param name="secretKey">The cryptographic secret key. Must be at least 32 bytes in length.</param>
    /// <param name="timeProvider">The time provider used to obtain the current UTC instant.</param>
    /// <returns>A new <see cref="SignedCursorToken"/> containing the payload, timestamp, and signature.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secretKey"/> is empty or shorter than 32 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SignedCursorToken Sign(CursorToken token, ReadOnlySpan<byte> secretKey, TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        return Sign(token, secretKey, UnixTimestamp.From(timeProvider));
    }

    /// <summary>
    /// Verifies whether the HMAC-SHA256 signature is authentic for this token and timestamp without enforcing lifetime expiration.
    /// </summary>
    /// <param name="secretKey">The secret key to verify against.</param>
    /// <returns><see langword="true"/> if the signature is authentic and untampered; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Verify(ReadOnlySpan<byte> secretKey) {
        if(this.IsEmpty || secretKey.Length < MinimumSecretKeyLengthInBytes) {
            return false;
        }

        HmacSha256Hash expectedSignature = ComputeSignature(this.Token, this.Timestamp, secretKey);
        return expectedSignature == this.Signature;
    }

    /// <summary>
    /// Verifies whether the HMAC-SHA256 signature is authentic and the token age does not exceed the specified <paramref name="maxAge"/> lifetime using the system clock.
    /// </summary>
    /// <param name="secretKey">The secret key to verify against.</param>
    /// <param name="maxAge">The maximum allowable duration since token issuance.</param>
    /// <returns><see langword="true"/> if authentic and within lifetime; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Verify(ReadOnlySpan<byte> secretKey, TimeSpan maxAge) {
        return Verify(secretKey, maxAge, UnixTimestamp.Now);
    }

    /// <summary>
    /// Verifies whether the HMAC-SHA256 signature is authentic and the token age does not exceed the specified <paramref name="maxAge"/> lifetime using the provided <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="secretKey">The secret key to verify against.</param>
    /// <param name="maxAge">The maximum allowable duration since token issuance.</param>
    /// <param name="timeProvider">The time provider used to obtain the current UTC instant.</param>
    /// <returns><see langword="true"/> if authentic and within lifetime; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Verify(ReadOnlySpan<byte> secretKey, TimeSpan maxAge, TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        return Verify(secretKey, maxAge, UnixTimestamp.From(timeProvider));
    }

    /// <summary>
    /// Verifies whether the HMAC-SHA256 signature is authentic and the token age does not exceed the specified <paramref name="maxAge"/> lifetime relative to an explicit reference timestamp.
    /// </summary>
    /// <param name="secretKey">The secret key to verify against.</param>
    /// <param name="maxAge">The maximum allowable duration since token issuance.</param>
    /// <param name="currentTimestamp">The current reference timestamp.</param>
    /// <returns><see langword="true"/> if authentic and within lifetime; otherwise, <see langword="false"/>.</returns>
    public bool Verify(ReadOnlySpan<byte> secretKey, TimeSpan maxAge, UnixTimestamp currentTimestamp) {
        if(!Verify(secretKey)) {
            return false;
        }

        if(maxAge <= TimeSpan.Zero) {
            return false;
        }

        TimeSpan age = currentTimestamp - this.Timestamp;
        return age >= TimeSpan.Zero && age <= maxAge;
    }

    /// <summary>
    /// Attempts to verify the HMAC-SHA256 signature and extract the inner <see cref="CursorToken"/> without lifetime expiration checks.
    /// </summary>
    /// <param name="secretKey">The secret key to verify against.</param>
    /// <param name="token">When valid, contains the verified inner cursor token; otherwise, <see cref="CursorToken.Empty"/>.</param>
    /// <returns><see langword="true"/> if verification succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryUnsign(ReadOnlySpan<byte> secretKey, out CursorToken token) {
        if(Verify(secretKey)) {
            token = this.Token;
            return true;
        }

        token = CursorToken.Empty;
        return false;
    }

    /// <summary>
    /// Attempts to verify the HMAC-SHA256 signature, validate the token lifetime against the system clock, and extract the inner <see cref="CursorToken"/>.
    /// </summary>
    /// <param name="secretKey">The secret key to verify against.</param>
    /// <param name="maxAge">The maximum allowable duration since token issuance.</param>
    /// <param name="token">When valid and unexpired, contains the verified inner cursor token; otherwise, <see cref="CursorToken.Empty"/>.</param>
    /// <returns><see langword="true"/> if verification and lifetime checks succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryUnsign(ReadOnlySpan<byte> secretKey, TimeSpan maxAge, out CursorToken token) {
        if(Verify(secretKey, maxAge)) {
            token = this.Token;
            return true;
        }

        token = CursorToken.Empty;
        return false;
    }

    /// <summary>
    /// Attempts to verify the HMAC-SHA256 signature, validate the token lifetime using the provided <see cref="TimeProvider"/>, and extract the inner <see cref="CursorToken"/>.
    /// </summary>
    /// <param name="secretKey">The secret key to verify against.</param>
    /// <param name="maxAge">The maximum allowable duration since token issuance.</param>
    /// <param name="timeProvider">The time provider used to obtain the current UTC instant.</param>
    /// <param name="token">When valid and unexpired, contains the verified inner cursor token; otherwise, <see cref="CursorToken.Empty"/>.</param>
    /// <returns><see langword="true"/> if verification and lifetime checks succeeded; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryUnsign(ReadOnlySpan<byte> secretKey, TimeSpan maxAge, TimeProvider timeProvider, out CursorToken token) {
        Preca.ThrowIfNull(timeProvider);

        if(Verify(secretKey, maxAge, timeProvider)) {
            token = this.Token;
            return true;
        }

        token = CursorToken.Empty;
        return false;
    }

    /// <summary>
    /// Attempts to verify the HMAC-SHA256 signature, validate the token lifetime against an explicit reference timestamp, and extract the inner <see cref="CursorToken"/>.
    /// </summary>
    /// <param name="secretKey">The secret key to verify against.</param>
    /// <param name="maxAge">The maximum allowable duration since token issuance.</param>
    /// <param name="currentTimestamp">The current reference timestamp to compare against.</param>
    /// <param name="token">When valid and unexpired, contains the verified inner cursor token; otherwise, <see cref="CursorToken.Empty"/>.</param>
    /// <returns><see langword="true"/> if verification and lifetime checks succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryUnsign(ReadOnlySpan<byte> secretKey, TimeSpan maxAge, UnixTimestamp currentTimestamp, out CursorToken token) {
        if(Verify(secretKey, maxAge, currentTimestamp)) {
            token = this.Token;
            return true;
        }

        token = CursorToken.Empty;
        return false;
    }

    private static HmacSha256Hash ComputeSignature(CursorToken token, UnixTimestamp timestamp, ReadOnlySpan<byte> secretKey) {
        int tokenLength = token.Length;
        int totalPayloadLength = tokenLength + 1 + sizeof(long);

        using ValueBuffer<byte> payloadBuffer = new(totalPayloadLength, stackalloc byte[128]);
        Span<byte> span = payloadBuffer.Span;

        Encoding.ASCII.GetBytes(token.Value, span[..tokenLength]);
        span[tokenLength] = (byte)':';
        BinaryPrimitives.WriteInt64BigEndian(span[(tokenLength + 1)..], timestamp.TotalMilliseconds);

        return HmacSha256Hash.Compute(secretKey, span[..totalPayloadLength]);
    }

    #endregion

    #region Parsing (ISpanParsable, IUtf8SpanParsable)

    /// <summary>
    /// Parses a string formatted as <c>Payload.Timestamp.Signature</c> into a <see cref="SignedCursorToken"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A valid <see cref="SignedCursorToken"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the string format is invalid or corrupted.</exception>
    public static SignedCursorToken Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span formatted as <c>Payload.Timestamp.Signature</c> into a <see cref="SignedCursorToken"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="SignedCursorToken"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the span format is invalid or corrupted.</exception>
    public static SignedCursorToken Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out SignedCursorToken result)) {
            return result;
        }
        throw new FormatException("Invalid SignedCursorToken format. Expected 'Payload.Timestamp.Signature'.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="SignedCursorToken"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A valid <see cref="SignedCursorToken"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the byte sequence is not a valid signed cursor token.</exception>
    public static SignedCursorToken Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out SignedCursorToken result)) {
            return result;
        }
        throw new FormatException("Invalid UTF-8 sequence for SignedCursorToken.");
    }

    /// <summary>
    /// Tries to parse a string formatted as <c>Payload.Timestamp.Signature</c> into a <see cref="SignedCursorToken"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed token if parsing succeeded; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out SignedCursorToken result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span formatted as <c>Payload.Timestamp.Signature</c> into a <see cref="SignedCursorToken"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed token if parsing succeeded; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out SignedCursorToken result) {
        result = default;
        if(s.IsEmpty) return false;

        int firstDot = s.IndexOf(SegmentDelimiter);
        if(firstDot <= 0) return false;

        ReadOnlySpan<char> payloadSpan = s[..firstDot];
        ReadOnlySpan<char> remainder = s[(firstDot + 1)..];

        int secondDot = remainder.IndexOf(SegmentDelimiter);
        if(secondDot <= 0 || secondDot == remainder.Length - 1) return false;

        ReadOnlySpan<char> timestampSpan = remainder[..secondDot];
        ReadOnlySpan<char> sigSpan = remainder[(secondDot + 1)..];

        if(!CursorToken.TryParse(payloadSpan, out CursorToken token)) return false;
        if(!UnixTimestamp.TryParse(timestampSpan, out UnixTimestamp timestamp)) return false;

        Span<byte> sigBytes = stackalloc byte[HmacSha256Hash.SizeInBytes];
        if(!Base64UrlString.TryDecode(sigSpan, sigBytes, out int written) 
            || !HmacSha256Hash.TryFromBytes(sigBytes[..written], out HmacSha256Hash signature)) {
            return false;
        }

        result = new SignedCursorToken(token, timestamp, signature);
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="SignedCursorToken"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed token if parsing succeeded; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out SignedCursorToken result) {
        if(utf8Text.IsEmpty) {
            result = default;
            return false;
        }

        Span<char> charBuf = stackalloc char[utf8Text.Length];
        if(Ascii.ToUtf16(utf8Text, charBuf, out _) == System.Buffers.OperationStatus.Done) {
            return TryParse(charBuf, out result);
        }

        result = default;
        return false;
    }

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <summary>
    /// Returns the canonical dot-delimited string representation (<c>Payload.Timestamp.Signature</c>).
    /// </summary>
    /// <returns>A formatted string containing the payload, timestamp, and signature.</returns>
    public override string ToString() {
        if(this.IsEmpty) return string.Empty;
        return $"{this.Token.Value}.{this.Timestamp.TotalMilliseconds}.{this.Signature.ToBase64UrlString().Value}";
    }

    /// <summary>
    /// Formats the signed cursor token into the destination character span.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.IsEmpty) {
            charsWritten = 0;
            return true;
        }

        Base64UrlString sigBase64 = this.Signature.ToBase64UrlString();
        Span<char> timeBuf = stackalloc char[24];
        if(!this.Timestamp.TotalMilliseconds.TryFormat(timeBuf, out int timeCharsWritten)) {
            charsWritten = 0;
            return false;
        }

        int requiredLength = this.Token.Length + 1 + timeCharsWritten + 1 + sigBase64.Length;
        if(destination.Length < requiredLength) {
            charsWritten = 0;
            return false;
        }

        this.Token.Value.AsSpan().CopyTo(destination);
        destination[this.Token.Length] = '.';

        timeBuf[..timeCharsWritten].CopyTo(destination[(this.Token.Length + 1)..]);
        int sigOffset = this.Token.Length + 1 + timeCharsWritten;
        destination[sigOffset] = '.';

        sigBase64.Value.AsSpan().CopyTo(destination[(sigOffset + 1)..]);

        charsWritten = requiredLength;
        return true;
    }

    /// <summary>
    /// Formats the signed cursor token into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of UTF-8 bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(this.IsEmpty) {
            bytesWritten = 0;
            return true;
        }

        Span<char> charBuf = stackalloc char[160];
        if(TryFormat(charBuf, out int charsWritten)) {
            if(utf8Destination.Length < charsWritten) {
                bytesWritten = 0;
                return false;
            }

            System.Text.Ascii.FromUtf16(charBuf[..charsWritten], utf8Destination, out bytesWritten);
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    // --- Explicit Interface Implementations ---

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return ToString();
    }

    bool ISpanFormattable.TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return TryFormat(utf8Destination, out bytesWritten);
    }

    static SignedCursorToken IParsable<SignedCursorToken>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<SignedCursorToken>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SignedCursorToken result) {
        return TryParse(s, out result);
    }

    static SignedCursorToken ISpanParsable<SignedCursorToken>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<SignedCursorToken>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SignedCursorToken result) {
        return TryParse(s, out result);
    }

    static SignedCursorToken IUtf8SpanParsable<SignedCursorToken>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<SignedCursorToken>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out SignedCursorToken result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    #region Implicit / Explicit Operators

    /// <summary>
    /// Implicitly converts a <see cref="SignedCursorToken"/> to its canonical string representation.
    /// </summary>
    /// <param name="token">The signed cursor token instance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(SignedCursorToken token) {
        return token.ToString();
    }

    /// <summary>
    /// Explicitly converts a string representation to a <see cref="SignedCursorToken"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <exception cref="FormatException">Thrown when the string format is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SignedCursorToken(string s) {
        return Parse(s);
    }

    #endregion
}