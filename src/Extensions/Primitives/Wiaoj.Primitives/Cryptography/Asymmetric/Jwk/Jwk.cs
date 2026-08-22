using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Collections;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents an immutable JSON Web Key (JWK) as defined in RFC 7517, RFC 7518, and RFC 8037.
/// </summary>
/// <remarks>
/// <para>
/// <b>Construction:</b> Use <see cref="JwkBuilder"/> to construct validated instances with a fluent API.
/// </para>
/// <para>
/// <b>RFC 7638 Thumbprint:</b> Provides hardware-accelerated, canonical thumbprint computation 
/// using <see cref="Sha256Hash"/> without heap allocations.
/// </para>
/// <para>
/// <b>Key Safety:</b> Supports separating private parameters using <see cref="AsPublicKey"/> to prevent 
/// accidental leakage of private key material to JWKS endpoints.
/// </para>
/// </remarks>
[DebuggerDisplay("Jwk ({KeyType}, kid: {KeyId ?? \"<none>\"}, use: {PublicKeyUse ?? \"<none>\"})")]
public sealed record Jwk {

    // ── Common Parameters (RFC 7517 Section 4) ────────────────────────────────

    /// <summary>Gets the cryptographic algorithm family used with the key (e.g., "RSA", "EC", "OKP", "oct").</summary>
    [JsonPropertyName("kty")]
    [JsonPropertyOrder(1)]
    public string KeyType { get; init; } = string.Empty;

    /// <summary>Gets the intended use of the public key (e.g., "sig" for signature, "enc" for encryption).</summary>
    [JsonPropertyName("use")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(2)]
    public string? PublicKeyUse { get; init; }

    /// <summary>Gets the operation values that the key is intended to be used for.</summary>
    [JsonPropertyName("key_ops")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(3)]
    public EquatableArray<string>? KeyOps { get; init; }

