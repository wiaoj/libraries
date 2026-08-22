using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents a JSON Web Key Set (JWKS) document as defined in RFC 7517 Section 5.
/// </summary>
/// <remarks>
/// Utilizes <see cref="EquatableArray{T}"/> to ensure immutable, allocation-free element comparisons and thread-safe publication.
/// </remarks>
[DebuggerDisplay("JwksDocument (Count = {Keys.Count})")]
public sealed record JwksDocument : IReadOnlyList<Jwk> {

    /// <summary>
    /// Gets an empty <see cref="JwksDocument"/> instance.
    /// </summary>
    public static JwksDocument Empty { get; } = new([]);

    /// <summary>
    /// Gets the array of <see cref="Jwk"/> elements contained in this set.
    /// </summary>
    [JsonPropertyName("keys")]
    public EquatableArray<Jwk> Keys { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="JwksDocument"/> with the specified collection of keys.
    /// </summary>
    /// <param name="keys">The array of keys.</param>
    [JsonConstructor]
    public JwksDocument(EquatableArray<Jwk> keys) {
        this.Keys = keys;
    }

    // ── Explicit Factory Methods ──────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="JwksDocument"/> from a span of keys.
    /// </summary>
    /// <param name="keys">The span of <see cref="Jwk"/> elements.</param>
    /// <returns>A new <see cref="JwksDocument"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JwksDocument Create(params ReadOnlySpan<Jwk> keys) {
        return new JwksDocument(EquatableArray.Create(keys));
    }

    /// <summary>
    /// Creates a <see cref="JwksDocument"/> from an existing <see cref="EquatableArray{Jwk}"/>.
    /// </summary>
    /// <param name="keys">The equatable array of keys.</param>
    /// <returns>A new <see cref="JwksDocument"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JwksDocument Create(EquatableArray<Jwk> keys) {
        return new JwksDocument(keys);
    }

    /// <summary>
    /// Creates a <see cref="JwksDocument"/> from an enumerable collection of keys.
    /// </summary>
    /// <param name="keys">The collection of keys.</param>
    /// <returns>A new <see cref="JwksDocument"/> instance.</returns>
    public static JwksDocument Create(IEnumerable<Jwk>? keys) {
        return new JwksDocument(keys.ToEquatableArray());
    }

    // ── Search & Key Retrieval ────────────────────────────────────────────────

    /// <summary>
    /// Finds the first key matching the specified Key ID (<c>kid</c>).
    /// </summary>
    /// <param name="keyId">The key identifier to match.</param>
    /// <returns>The matching <see cref="Jwk"/>, or <see langword="null"/> if not found.</returns>
    public Jwk? FindKey(string keyId) {
        Preca.ThrowIfNullOrWhiteSpace(keyId);

        foreach(Jwk key in this.Keys) {
            if(string.Equals(key.KeyId, keyId, StringComparison.Ordinal)) {
                return key;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns a new <see cref="JwksDocument"/> containing only public key components, 
    /// ensuring no private key material is published.
    /// </summary>
    /// <returns>A sanitized public-only <see cref="JwksDocument"/>.</returns>
    public JwksDocument AsPublicDocument() {
        bool needsSanitization = false;
        foreach(Jwk key in this.Keys) {
            if(key.HasPrivateKey) {
                needsSanitization = true;
                break;
            }
        }

        if(!needsSanitization) return this;

        Jwk[] sanitized = new Jwk[this.Keys.Count];
        for(int i = 0; i < this.Keys.Count; i++) {
            sanitized[i] = this.Keys[i].AsPublicKey();
        }

        return new JwksDocument(EquatableArray.Create<Jwk>(sanitized));
    }

    // ── IReadOnlyList<Jwk> Implementation ─────────────────────────────────────

    /// <summary>Gets the total number of keys in the document.</summary>
    public int Count => this.Keys.Count;

    /// <summary>Gets the <see cref="Jwk"/> at the specified index.</summary>
    public Jwk this[int index] => this.Keys[index];

    /// <summary>Returns an enumerator that iterates through the keys.</summary>
    public IEnumerator<Jwk> GetEnumerator() {
        return ((IEnumerable<Jwk>)this.Keys).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    // ── Serialization & Parsing ───────────────────────────────────────────────

    /// <summary>
    /// Serializes this JWKS document into a compact UTF-8 JSON string without indentation.
    /// </summary>
    /// <returns>A compact JSON string representation of the JWKS document.</returns>
    public string ToJsonString() {
        return ToJsonString(false);
    }

    /// <summary>
    /// Serializes this JWKS document into a UTF-8 JSON string.
    /// </summary>
    /// <param name="indented"><see langword="true"/> to format the JSON output with indentation; otherwise, <see langword="false"/>.</param>
    /// <returns>A JSON string representation of the JWKS document.</returns>
    public string ToJsonString(bool indented) {
        JwkJsonSerializerContext context = indented
            ? JwkJsonSerializerContext.Indented
            : JwkJsonSerializerContext.Compact;

        return JsonSerializer.Serialize(this, context.JwksDocument);
    }

    /// <summary>
    /// Parses a JSON string into a <see cref="JwksDocument"/>.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <returns>The parsed <see cref="JwksDocument"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is null or whitespace.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static JwksDocument Parse(string json) {
        Preca.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize(json, JwkJsonSerializerContext.Compact.JwksDocument)
               ?? throw new JsonException("Failed to deserialize JWKS document.");
    }

    /// <summary>
    /// Parses a UTF-8 encoded JSON byte span into a <see cref="JwksDocument"/> instance without intermediate string allocations.
    /// </summary>
    /// <param name="utf8Json">The read-only span of UTF-8 encoded bytes representing the JSON payload.</param>
    /// <returns>The parsed <see cref="JwksDocument"/> instance.</returns>
    /// <exception cref="JsonException">Thrown when the JSON payload is invalid or fails to deserialize.</exception>
    public static JwksDocument Parse(ReadOnlySpan<byte> utf8Json) {
        return JsonSerializer.Deserialize(utf8Json, JwkJsonSerializerContext.Compact.JwksDocument)
               ?? throw new JsonException("Failed to deserialize JWKS document.");
    }

    /// <summary>
    /// Attempts to parse a JSON string into a <see cref="JwksDocument"/> without throwing exceptions on failure.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <param name="result">When this method returns, contains the parsed document if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? json, [NotNullWhen(true)] out JwksDocument? result) {
        if(string.IsNullOrWhiteSpace(json)) {
            result = null;
            return false;
        }

        try {
            result = JsonSerializer.Deserialize(json, JwkJsonSerializerContext.Compact.JwksDocument);
            return result is not null;
        }
        catch {
            result = null;
            return false;
        }
    }
}