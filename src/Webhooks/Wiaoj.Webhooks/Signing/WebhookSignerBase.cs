using System.Buffers.Text;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Webhooks.Signing;

/// <summary>
/// Abstract base class for all webhook signers providing canonical payload formatting,
/// parameter validation, and clock drift tolerance checks.
/// </summary>
public abstract class WebhookSignerBase : IWebhookSigner {
    /// <summary>The default HTTP header name used for webhook signatures.</summary>
    public const string DefaultHeaderName = WebhookHeaderNames.WebhookSignature;

    /// <summary>The separator character between key-value pairs in the signature header.</summary>
    public const char PairSeparator = ',';

    /// <summary>The separator character between key and value in the signature header.</summary>
    public const char KeyValueSeparator = '=';

    /// <summary>The prefix identifying the timestamp component in signature headers.</summary>
    public const string TimestampPrefix = "t=";

    /// <summary>The canonical separator byte (ASCII '.') used between timestamp and payload.</summary>
    protected const byte CanonicalPayloadDelimiter = (byte)'.';

    private const int TimestampStackBufferSize = 32;
    private const int SignedBytesStackBufferSize = 256;

    /// <inheritdoc/>
    public abstract string AlgorithmName { get; }

    /// <inheritdoc/>
    public string HeaderName { get; }

    /// <inheritdoc/>
    public abstract string SchemePrefix { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookSignerBase"/> class using the default header name.
    /// </summary>
    protected WebhookSignerBase() : this(DefaultHeaderName) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookSignerBase"/> class using a custom header name.
    /// </summary>
    /// <param name="headerName">The custom HTTP header name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="headerName"/> is null, empty, or whitespace.</exception>
    protected WebhookSignerBase(string headerName) {
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
    /// Formats the canonical byte sequence to be signed or verified in the format <c>{timestamp}.{payload}</c>.
    /// </summary>
    /// <param name="payload">The raw payload byte span.</param>
    /// <param name="timestamp">The generation timestamp.</param>
    /// <param name="initialBuffer">A stack-allocated span for initial buffer storage.</param>
    /// <returns>A <see cref="ValueBuffer{Byte}"/> containing the formatted canonical slice.</returns>
    /// <exception cref="InvalidOperationException">Thrown when timestamp formatting fails.</exception>
    protected static ValueBuffer<byte> CreateSignedBytes(
        ReadOnlySpan<byte> payload,
        UnixTimestamp timestamp,
        Span<byte> initialBuffer) {

        Span<byte> timestampBuf = stackalloc byte[TimestampStackBufferSize];
        if(!Utf8Formatter.TryFormat(timestamp.TotalSeconds, timestampBuf, out int bytesWritten)) {
            throw new InvalidOperationException("Failed to format timestamp as UTF-8.");
        }

        int totalLength = bytesWritten + 1 + payload.Length;
        ValueBuffer<byte> buffer = new(totalLength, initialBuffer);

        Span<byte> span = buffer.Span;
        timestampBuf[..bytesWritten].CopyTo(span);
        span[bytesWritten] = CanonicalPayloadDelimiter;
        payload.CopyTo(span[(bytesWritten + 1)..]);

        return buffer;
    }

    /// <summary>
    /// Validates clock skew tolerance and parses candidate signature slice ranges from the header.
    /// </summary>
    /// <param name="signatureHeader">The signature header span.</param>
    /// <param name="tolerance">The maximum allowable clock drift.</param>
    /// <param name="currentTimestamp">The current reference timestamp.</param>
    /// <param name="headerTimestamp">When this method returns, contains the timestamp extracted from the header.</param>
    /// <param name="signatures">When this method returns, contains the slice ranges of candidate signatures.</param>
    /// <returns><see langword="true"/> if parameters and timestamp are valid; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is negative.</exception>
    protected bool ValidateVerificationParameters(
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
        ReadOnlySpan<char> schemePrefix = this.SchemePrefix;
        int schemeWithEqualsLength = schemePrefix.Length + 1; // "v1" + "=" = 3

        foreach(Range segmentRange in header.Split(PairSeparator)) {
            ReadOnlySpan<char> rawPart = header[segmentRange];
            ReadOnlySpan<char> part = rawPart.Trim();

            if(part.IsEmpty) {
                continue;
            }

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
            else if(part.Length >= schemeWithEqualsLength &&
                     part[schemePrefix.Length] == KeyValueSeparator &&
                     part.StartsWith(schemePrefix, StringComparison.OrdinalIgnoreCase)) {

                int partStartInHeader = segmentRange.Start.Value + (rawPart.Length - rawPart.TrimStart().Length);
                int sigValStart = partStartInHeader + schemeWithEqualsLength;
                ReadOnlySpan<char> sigVal = part[schemeWithEqualsLength..];
                int sigValTrimmedStart = sigValStart + (sigVal.Length - sigVal.TrimStart().Length);
                ReadOnlySpan<char> sigValTrimmed = sigVal.Trim();

                if(!sigValTrimmed.IsEmpty) {
                    signatures.Add(new Range(sigValTrimmedStart, sigValTrimmedStart + sigValTrimmed.Length));
                }
            }
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