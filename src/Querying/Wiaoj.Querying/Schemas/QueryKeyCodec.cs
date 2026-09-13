using System.Globalization;

namespace Wiaoj.Querying;

/// <summary>
/// Turns a sort key into text a cursor can carry, and back.
/// </summary>
/// <remarks>
/// Kept free of any paging type so a schema can declare its cursor keys without referencing a paging package.
/// </remarks>
public interface IQueryKeyCodec {
    /// <summary>Gets the key type the codec reads and writes.</summary>
    Type KeyType { get; }

    /// <summary>Writes <paramref name="key"/> as text.</summary>
    string Encode(object key);

    /// <summary>Reads a key written by <see cref="Encode"/>.</summary>
    /// <exception cref="FormatException">The text is not a key this codec wrote.</exception>
    object Decode(string text);
}

/// <summary>
/// A codec for keys of type <typeparamref name="T"/>, from a pair of functions.
/// </summary>
/// <typeparam name="T">The key type.</typeparam>
public sealed class QueryKeyCodec<T> : IQueryKeyCodec {
    private readonly Func<T, string> _encode;
    private readonly Func<string, T> _decode;

    /// <summary>Initializes a codec.</summary>
    /// <param name="encode">Writes a key as text. Must round-trip exactly through <paramref name="decode"/>.</param>
    /// <param name="decode">Reads text written by <paramref name="encode"/>.</param>
    public QueryKeyCodec(Func<T, string> encode, Func<string, T> decode) {
        ArgumentNullException.ThrowIfNull(encode);
        ArgumentNullException.ThrowIfNull(decode);
        this._encode = encode;
        this._decode = decode;
    }

    /// <inheritdoc/>
    public Type KeyType => typeof(T);

    /// <inheritdoc/>
    public string Encode(object key) => this._encode((T)key);

    /// <inheritdoc/>
    public object Decode(string text) {
        try {
            return this._decode(text) ?? throw new FormatException($"'{text}' decoded to a null {typeof(T).Name} cursor key.");
        }
        catch(Exception error) when(error is not FormatException) {
            throw new FormatException($"'{text}' is not a valid {typeof(T).Name} cursor key.", error);
        }
    }
}

/// <summary>
/// The codecs available without declaring one.
/// </summary>
internal static class BuiltInQueryKeyCodecs {
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Returns the built-in codec for <typeparamref name="T"/>, or <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// Every codec round-trips exactly: <c>"R"</c> for floating point, <c>"O"</c> for dates, so a key written into a
    /// cursor compares equal to the column value it came from.
    /// </remarks>
    public static IQueryKeyCodec? For<T>() {
        Type type = typeof(T);

        // An enum is written as its underlying number, which is what it is stored and ordered by.
        if(type.IsEnum) {
            return new QueryKeyCodec<T>(
                v => Convert.ToInt64(v, Invariant).ToString(Invariant),
                t => (T)Enum.ToObject(type, long.Parse(t, Invariant)));
        }

        return Type.GetTypeCode(type) switch {
            TypeCode.String => new QueryKeyCodec<string>(v => v, t => t),
            TypeCode.Int32 => new QueryKeyCodec<int>(v => v.ToString(Invariant), t => int.Parse(t, Invariant)),
            TypeCode.Int64 => new QueryKeyCodec<long>(v => v.ToString(Invariant), t => long.Parse(t, Invariant)),
            TypeCode.Int16 => new QueryKeyCodec<short>(v => v.ToString(Invariant), t => short.Parse(t, Invariant)),
            TypeCode.Byte => new QueryKeyCodec<byte>(v => v.ToString(Invariant), t => byte.Parse(t, Invariant)),
            TypeCode.UInt32 => new QueryKeyCodec<uint>(v => v.ToString(Invariant), t => uint.Parse(t, Invariant)),
            TypeCode.UInt64 => new QueryKeyCodec<ulong>(v => v.ToString(Invariant), t => ulong.Parse(t, Invariant)),
            TypeCode.Decimal => new QueryKeyCodec<decimal>(v => v.ToString(Invariant), t => decimal.Parse(t, Invariant)),
            TypeCode.Double => new QueryKeyCodec<double>(v => v.ToString("R", Invariant), t => double.Parse(t, Invariant)),
            TypeCode.Single => new QueryKeyCodec<float>(v => v.ToString("R", Invariant), t => float.Parse(t, Invariant)),
            TypeCode.Boolean => new QueryKeyCodec<bool>(v => v ? "1" : "0", t => t switch { "1" => true, "0" => false, _ => throw new FormatException() }),
            TypeCode.DateTime => new QueryKeyCodec<DateTime>(v => v.ToString("O", Invariant), t => DateTime.Parse(t, Invariant, DateTimeStyles.RoundtripKind)),
            _ when type == typeof(Guid) => new QueryKeyCodec<Guid>(v => v.ToString("N"), t => Guid.ParseExact(t, "N")),
            _ when type == typeof(DateTimeOffset) => new QueryKeyCodec<DateTimeOffset>(v => v.ToString("O", Invariant), t => DateTimeOffset.Parse(t, Invariant, DateTimeStyles.RoundtripKind)),
            _ when type == typeof(DateOnly) => new QueryKeyCodec<DateOnly>(v => v.DayNumber.ToString(Invariant), t => DateOnly.FromDayNumber(int.Parse(t, Invariant))),
            _ when type == typeof(TimeOnly) => new QueryKeyCodec<TimeOnly>(v => v.Ticks.ToString(Invariant), t => new TimeOnly(long.Parse(t, Invariant))),
            _ when type == typeof(TimeSpan) => new QueryKeyCodec<TimeSpan>(v => v.Ticks.ToString(Invariant), t => new TimeSpan(long.Parse(t, Invariant))),
            _ => null
        };
    }
}

/// <summary>
/// One key of a keyset ordering: what to order by, which way, and how to carry its value in a cursor.
/// </summary>
/// <param name="Name">The name the key is identified by in the cursor — its member path.</param>
/// <param name="Selector">The member to order and seek by.</param>
/// <param name="IsDescending">Whether the key is ordered descending.</param>
/// <param name="Codec">Writes the key's value into a cursor and reads it back.</param>
public sealed record QueryCursorKey(string Name, System.Linq.Expressions.LambdaExpression Selector, bool IsDescending, IQueryKeyCodec Codec);