    /// <summary>Gets the algorithm intended for use with the key (e.g., "RS256", "ES256", "EdDSA").</summary>
    [JsonPropertyName("alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(4)]
    public string? Algorithm { get; init; }

    /// <summary>Gets the unique Key ID used to match a specific key.</summary>
    [JsonPropertyName("kid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(5)]
    public string? KeyId { get; init; }

    // ── RSA Parameters (RFC 7518 Section 6.3) ─────────────────────────────────

    /// <summary>Gets the RSA Modulus (n), Base64Url-encoded.</summary>
    [JsonPropertyName("n")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Modulus { get; init; }

    /// <summary>Gets the RSA Exponent (e), Base64Url-encoded.</summary>
    [JsonPropertyName("e")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Exponent { get; init; }

    /// <summary>Gets the RSA Private Exponent (d), Base64Url-encoded.</summary>
    [JsonPropertyName("d")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateExponent { get; init; }

    /// <summary>Gets the RSA First Prime Factor (p), Base64Url-encoded.</summary>
    [JsonPropertyName("p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstPrimeFactor { get; init; }

    /// <summary>Gets the RSA Second Prime Factor (q), Base64Url-encoded.</summary>
    [JsonPropertyName("q")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecondPrimeFactor { get; init; }

    /// <summary>Gets the RSA First Factor CRT Exponent (dp), Base64Url-encoded.</summary>
    [JsonPropertyName("dp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstFactorCrtExponent { get; init; }

    /// <summary>Gets the RSA Second Factor CRT Exponent (dq), Base64Url-encoded.</summary>
    [JsonPropertyName("dq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecondFactorCrtExponent { get; init; }

    /// <summary>Gets the RSA First CRT Coefficient (qi), Base64Url-encoded.</summary>
    [JsonPropertyName("qi")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstCrtCoefficient { get; init; }

    // ── Elliptic Curve & OKP Parameters (RFC 7518 Section 6.2 & RFC 8037) ──────

    /// <summary>Gets the cryptographic curve name (e.g., "P-256", "P-384", "P-521", "Ed25519").</summary>
    [JsonPropertyName("crv")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Curve { get; init; }

    /// <summary>Gets the X Coordinate / Public Key parameter for EC/OKP, Base64Url-encoded.</summary>
    [JsonPropertyName("x")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? X { get; init; }

    /// <summary>Gets the Y Coordinate for EC keys, Base64Url-encoded.</summary>
    [JsonPropertyName("y")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Y { get; init; }

    // ── Symmetric / Octet Sequence Parameters (RFC 7518 Section 6.4) ──────────

    /// <summary>Gets the symmetric key material (k) for "oct" keys, Base64Url-encoded.</summary>
    [JsonPropertyName("k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeyValue { get; init; }

    // ── State Inspection ──────────────────────────────────────────────────────

    /// <summary>
    /// Gets a value indicating whether this JWK contains any private key components.
    /// </summary>
    [JsonIgnore]
    public bool HasPrivateKey =>
        this.PrivateExponent is not null ||
        this.FirstPrimeFactor is not null ||
        this.KeyValue is not null;

    // ── Public Projection ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a new <see cref="Jwk"/> instance containing only the public parameters of this key.
    /// Strips all private parameters (<c>d</c>, <c>p</c>, <c>q</c>, <c>dp</c>, <c>dq</c>, <c>qi</c>, <c>k</c>).
    /// </summary>
    /// <returns>A sanitized public-only <see cref="Jwk"/> safe for exposure via JWKS.</returns>
    public Jwk AsPublicKey() {
        if(!this.HasPrivateKey) return this;

        return this with {
            PrivateExponent = null,
            FirstPrimeFactor = null,
            SecondPrimeFactor = null,
            FirstFactorCrtExponent = null,
            SecondFactorCrtExponent = null,
            FirstCrtCoefficient = null,
            KeyValue = null
        };
    }

    // ── RFC 7638 Thumbprint Computation ───────────────────────────────────────

    /// <summary>
    /// Computes the SHA-256 JWK Thumbprint hash as defined in RFC 7638.
    /// </summary>
    /// <returns>The computed <see cref="Sha256Hash"/> of the canonical JSON representation.</returns>
    /// <exception cref="NotSupportedException">Thrown when <see cref="KeyType"/> is unsupported for thumbprint calculation.</exception>
    public Sha256Hash ComputeThumbprintHash() {
        string canonicalJson = this.KeyType switch {
            "RSA" => $"{{\"e\":\"{this.Exponent}\",\"kty\":\"RSA\",\"n\":\"{this.Modulus}\"}}",
            "EC" => $"{{\"crv\":\"{this.Curve}\",\"kty\":\"EC\",\"x\":\"{this.X}\",\"y\":\"{this.Y}\"}}",
            "OKP" => $"{{\"crv\":\"{this.Curve}\",\"kty\":\"OKP\",\"x\":\"{this.X}\"}}",
            "oct" => $"{{\"k\":\"{this.KeyValue}\",\"kty\":\"oct\"}}",
            _ => throw new NotSupportedException($"JWK thumbprint calculation is not supported for KeyType '{this.KeyType}'.")
        };

        return Sha256Hash.Compute(canonicalJson, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the Base64Url-encoded SHA-256 JWK Thumbprint (RFC 7638), commonly used as a <c>kid</c> or in DPoP confirmation claims.
    /// </summary>
    /// <returns>A <see cref="Base64UrlString"/> containing the thumbprint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base64UrlString ComputeThumbprint() {
        return ComputeThumbprintHash().ToBase64UrlString();
    }

    // ── Serialization & Parsing ───────────────────────────────────────────────

    /// <summary>
    /// Serializes this JSON Web Key (JWK) into a compact UTF-8 JSON string without indentation.
    /// </summary>
    /// <returns>A compact JSON string representation of the JWK.</returns>
    public string ToJsonString() {
        return ToJsonString(false);
    }

    /// <summary>
    /// Serializes this JSON Web Key (JWK) into a UTF-8 JSON string.
    /// </summary>
    /// <param name="indented"><see langword="true"/> to format the JSON output with indentation; otherwise, <see langword="false"/>.</param>
    /// <returns>A JSON string representation of the JWK.</returns>
    public string ToJsonString(bool indented) {
        JwkJsonSerializerContext context = indented
            ? JwkJsonSerializerContext.Indented
            : JwkJsonSerializerContext.Compact;

        return JsonSerializer.Serialize(this, context.Jwk);
    }

    /// <summary>
    /// Parses a JSON string into a <see cref="Jwk"/> instance.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <returns>The parsed <see cref="Jwk"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is null or whitespace.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static Jwk Parse(string json) {
        Preca.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize(json, JwkJsonSerializerContext.Compact.Jwk)
               ?? throw new JsonException("Failed to deserialize JWK payload.");
    }

    /// <summary>
    /// Parses a UTF-8 encoded JSON byte span into a <see cref="Jwk"/> instance without intermediate string allocations.
    /// </summary>
    /// <param name="utf8Json">The read-only span of UTF-8 encoded bytes representing the JSON payload.</param>
    /// <returns>The parsed <see cref="Jwk"/> instance.</returns>
    /// <exception cref="JsonException">Thrown when the JSON payload is invalid or fails to deserialize.</exception>
    public static Jwk Parse(ReadOnlySpan<byte> utf8Json) {
        return JsonSerializer.Deserialize(utf8Json, JwkJsonSerializerContext.Compact.Jwk)
               ?? throw new JsonException("Failed to deserialize JWK payload.");
    }

    /// <summary>
    /// Attempts to parse a JSON string into a <see cref="Jwk"/> instance without throwing exceptions on failure.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <param name="result">When this method returns, contains the parsed key if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? json, [NotNullWhen(true)] out Jwk? result) {
        if(string.IsNullOrWhiteSpace(json)) {
            result = null;
            return false;
        }

        try {
            result = JsonSerializer.Deserialize(json, JwkJsonSerializerContext.Compact.Jwk);
            return result is not null;
        }
        catch {
            result = null;
            return false;
        }
    }
}