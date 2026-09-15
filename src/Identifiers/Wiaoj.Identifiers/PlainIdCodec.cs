using Wiaoj.Preconditions;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers;

/// <summary>
/// Writes an identifier as <c>prefix_</c> followed by its Snowflake value in base62 — short, readable back without a
/// key, but not secret.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not an obfuscation.</b> A Snowflake value contains its creation time, so anyone holding the text can read when the
/// identifier was created, and consecutive identifiers reveal how many were created in between. Use it where that is
/// acceptable, such as internal services; use <see cref="AesIdCodec"/> for identifiers shown to the public.
/// </para>
/// <para>
/// The value is written with no leading zeros and read back only in that form, so each identifier has exactly one
/// spelling — two strings never name the same identifier in a cache key or a comparison.
/// </para>
/// </remarks>
public sealed class PlainIdCodec : IdCodec {
    /// <summary>Gets the shared instance; the codec holds no state.</summary>
    public static PlainIdCodec Instance { get; } = new();

    /// <inheritdoc/>
    protected override bool IsEquivalentTo(IdCodec other) => other is PlainIdCodec;

    /// <inheritdoc/>
    public override int GetMaxEncodedLength(string prefix) {
        Preca.ThrowIfNull(prefix);
        return prefix.Length + 1 + Base62.MaxUInt64Length;
    }

    /// <inheritdoc/>
    public override bool TryEncode(string prefix, SnowflakeId value, Span<char> destination, out int charsWritten) {
        Preca.ThrowIfNull(prefix);

        Span<char> digits = stackalloc char[Base62.MaxUInt64Length];
        int count = Base62.WriteUInt64((ulong)value.Value, digits);

        if(!TryWritePrefix(prefix, destination, out Span<char> payload) || payload.Length < count) {
            charsWritten = 0;
            return false;
        }

        digits[..count].CopyTo(payload);
        charsWritten = prefix.Length + 1 + count;
        return true;
    }

    /// <inheritdoc/>
    public override bool TryDecode(string prefix, ReadOnlySpan<char> text, out SnowflakeId value) {
        Preca.ThrowIfNull(prefix);

        ReadOnlySpan<char> payload = PayloadAfterPrefix(prefix, text, out bool matched);
        if(matched && Base62.TryReadUInt64(payload, out ulong raw)) {
            value = new SnowflakeId((long)raw);
            return true;
        }

        value = default;
        return false;
    }
}
