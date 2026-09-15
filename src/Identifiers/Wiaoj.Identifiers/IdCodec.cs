using System.Diagnostics.CodeAnalysis;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers;

/// <summary>
/// Writes identifiers as text and reads them back: <c>prefix_</c> followed by a payload the codec defines.
/// </summary>
/// <remarks>
/// <para>
/// Two codecs are provided. <see cref="PlainIdCodec"/> writes the Snowflake value itself, so the text is short and sorts
/// by creation, but reveals when the identifier was created. <see cref="AesIdCodec"/> encrypts it under a key, so the
/// text reveals nothing and a forged or altered identifier is refused.
/// </para>
/// <para>
/// Generated identifiers use <see cref="Current"/>, so <c>id.ToString()</c>, <c>UserId.Parse(text)</c>, route binding,
/// JSON and type conversion work without injecting anything. <c>AddIdentifiers</c> installs the codec from the
/// container when the host starts. A codec can also be used directly, for example by a tool that reads identifiers
/// written under several keys.
/// </para>
/// </remarks>
public abstract class IdCodec {
    /// <summary>The character between the prefix and the payload.</summary>
    public const char Separator = '_';

    /// <summary>The longest prefix an identifier may have.</summary>
    public const int MaxPrefixLength = 32;

    private static readonly AsyncLocal<IdCodec?> OverrideCodec = new();
    private static IdCodec? _installed;

    /// <summary>
    /// Gets the codec generated identifiers use: the one overridden for the current asynchronous flow, otherwise the
    /// installed one.
    /// </summary>
    /// <exception cref="InvalidOperationException">No codec has been installed.</exception>
    public static IdCodec Current =>
        OverrideCodec.Value
        ?? Volatile.Read(ref _installed)
        ?? throw new InvalidOperationException(
            "No identifier codec is installed. Call services.AddIdentifiers(...) and start the host, or call " +
            "IdCodec.Install(...) at startup, before identifiers are written or read.");

    /// <summary>
    /// Installs the codec generated identifiers use, for the lifetime of the process.
    /// </summary>
    /// <param name="codec">The codec.</param>
    /// <exception cref="InvalidOperationException">A different codec is already installed.</exception>
    /// <remarks>
    /// Installing once and refusing a second, different codec keeps identifiers already written readable: a codec
    /// swapped while running would stop reading everything issued before it. Installing the same codec again — the same
    /// instance, or one that reads and writes identically, such as an <see cref="AesIdCodec"/> with the same key and
    /// version — is allowed, so several hosts in one process (integration tests) do not fail. To use a different codec in
    /// a test, use <see cref="Override"/>.
    /// </remarks>
    public static void Install(IdCodec codec) {
        Preca.ThrowIfNull(codec);

        IdCodec? existing = Interlocked.CompareExchange(ref _installed, codec, null);
        if(existing is not null && !ReferenceEquals(existing, codec) && !existing.IsEquivalentTo(codec)) {
            throw new InvalidOperationException(
                $"An identifier codec ({existing.GetType().Name}) is already installed. The codec cannot change while the " +
                "process runs, or identifiers written before the change could no longer be read.");
        }
    }

    /// <summary>
    /// Uses <paramref name="codec"/> instead of the installed codec in the current asynchronous flow until the returned
    /// scope is disposed — for tests, which run in parallel with different codecs.
    /// </summary>
    /// <param name="codec">The codec to use.</param>
    /// <returns>A scope that restores the previous codec when disposed.</returns>
    public static IDisposable Override(IdCodec codec) {
        Preca.ThrowIfNull(codec);

        IdCodec? previous = OverrideCodec.Value;
        OverrideCodec.Value = codec;
        return new OverrideScope(previous);
    }

    /// <summary>Clears the installed codec, for tests of installation itself.</summary>
    internal static void ResetInstalled() => Volatile.Write(ref _installed, null);

