using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Wiaoj.Pagination.JsonConverters;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Pagination;

/// <summary>
/// Represents a tamper-proof, cryptographically signed pagination cursor token using HMAC-SHA256.
/// </summary>
/// <remarks>
/// <para>
/// Formatted as <c>Payload.Signature</c> where both segments are valid Base64Url strings (RFC 4648, Section 5).
/// Prevents parameter tampering, ID enumeration, and cursor spoofing attacks.
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

    /// <summary>
    /// Represents an empty or uninitialized signed cursor token.
    /// </summary>
    public static readonly SignedCursorToken Empty = default;

    /// <summary>
    /// Gets the underlying cursor payload token.
    /// </summary>
    public CursorToken Token { get; }

    /// <summary>
    /// Gets the HMAC-SHA256 cryptographic signature.
    /// </summary>
    public Sha256Hash Signature { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is empty or uninitialized.
    /// </summary>
    public bool IsEmpty => this.Token.IsEmpty && this.Signature == Sha256Hash.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignedCursorToken"/> struct.
    /// </summary>
    /// <param name="token">The cursor token payload.</param>
    /// <param name="signature">The HMAC-SHA256 signature.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SignedCursorToken(CursorToken token, Sha256Hash signature) {
        this.Token = token;
        this.Signature = signature;
    }

    #region Cryptographic Signing & Verification

    /// <summary>
    /// Signs a <see cref="CursorToken"/> using HMAC-SHA256 with zero heap allocations.
    /// </summary>
    /// <param name="token">The cursor token to sign.</param>
    /// <param name="secretKey">The secret key used for HMAC signing.</param>
    /// <returns>A new <see cref="SignedCursorToken"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SignedCursorToken Sign(CursorToken token, ReadOnlySpan<byte> secretKey) {
        if(token.IsEmpty) {
            return Empty;
        }

        Span<byte> signatureBuffer = stackalloc byte[Sha256Hash.SizeInBytes];
        Span<byte> payloadBuffer = stackalloc byte[token.Length];

        System.Text.Ascii.FromUtf16(token.Value.AsSpan(), payloadBuffer, out _);
        HMACSHA256.HashData(secretKey, payloadBuffer, signatureBuffer);

        return new SignedCursorToken(token, Sha256Hash.FromBytes(signatureBuffer));
    }

    /// <summary>
    /// Verifies whether the signature matches the token payload using a timing-attack resistant comparison.
    /// </summary>
    /// <param name="secretKey">The secret key used for verification.</param>
    /// <returns><see langword="true"/> if the signature is authentic; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Verify(ReadOnlySpan<byte> secretKey) {
        if(IsEmpty) {
            return false;
        }

        Span<byte> expectedSignature = stackalloc byte[Sha256Hash.SizeInBytes];
        Span<byte> payloadBuffer = stackalloc byte[this.Token.Length];

        System.Text.Ascii.FromUtf16(this.Token.Value.AsSpan(), payloadBuffer, out _);
        HMACSHA256.HashData(secretKey, payloadBuffer, expectedSignature);

        return CryptographicOperations.FixedTimeEquals(expectedSignature, this.Signature.AsSpan());
    }

    /// <summary>
    /// Attempts to verify the signature and retrieve the inner <see cref="CursorToken"/>.
    /// </summary>
    /// <param name="secretKey">The secret key.</param>
    /// <param name="token">When valid, contains the authentic cursor token.</param>
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

    #endregion

    #region Parsing (ISpanParsable, IUtf8SpanParsable)

    /// <summary>
    /// Parses a string into a <see cref="SignedCursorToken"/>.
    /// </summary>
    public static SignedCursorToken Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span formatted as <c>Payload.Signature</c> into a <see cref="SignedCursorToken"/>.
    /// </summary>
    public static SignedCursorToken Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out SignedCursorToken result)) {
            return result;
        }
        throw new FormatException("Invalid SignedCursorToken format. Expected 'Payload.Signature'.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="SignedCursorToken"/>.
    /// </summary>
    public static SignedCursorToken Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out SignedCursorToken result)) {
            return result;
        }
        throw new FormatException("Invalid UTF-8 sequence for SignedCursorToken.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="SignedCursorToken"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out SignedCursorToken result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span formatted as <c>Payload.Signature</c> into a <see cref="SignedCursorToken"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out SignedCursorToken result) {
        if(s.IsEmpty) {
            result = default;
            return false;
        }

        int dotIndex = s.LastIndexOf('.');
        if(dotIndex <= 0 || dotIndex >= s.Length - 1) {
            result = default;
            return false;
        }

        ReadOnlySpan<char> payloadSpan = s[..dotIndex];
        ReadOnlySpan<char> sigSpan = s[(dotIndex + 1)..];

        if(!CursorToken.TryParse(payloadSpan, out CursorToken token)) {
            result = default;
            return false;
        }

        Span<byte> sigBytes = stackalloc byte[Sha256Hash.SizeInBytes];
        if(!Base64UrlString.TryDecode(sigSpan, sigBytes, out int written) || written != Sha256Hash.SizeInBytes) {
            result = default;
            return false;
        }

        result = new SignedCursorToken(token, Sha256Hash.FromBytes(sigBytes));
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="SignedCursorToken"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out SignedCursorToken result) {
        if(utf8Text.IsEmpty) {
            result = default;
            return false;
        }

        Span<char> charBuf = stackalloc char[utf8Text.Length];
        if(System.Text.Ascii.ToUtf16(utf8Text, charBuf, out _) == System.Buffers.OperationStatus.Done) {
            return TryParse(charBuf, out result);
        }

        result = default;
        return false;
    }

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <inheritdoc/>
    public override string ToString() {
        if(IsEmpty) return string.Empty;
        return $"{this.Token.Value}.{this.Signature.ToBase64UrlString().Value}";
    }

    /// <summary>
    /// Tries to format the signed cursor into the destination character span with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(IsEmpty) {
            charsWritten = 0;
            return true;
        }

        Base64UrlString sigBase64 = this.Signature.ToBase64UrlString();
        int requiredLength = this.Token.Length + 1 + sigBase64.Length;

        if(destination.Length < requiredLength) {
            charsWritten = 0;
            return false;
        }

        this.Token.Value.AsSpan().CopyTo(destination);
        destination[this.Token.Length] = '.';
        sigBase64.Value.AsSpan().CopyTo(destination[(this.Token.Length + 1)..]);

        charsWritten = requiredLength;
        return true;
    }

    /// <summary>
    /// Tries to format the signed cursor into the destination UTF-8 byte span with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(IsEmpty) {
            bytesWritten = 0;
            return true;
        }

        Span<char> charBuf = stackalloc char[128];
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

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ToString();

    bool ISpanFormattable.TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) => TryFormat(destination, out charsWritten);

    bool IUtf8SpanFormattable.TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) => TryFormat(utf8Destination, out bytesWritten);

    static SignedCursorToken IParsable<SignedCursorToken>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<SignedCursorToken>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SignedCursorToken result) => TryParse(s, out result);
    static SignedCursorToken ISpanParsable<SignedCursorToken>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<SignedCursorToken>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SignedCursorToken result) => TryParse(s, out result);
    static SignedCursorToken IUtf8SpanParsable<SignedCursorToken>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<SignedCursorToken>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out SignedCursorToken result) => TryParse(utf8Text, out result);

    #endregion

    #region Implicit / Explicit Operators

    /// <summary>
    /// Implicitly converts a <see cref="SignedCursorToken"/> to its formatted string representation.
    /// </summary>
    /// <param name="token">The signed cursor token.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(SignedCursorToken token) => token.ToString();

    /// <summary>
    /// Explicitly converts a string to a <see cref="SignedCursorToken"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <exception cref="FormatException">Thrown if the string is not valid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SignedCursorToken(string s) => Parse(s);

    #endregion
}