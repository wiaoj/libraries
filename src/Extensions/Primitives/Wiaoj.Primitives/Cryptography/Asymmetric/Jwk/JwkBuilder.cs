using System.Diagnostics;
using System.Runtime.CompilerServices;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// A high-performance, fluent builder for constructing validated and immutable <see cref="Jwk"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// <b>Single Allocation:</b> Collects parameters mutably and allocates the final <see cref="Jwk"/> instance 
/// exactly once upon calling <see cref="Build"/>, avoiding intermediate heap clones.
/// </para>
/// <para>
/// <b>RFC Validation:</b> Enforces mandatory parameter constraints according to RFC 7517, RFC 7518, and RFC 8037 during <see cref="Build"/>.
/// </para>
/// </remarks>
[DebuggerDisplay("JwkBuilder (kty: {_kty}, kid: {_kid ?? \"<none>\"}, use: {_use ?? \"<none>\"})")]
public sealed class JwkBuilder {
    private readonly string _kty;
    private string? _kid;
    private string? _alg;
    private string? _use;
    private EquatableArray<string>? _keyOps;

    // ── RSA Parameters ────────────────────────────────────────────────────────
    private Base64UrlString? _n;
    private Base64UrlString? _e;

    // ── EC & OKP Parameters ───────────────────────────────────────────────────
    private string? _crv;
    private Base64UrlString? _x;
    private Base64UrlString? _y;

    // ── Symmetric Parameters ──────────────────────────────────────────────────
    private Base64UrlString? _k;

    private JwkBuilder(string kty) {
        this._kty = kty;
    }

    // ── Static Entry Points (Factories) ───────────────────────────────────────

    /// <summary>
    /// Initializes a builder for an RSA public key with the mandatory Modulus (n) and Exponent (e).
    /// </summary>
    /// <param name="modulus">The Base64Url-encoded RSA modulus parameter (n).</param>
    /// <param name="exponent">The Base64Url-encoded RSA public exponent parameter (e).</param>
    /// <returns>A configured <see cref="JwkBuilder"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modulus"/> or <paramref name="exponent"/> is empty or uninitialized.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JwkBuilder CreateRsa(Base64UrlString modulus, Base64UrlString exponent) {
        Preca.ThrowIfEmpty(modulus);
        Preca.ThrowIfEmpty(exponent);