    /// <summary>
    /// Returns whether <paramref name="prefix"/> is a valid identifier prefix: 1–32 lowercase ASCII letters and digits,
    /// starting with a letter, optionally separated by single underscores.
    /// </summary>
    /// <param name="prefix">The prefix to check.</param>
    /// <returns><see langword="true"/> when the prefix is valid.</returns>
    public static bool IsValidPrefix([NotNullWhen(true)] string? prefix) {
        if(string.IsNullOrEmpty(prefix) || prefix.Length > MaxPrefixLength || prefix[0] is < 'a' or > 'z' || prefix[^1] == Separator) {
            return false;
        }

        for(int i = 1; i < prefix.Length; i++) {
            char c = prefix[i];
            bool allowed = c is (>= 'a' and <= 'z') or (>= '0' and <= '9') || (c == Separator && prefix[i - 1] != Separator);
            if(!allowed) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns whether <paramref name="other"/> writes and reads exactly the same text as this codec, so installing it
    /// in place of this one changes nothing.
    /// </summary>
    /// <param name="other">The other codec.</param>
    /// <returns><see langword="true"/> when the codecs are interchangeable. The default compares references.</returns>
    protected virtual bool IsEquivalentTo(IdCodec other) => ReferenceEquals(this, other);

    /// <summary>Gets the longest text this codec writes for an identifier with <paramref name="prefix"/>.</summary>
    /// <param name="prefix">The identifier's prefix.</param>
    /// <returns>The length in characters.</returns>
    public abstract int GetMaxEncodedLength(string prefix);

    /// <summary>Writes <paramref name="value"/> with <paramref name="prefix"/> into <paramref name="destination"/>.</summary>
    /// <param name="prefix">The identifier's prefix.</param>
    /// <param name="value">The identifier's value.</param>
    /// <param name="destination">Receives the text.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <returns><see langword="false"/> when <paramref name="destination"/> is too small.</returns>
    public abstract bool TryEncode(string prefix, SnowflakeId value, Span<char> destination, out int charsWritten);

    /// <summary>
    /// Reads an identifier with <paramref name="prefix"/> from <paramref name="text"/>.
    /// </summary>
    /// <param name="prefix">The prefix the text must start with.</param>
    /// <param name="text">The text.</param>
    /// <param name="value">The identifier's value when the text is valid.</param>
    /// <returns>
    /// <see langword="true"/> only for text this codec writes for <paramref name="prefix"/>: another prefix, another
    /// spelling of the same value, or a payload the codec rejects all return <see langword="false"/>.
    /// </returns>
    public abstract bool TryDecode(string prefix, ReadOnlySpan<char> text, out SnowflakeId value);

    /// <summary>Returns <paramref name="value"/> with <paramref name="prefix"/> as text.</summary>
    /// <param name="prefix">The identifier's prefix.</param>
    /// <param name="value">The identifier's value.</param>
    /// <returns>The text.</returns>
    public string Encode(string prefix, SnowflakeId value) {
        Preca.ThrowIfNull(prefix);

        int length = this.GetMaxEncodedLength(prefix);
        Span<char> buffer = length <= 128 ? stackalloc char[length] : new char[length];
        if(!this.TryEncode(prefix, value, buffer, out int written)) {
            throw new InvalidOperationException($"{this.GetType().Name} wrote more than its maximum encoded length.");
        }

        return new string(buffer[..written]);
    }

    /// <summary>Reads an identifier with <paramref name="prefix"/> from UTF-8 <paramref name="utf8Text"/>.</summary>
    /// <param name="prefix">The prefix the text must start with.</param>
    /// <param name="utf8Text">The UTF-8 text.</param>
    /// <param name="value">The identifier's value when the text is valid.</param>
    /// <returns><see langword="true"/> when the text is valid; see <see cref="TryDecode(string, ReadOnlySpan{char}, out SnowflakeId)"/>.</returns>
    public bool TryDecode(string prefix, ReadOnlySpan<byte> utf8Text, out SnowflakeId value) {
        Preca.ThrowIfNull(prefix);

        // Every valid identifier is ASCII, so bytes map to characters one to one; anything longer is invalid.
        if(utf8Text.Length > this.GetMaxEncodedLength(prefix)) {
            value = default;
            return false;
        }

        Span<char> chars = stackalloc char[utf8Text.Length];
        for(int i = 0; i < utf8Text.Length; i++) {
            if(utf8Text[i] > 0x7F) {
                value = default;
                return false;
            }

            chars[i] = (char)utf8Text[i];
        }

        return this.TryDecode(prefix, chars, out value);
    }

    /// <summary>Returns <paramref name="id"/> as text.</summary>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="id">The identifier.</param>
    /// <returns>The text.</returns>
    public string Encode<TId>(TId id) where TId : struct, IIdentifier<TId> => this.Encode(TId.Prefix, id.Value);

    /// <summary>Reads an identifier of type <typeparamref name="TId"/> from <paramref name="text"/>.</summary>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="text">The text.</param>
    /// <param name="id">The identifier when the text is valid.</param>
    /// <returns><see langword="true"/> when the text is valid.</returns>
    public bool TryDecode<TId>(ReadOnlySpan<char> text, out TId id) where TId : struct, IIdentifier<TId> {
        if(this.TryDecode(TId.Prefix, text, out SnowflakeId value)) {
            id = TId.From(value);
            return true;
        }

        id = default;
        return false;
    }

    /// <summary>
    /// Returns the payload after <paramref name="prefix"/> and the separator; <paramref name="matched"/> is
    /// <see langword="false"/> when <paramref name="text"/> does not start with them.
    /// </summary>
    protected static ReadOnlySpan<char> PayloadAfterPrefix(string prefix, ReadOnlySpan<char> text, out bool matched) {
        matched = text.Length > prefix.Length && text.StartsWith(prefix, StringComparison.Ordinal) && text[prefix.Length] == Separator;
        return matched ? text[(prefix.Length + 1)..] : default;
    }

    /// <summary>Writes <paramref name="prefix"/> and the separator, returning the rest of <paramref name="destination"/>.</summary>
    protected static bool TryWritePrefix(string prefix, Span<char> destination, out Span<char> payload) {
        if(destination.Length < prefix.Length + 1) {
            payload = default;
            return false;
        }

        prefix.CopyTo(destination);
        destination[prefix.Length] = Separator;
        payload = destination[(prefix.Length + 1)..];
        return true;
    }

    private sealed class OverrideScope(IdCodec? previous) : IDisposable {
        private int _disposed;

        public void Dispose() {
            if(Interlocked.Exchange(ref this._disposed, 1) == 0) {
                OverrideCodec.Value = previous;
            }
        }
    }
}
