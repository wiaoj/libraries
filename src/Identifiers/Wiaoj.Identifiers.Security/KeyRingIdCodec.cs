using System.Collections.Concurrent;
using System.Security.Cryptography;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Snowflake;
using Wiaoj.Security;

namespace Wiaoj.Identifiers.Security;

/// <summary>
/// Encrypts identifiers like <see cref="AesIdCodec"/>, keyed by a Wiaoj.Security key ring: new identifiers use the ring's
/// current key version, and identifiers written under an earlier version stay readable while that version is in the ring.
/// </summary>
/// <remarks>
/// <para>
/// Each key version gets its own <see cref="AesIdCodec"/>, keyed by a subkey derived for <see cref="SubkeyPurpose"/> —
/// the ring's keys are never exposed. The version character written into an identifier is the base62 digit of the
/// version modulo 62 (<c>1</c>…<c>9</c>, <c>A</c>…<c>Z</c>, <c>a</c>…<c>z</c>, then <c>0</c>). When two versions in the
/// ring share a character, both are tried; the tag check refuses the one that did not write the identifier.
/// </para>
/// <para>
/// <b>Rotation.</b> With <c>AddManagedProtector</c>, a rotation reloads the ring, and this codec follows it: identifiers
/// written afterwards carry the new version. An identifier written under a version that is later removed from the ring
/// can no longer be read — keep retired versions for as long as their identifiers are in circulation.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The secret domain whose key ring keys identifiers.</typeparam>
public sealed class KeyRingIdCodec<TContext> : IdCodec where TContext : ISecretContext {
    /// <summary>The purpose identifier subkeys are derived for.</summary>
    public const string SubkeyPurpose = "wiaoj.identifiers";

    private const string VersionAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int PayloadLength = 23;

    private static readonly byte[] Purpose = System.Text.Encoding.ASCII.GetBytes(SubkeyPurpose);

    private readonly ISubkeyDeriver<TContext> _keys;
    private readonly ConcurrentDictionary<int, AesIdCodec> _codecs = new();

    /// <summary>Creates the codec.</summary>
    /// <param name="keys">Derives subkeys from the domain's key ring; with <c>AddManagedProtector</c>, it follows rotation.</param>
    public KeyRingIdCodec(ISubkeyDeriver<TContext> keys) {
        Preca.ThrowIfNull(keys);
        this._keys = keys;
    }

    /// <summary>Returns the character written into identifiers for <paramref name="version"/>.</summary>
    /// <param name="version">The key version.</param>
    /// <returns>The base62 digit of the version modulo 62.</returns>
    public static char VersionCharacter(KeyVersion version) => VersionAlphabet[version.Value % VersionAlphabet.Length];

    /// <inheritdoc/>
    /// <remarks>Equivalent when both read keys from the same <see cref="ISubkeyDeriver{TContext}"/>.</remarks>
    public override bool IsEquivalentTo(IdCodec other) {
        return other is KeyRingIdCodec<TContext> ring && ReferenceEquals(ring._keys, this._keys);
    }

    /// <inheritdoc/>
    public override int GetMaxEncodedLength(string prefix) {
        Preca.ThrowIfNull(prefix);
        return prefix.Length + 1 + PayloadLength;
    }

    /// <inheritdoc/>
    public override bool TryEncode(string prefix, SnowflakeId value, Span<char> destination, out int charsWritten) {
        return this.CodecFor(this._keys.CurrentKeyVersion).TryEncode(prefix, value, destination, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryDecode(string prefix, ReadOnlySpan<char> text, out SnowflakeId value) {
        Preca.ThrowIfNull(prefix);
        value = default;

        ReadOnlySpan<char> payload = PayloadAfterPrefix(prefix, text, out bool matched);
        if(!matched || payload.Length != PayloadLength) {
            return false;
        }

        foreach(KeyVersion version in this._keys.KeyVersions) {
            if(VersionCharacter(version) == payload[0] && this.CodecFor(version).TryDecode(prefix, text, out value)) {
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>The codec for <paramref name="version"/>, keyed by a subkey derived once per version.</summary>
    private AesIdCodec CodecFor(KeyVersion version) {
        return this._codecs.GetOrAdd(version.Value, static (_, state) => {
            (ISubkeyDeriver<TContext> keys, KeyVersion keyVersion) = state;
            Span<byte> subkey = stackalloc byte[32];
            try {
                keys.DeriveSubkey(keyVersion, Purpose, subkey);
                return new AesIdCodec(subkey, VersionCharacter(keyVersion));
            }
            finally {
                CryptographicOperations.ZeroMemory(subkey);
            }
        }, (this._keys, version));
    }
}
