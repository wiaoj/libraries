using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Webhooks.Signing;

/// <summary>
/// Abstract base class for HMAC-based webhook signers implementing timestamp-bound signing and constant-time verification.
/// </summary>
public abstract class HmacWebhookSignerBase : IWebhookSigner {

    /// <summary>
    /// The default HTTP header name used for webhook signatures.
    /// </summary>
    public const string DefaultHeaderName = "Webhook-Signature";

    /// <summary>
    /// The separator character used between key-value pairs in the signature header (e.g. "t=...,v1=...").
    /// </summary>
    public const char PairSeparator = ',';

    /// <summary>
    /// The separator character used between the key and value (e.g. "t=" or "v1=").
    /// </summary>
    public const char KeyValueSeparator = '=';

    /// <summary>
    /// The key name for the Unix epoch timestamp component in the signature header.
    /// </summary>
    public const char TimestampKey = 't';

    /// <summary>
    /// The formatted prefix for timestamp pairs in the signature header.
    /// </summary>
    public const string TimestampPrefix = "t=";

    /// <inheritdoc/>
    public abstract string AlgorithmName { get; }

    /// <inheritdoc/>
    public string HeaderName { get; }

    /// <inheritdoc/>
    public abstract string SchemePrefix { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacWebhookSignerBase"/> class using the default header name.
    /// </summary>
    protected HmacWebhookSignerBase() : this(DefaultHeaderName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacWebhookSignerBase"/> class using a custom header name.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    protected HmacWebhookSignerBase(string headerName) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        this.HeaderName = headerName;
    }

    /// <summary>
    /// Computes the cryptographic HMAC hash for the given data and returns its lowercase hexadecimal representation.
    /// </summary>
    /// <param name="key">The secret key bytes.</param>
    /// <param name="data">The combined timestamp and payload data.</param>
    /// <returns>A lowercase hexadecimal string representing the hash.</returns>
    protected abstract string ComputeHashString(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data);

    /// <inheritdoc/>
    public WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfEmpty(secretKey);

        byte[] signedBytes = CreateSignedBytes(payload, timestamp);
        try {
            string hash = ComputeHashString(secretKey, signedBytes);
            return new WebhookSignature(timestamp, this.SchemePrefix, hash);
        }
        finally {
            ArrayPool<byte>.Shared.Return(signedBytes);
        }
    }