        return new JwkBuilder("RSA") {
            _n = modulus,
            _e = exponent
        };
    }

    /// <summary>
    /// Initializes a builder for an Elliptic Curve (EC) public key with the mandatory Curve and Coordinates.
    /// </summary>
    /// <param name="curve">The cryptographic curve name (e.g. "P-256", "P-384", "P-521").</param>
    /// <param name="x">The Base64Url-encoded X coordinate.</param>
    /// <param name="y">The Base64Url-encoded Y coordinate.</param>
    /// <returns>A configured <see cref="JwkBuilder"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="curve"/> is null/whitespace, or <paramref name="x"/>/<paramref name="y"/> is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JwkBuilder CreateEcdsa(string curve, Base64UrlString x, Base64UrlString y) {
        Preca.ThrowIfNullOrWhiteSpace(curve);
        Preca.ThrowIfEmpty(x);
        Preca.ThrowIfEmpty(y);

        return new JwkBuilder("EC") {
            _crv = curve,
            _x = x,
            _y = y
        };
    }

    /// <summary>
    /// Initializes a builder for an Octet Key Pair (OKP / Ed25519) with the mandatory Curve and Public Key parameter.
    /// </summary>
    /// <param name="curve">The curve name (e.g. "Ed25519", "X25519").</param>
    /// <param name="x">The Base64Url-encoded public key parameter (x).</param>
    /// <returns>A configured <see cref="JwkBuilder"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="curve"/> is null/whitespace, or <paramref name="x"/> is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JwkBuilder CreateOctetKeyPair(string curve, Base64UrlString x) {
        Preca.ThrowIfNullOrWhiteSpace(curve);
        Preca.ThrowIfEmpty(x);

        return new JwkBuilder("OKP") {
            _crv = curve,
            _x = x
        };
    }

    /// <summary>
    /// Initializes a builder for a symmetric ("oct") key with the mandatory key material.
    /// </summary>
    /// <param name="keyMaterial">The Base64Url-encoded key bytes (k).</param>
    /// <returns>A configured <see cref="JwkBuilder"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keyMaterial"/> is empty or uninitialized.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JwkBuilder CreateSymmetric(Base64UrlString keyMaterial) {
        Preca.ThrowIfEmpty(keyMaterial);

        return new JwkBuilder("oct") {
            _k = keyMaterial
        };
    }

    // ── Fluent Modifiers ──────────────────────────────────────────────────────

    /// <summary>
    /// Sets the unique Key ID (<c>kid</c>) parameter.
    /// </summary>
    /// <param name="keyId">The key identifier string.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keyId"/> is null or whitespace.</exception>
    public JwkBuilder WithKeyId(string keyId) {
        Preca.ThrowIfNullOrWhiteSpace(keyId);
        this._kid = keyId;
        return this;
    }

    /// <summary>
    /// Sets the intended algorithm identifier (<c>alg</c>) using a standard IANA/JOSE string.
    /// </summary>
    /// <param name="algorithm">The algorithm identifier (e.g. "RS256", "ES256").</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="algorithm"/> is null or whitespace.</exception>
    public JwkBuilder WithAlgorithm(string algorithm) {
        Preca.ThrowIfNullOrWhiteSpace(algorithm);
        this._alg = algorithm;
        return this;
    }

    /// <summary>
    /// Sets the intended algorithm identifier (<c>alg</c>) using a strongly-typed <see cref="RsaAlgorithm"/>.
    /// </summary>
    /// <param name="algorithm">The strongly-typed RSA algorithm.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithm"/> is null.</exception>
    public JwkBuilder WithAlgorithm(RsaAlgorithm algorithm) {
        Preca.ThrowIfNull(algorithm);
        this._alg = algorithm.Name;
        return this;
    }

    /// <summary>
    /// Configures the key for digital signatures (<c>use: "sig"</c>).
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    public JwkBuilder ForSignature() {
        this._use = "sig";
        return this;
    }

    /// <summary>
    /// Configures the key for encryption / key wrapping (<c>use: "enc"</c>).
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    public JwkBuilder ForEncryption() {
        this._use = "enc";
        return this;
    }

    /// <summary>
    /// Sets a custom public key use (<c>use</c>) parameter.
    /// </summary>
    /// <param name="use">The public key use value.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="use"/> is null or whitespace.</exception>
    public JwkBuilder WithUse(string use) {
        Preca.ThrowIfNullOrWhiteSpace(use);
        this._use = use;
        return this;
    }

    /// <summary>
    /// Sets the key operation values (<c>key_ops</c>).
    /// </summary>
    /// <param name="keyOps">A span of permitted key operations (e.g. "sign", "verify", "encrypt", "decrypt").</param>
    /// <returns>This builder instance for method chaining.</returns>
    public JwkBuilder WithKeyOps(params ReadOnlySpan<string> keyOps) {
        this._keyOps = EquatableArray.Create(keyOps);
        return this;
    }

    /// <summary>
    /// Sets the key operation values (<c>key_ops</c>) from an existing <see cref="EquatableArray{String}"/>.
    /// </summary>
    /// <param name="keyOps">The array of permitted key operations.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public JwkBuilder WithKeyOps(EquatableArray<string> keyOps) {
        this._keyOps = keyOps;
        return this;
    }

    // ── Build Methods ─────────────────────────────────────────────────────────

    /// <summary>
    /// Validates all parameters and builds the immutable <see cref="Jwk"/> instance.
    /// </summary>
    /// <returns>The constructed, immutable <see cref="Jwk"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when mandatory parameters for the specified key type are missing or empty.</exception>
    public Jwk Build() {
        ValidateInvariants();

        return new Jwk {
            KeyType = this._kty,
            KeyId = this._kid,
            Algorithm = this._alg,
            PublicKeyUse = this._use,
            KeyOps = this._keyOps,
            Modulus = this._n?.Value,
            Exponent = this._e?.Value,
            Curve = this._crv,
            X = this._x?.Value,
            Y = this._y?.Value,
            KeyValue = this._k?.Value
        };
    }

    /// <summary>
    /// Builds the <see cref="Jwk"/> instance and automatically computes and sets the Key ID (<c>kid</c>) 
    /// to its RFC 7638 SHA-256 Thumbprint if not already explicitly provided.
    /// </summary>
    /// <returns>The constructed <see cref="Jwk"/> with a verified thumbprint <c>kid</c>.</returns>
    public Jwk BuildWithThumbprintKeyId() {
        if(this._kid is not null) {
            return Build();
        }

        Jwk withoutKid = Build();
        return withoutKid with { KeyId = withoutKid.ComputeThumbprint().Value };
    }

    // ── Private Validation ────────────────────────────────────────────────────

    private void ValidateInvariants() {
        switch(this._kty) {
            case "RSA":
                if(this._n is null || this._e is null || this._n.Value.IsEmpty || this._e.Value.IsEmpty) {
                    throw new InvalidOperationException("RSA JWK requires non-empty Modulus (n) and Exponent (e) parameters.");
                }
                break;

            case "EC":
                if(string.IsNullOrWhiteSpace(this._crv) || this._x is null || this._y is null || this._x.Value.IsEmpty || this._y.Value.IsEmpty) {
                    throw new InvalidOperationException("EC JWK requires non-empty Curve (crv), X, and Y coordinate parameters.");
                }
                break;

            case "OKP":
                if(string.IsNullOrWhiteSpace(this._crv) || this._x is null || this._x.Value.IsEmpty) {
                    throw new InvalidOperationException("OKP JWK requires non-empty Curve (crv) and public key parameter (x).");
                }
                break;

            case "oct":
                if(this._k is null || this._k.Value.IsEmpty) {
                    throw new InvalidOperationException("Symmetric ('oct') JWK requires non-empty key material (k).");
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported JWK key type '{this._kty}'.");
        }
    }
}