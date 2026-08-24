using System.Buffers;
using System.Buffers.Text;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Webhooks.Signing.Asymmetric;

/// <summary>
/// Abstract base class for asymmetric cryptographic webhook signers implementing timestamp-bound signing,
/// DoS-protected header parsing, and replay attack mitigation.
/// </summary>
public abstract class AsymmetricWebhookSignerBase : IWebhookSigner {
    /// <summary>The default HTTP header name used for webhook signatures.</summary>
    public const string DefaultHeaderName = WebhookHeaderNames.WebhookSignature;

    /// <summary>The separator character between key-value pairs in the signature header.</summary>
    public const char PairSeparator = ',';

    /// <summary>The separator character between key and value in the signature header.</summary>
    public const char KeyValueSeparator = '=';

    /// <summary>The prefix identifying the timestamp component in signature headers.</summary>
    public const string TimestampPrefix = "t=";

    /// <inheritdoc/>
    public abstract string AlgorithmName { get; }

    /// <inheritdoc/>
    public string HeaderName { get; }

    /// <inheritdoc/>
    public abstract string SchemePrefix { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsymmetricWebhookSignerBase"/> class.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name. Defaults to <c>"Webhook-Signature"</c>.</param>
    protected AsymmetricWebhookSignerBase(string headerName = DefaultHeaderName) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        this.HeaderName = headerName;
    }

    /// <inheritdoc/>
    public abstract WebhookSignature Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> secretKey, UnixTimestamp timestamp);

    /// <inheritdoc/>
    public abstract WebhookSignature Sign(ReadOnlySpan<byte> payload, Secret<byte> secretKey, UnixTimestamp timestamp);

    /// <inheritdoc/>
    public abstract bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        ReadOnlySpan<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp);

    /// <inheritdoc/>
    public abstract bool Verify(
        ReadOnlySpan<byte> payload,
        string signatureHeader,
        Secret<byte> secretKey,
        TimeSpan tolerance,
        UnixTimestamp currentTimestamp);

    /// <inheritdoc/>
    public bool Verify(ReadOnlySpan<byte> payload, string signatureHeader, ReadOnlySpan<byte> secretKey, TimeSpan tolerance) {
        return Verify(payload, signatureHeader, secretKey, tolerance, UnixTimestamp.Now);
    }

    /// <inheritdoc/>
    public bool Verify(ReadOnlySpan<byte> payload, string signatureHeader, Secret<byte> secretKey, TimeSpan tolerance) {
        return Verify(payload, signatureHeader, secretKey, tolerance, UnixTimestamp.Now);
    }

    /// <summary>
    /// Formats the canonical byte sequence to be digitally signed or verified: <c>{timestamp}.{payload}</c>.
    /// Uses <see cref="ArrayPool{Byte}"/> to minimize GC allocations.
    /// </summary>
    /// <param name="payload">The raw UTF-8 payload bytes.</param>
    /// <param name="timestamp">The generation timestamp.</param>
    /// <param name="totalLength">When this method returns, contains the exact length of the canonical data.</param>
    /// <returns>A rented byte array containing the canonical data. Caller MUST return to pool.</returns>
    protected static byte[] CreateSignedBytes(ReadOnlySpan<byte> payload, UnixTimestamp timestamp, out int totalLength) {
        Span<byte> timestampBuf = stackalloc byte[32];
        if(!Utf8Formatter.TryFormat(timestamp.TotalSeconds, timestampBuf, out int bytesWritten)) {
            throw new InvalidOperationException("Failed to format timestamp as UTF-8.");
        }

        totalLength = bytesWritten + 1 + payload.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(totalLength);

        timestampBuf[..bytesWritten].CopyTo(buffer.AsSpan(0, bytesWritten));
        buffer[bytesWritten] = (byte)'.';
        payload.CopyTo(buffer.AsSpan(bytesWritten + 1, payload.Length));

        return buffer;
    }

    /// <summary>
    /// Validates clock skew tolerance and parses candidate signature slices from the header without string allocations.
    /// </summary>
    protected bool ValidateAndExtractSignatures(
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

    private static bool IsTimestampWithinTolerance(UnixTimestamp headerTimestamp, UnixTimestamp currentTimestamp, TimeSpan tolerance) {
        long currentSec = currentTimestamp.TotalSeconds;
        long headerSec = headerTimestamp.TotalSeconds;
        long toleranceSec = (long)tolerance.TotalSeconds;

        long minValidSec = currentSec >= (long.MinValue + toleranceSec) ? currentSec - toleranceSec : long.MinValue;
        long maxValidSec = currentSec <= (long.MaxValue - toleranceSec) ? currentSec + toleranceSec : long.MaxValue;

        return headerSec >= minValidSec && headerSec <= maxValidSec;
    }
}