    /// <inheritdoc/>
    public WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp) {
        Preca.ThrowIfNull(secretKey);

        byte[] signedBytes = CreateSignedBytes(payload, timestamp);
        try {
            string hash = secretKey.Expose(keySpan => ComputeHashString(keySpan, signedBytes));
            return new WebhookSignature(timestamp, this.SchemePrefix, hash);
        }
        finally {
            ArrayPool<byte>.Shared.Return(signedBytes);
        }
    }

    /// <inheritdoc/>
    public bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {
        if(string.IsNullOrWhiteSpace(signatureHeader) || secretKey.IsEmpty) {
            return false;
        }

        Span<Range> initialRangeBuffer = stackalloc Range[4];
        ValueList<Range> signatureRanges = new(initialRangeBuffer);
        try {
            if(!ValidateVerificationParameters(signatureHeader.AsSpan(), tolerance, currentTimestamp, out UnixTimestamp headerTimestamp, ref signatureRanges)) {
                return false;
            }

            byte[] signedBytes = CreateSignedBytes(payload, headerTimestamp);
            try {
                string expectedSignature = ComputeHashString(secretKey, signedBytes);
                return VerifyConstantTime(signatureHeader.AsSpan(), expectedSignature.AsSpan(), signatureRanges.AsSpan());
            }
            finally {
                ArrayPool<byte>.Shared.Return(signedBytes);
            }
        }
        finally {
            signatureRanges.Dispose();
        }
    }

    /// <inheritdoc/>
    public bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        Secret<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp) {
        Preca.ThrowIfNull(secretKey);

        if(string.IsNullOrWhiteSpace(signatureHeader)) {
            return false;
        }

        Span<Range> initialRangeBuffer = stackalloc Range[4];
        ValueList<Range> signatureRanges = new(initialRangeBuffer);
        try {
            if(!ValidateVerificationParameters(signatureHeader.AsSpan(), tolerance, currentTimestamp, out UnixTimestamp headerTimestamp, ref signatureRanges)) {
                return false;
            }

            byte[] signedBytes = CreateSignedBytes(payload, headerTimestamp);
            try {
                string expectedSignature = secretKey.Expose(keySpan => ComputeHashString(keySpan, signedBytes));
                return VerifyConstantTime(signatureHeader.AsSpan(), expectedSignature.AsSpan(), signatureRanges.AsSpan());
            }
            finally {
                ArrayPool<byte>.Shared.Return(signedBytes);
            }
        }
        finally {
            signatureRanges.Dispose();
        }
    }

    /// <inheritdoc/>
    public bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance) =>
        Verify(payload, signatureHeader, secretKey, tolerance, UnixTimestamp.Now);

    /// <inheritdoc/>
    public bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        Secret<byte> secretKey,
        TimeSpan tolerance) =>
        Verify(payload, signatureHeader, secretKey, tolerance, UnixTimestamp.Now);

    private bool ValidateVerificationParameters(
        ReadOnlySpan<char> signatureHeader,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp,
        out UnixTimestamp headerTimestamp,
        ref ValueList<Range> signatures) {
        headerTimestamp = default;

        if(tolerance < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance cannot be negative.");
        }

        if(!TryParseHeader(signatureHeader, out headerTimestamp, ref signatures)) {
            return false;
        }

        return IsTimestampWithinTolerance(headerTimestamp, currentTimestamp, tolerance);
    }

    private static bool VerifyConstantTime(
        ReadOnlySpan<char> header,
        ReadOnlySpan<char> expectedSignature,
        ReadOnlySpan<Range> signatures) {
        Span<byte> expectedBytes = stackalloc byte[expectedSignature.Length];
        Encoding.UTF8.GetBytes(expectedSignature, expectedBytes);

        Span<byte> candidateBytes = stackalloc byte[expectedSignature.Length];

        for(int i = 0; i < signatures.Length; i++) {
            ReadOnlySpan<char> candidate = header[signatures[i]];
            if(candidate.Length != expectedSignature.Length) {
                continue;
            }

            Encoding.UTF8.GetBytes(candidate, candidateBytes);
            Ascii.ToLowerInPlace(candidateBytes, out _);

            if(CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsTimestampWithinTolerance(UnixTimestamp headerTimestamp, UnixTimestamp currentTimestamp, TimeSpan tolerance) {
        long currentSec = currentTimestamp.TotalSeconds;
        long headerSec = headerTimestamp.TotalSeconds;
        long toleranceSec = (long)tolerance.TotalSeconds;

        long minValidSec = currentSec >= (long.MinValue + toleranceSec) ? currentSec - toleranceSec : long.MinValue;
        long maxValidSec = currentSec <= (long.MaxValue - toleranceSec) ? currentSec + toleranceSec : long.MaxValue;

        return headerSec >= minValidSec && headerSec <= maxValidSec;
    }

    private static byte[] CreateSignedBytes(ReadOnlySpan<byte> payload, UnixTimestamp timestamp) {
        Span<byte> timestampBuf = stackalloc byte[32];
        if(!Utf8Formatter.TryFormat(timestamp.TotalSeconds, timestampBuf, out int bytesWritten)) {
            throw new InvalidOperationException("Failed to format timestamp.");
        }

        int totalLength = bytesWritten + 1 + payload.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(totalLength);

        timestampBuf[..bytesWritten].CopyTo(buffer.AsSpan(0, bytesWritten));
        buffer[bytesWritten] = (byte)'.';
        payload.CopyTo(buffer.AsSpan(bytesWritten + 1, payload.Length));

        return buffer;
    }

    private bool TryParseHeader(ReadOnlySpan<char> header, out UnixTimestamp timestamp, ref ValueList<Range> signatures) {
        timestamp = default;
        int timestampCount = 0;
        int currentOffset = 0;

        ReadOnlySpan<char> span = header;
        ReadOnlySpan<char> schemeWithEquals = $"{this.SchemePrefix}{KeyValueSeparator}".AsSpan();

        while(!span.IsEmpty) {
            int commaIndex = span.IndexOf(PairSeparator);
            ReadOnlySpan<char> rawPart = commaIndex >= 0 ? span[..commaIndex] : span;
            ReadOnlySpan<char> part = rawPart.Trim();

            if(!part.IsEmpty) {
                if(part.StartsWith(TimestampPrefix, StringComparison.OrdinalIgnoreCase)) {
                    timestampCount++;
                    if(timestampCount > 1) {
                        // Parameter pollution defense: multiple t= timestamps in a single header are strictly rejected
                        return false;
                    }

                    ReadOnlySpan<char> timeSpan = part[TimestampPrefix.Length..];
                    if(long.TryParse(timeSpan, out long parsedTime)) {
                        if(parsedTime < UnixTimestamp.MinValue.TotalSeconds || parsedTime > UnixTimestamp.MaxValue.TotalSeconds) {
                            return false;
                        }
                        timestamp = UnixTimestamp.FromSeconds(parsedTime);
                    }
                    else {
                        return false;
                    }
                }
                else if(part.StartsWith(schemeWithEquals, StringComparison.OrdinalIgnoreCase)) {
                    int partStart = currentOffset + (rawPart.Length - rawPart.TrimStart().Length);
                    int sigValStart = partStart + schemeWithEquals.Length;
                    ReadOnlySpan<char> sigVal = part[schemeWithEquals.Length..];
                    int sigValTrimmedStart = sigValStart + (sigVal.Length - sigVal.TrimStart().Length);
                    ReadOnlySpan<char> sigValTrimmed = sigVal.Trim();

                    if(!sigValTrimmed.IsEmpty) {
                        signatures.Add(new Range(sigValTrimmedStart, sigValTrimmedStart + sigValTrimmed.Length));
                    }
                }
            }

            int advance = commaIndex >= 0 ? commaIndex + 1 : span.Length;
            span = commaIndex >= 0 ? span[(commaIndex + 1)..] : default;
            currentOffset += advance;
        }

        return timestampCount == 1 && !signatures.IsEmpty;
    }
}
