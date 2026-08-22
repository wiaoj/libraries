using System.Diagnostics;
using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents an asymmetric Elliptic Curve (ECDSA) key pair containing both private and public keys.
/// </summary>
[DebuggerDisplay("EcdsaKeyPair ({PublicKey.CurveName}, {KeySizeInBits}-bit)")]
public sealed partial class EcdsaKeyPair : IDisposable {
    private readonly ECDsa _ecdsa;
    private readonly DisposeState _disposeState = new();

    /// <summary>Gets the public key portion of this key pair.</summary>
    public EcdsaPublicKey PublicKey { get; }

    /// <summary>Gets the size of the key in bits (256, 384, or 521).</summary>
    public int KeySizeInBits {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(EcdsaKeyPair));
            return this._ecdsa.KeySize;
        }
    }

    private EcdsaKeyPair(ECDsa ecdsa, EcdsaPublicKey publicKey) {
        this._ecdsa = ecdsa;
        this.PublicKey = publicKey;
    }

    // ── Key Generation Factories ──────────────────────────────────────────────

    /// <summary>
    /// Generates a standard NIST P-256 (ES256) key pair. Standard for DPoP (RFC 9449) and OAuth 2.0.
    /// </summary>
    public static EcdsaKeyPair GenerateP256() => Generate(EcdsaAlgorithm.ES256);

    /// <summary>
    /// Generates a high-security NIST P-384 (ES384) key pair.
    /// </summary>
    public static EcdsaKeyPair GenerateP384() => Generate(EcdsaAlgorithm.ES384);

    /// <summary>
    /// Generates an ultra-secure NIST P-521 (ES512) key pair.
    /// </summary>
    public static EcdsaKeyPair GenerateP521() => Generate(EcdsaAlgorithm.ES512);

    /// <summary>
    /// Generates a new ECDSA key pair for the specified algorithm.
    /// </summary>
    public static EcdsaKeyPair Generate(EcdsaAlgorithm algorithm) {
        Preca.ThrowIfNull(algorithm);

        ECDsa ecdsa = ECDsa.Create(algorithm.Curve);
        ECParameters publicParams = ecdsa.ExportParameters(false);

        Base64UrlString x = Base64UrlString.FromBytes(publicParams.Q.X!);
        Base64UrlString y = Base64UrlString.FromBytes(publicParams.Q.Y!);

        ECDsa publicEcdsa = ECDsa.Create(publicParams);
        EcdsaPublicKey publicKey = new(publicEcdsa, algorithm.CurveName, x, y);

        return new EcdsaKeyPair(ecdsa, publicKey);
    }

    // ── Cryptographic Operations ──────────────────────────────────────────────

    /// <summary>
    /// Signs data using the private key in IEEE P1363 (R || S) format as mandated by JOSE/JWT (RFC 7518 Section 3.4).
    /// </summary>
    /// <param name="data">The data span to sign.</param>
    /// <param name="algorithm">The ECDSA algorithm (e.g. <see cref="EcdsaAlgorithm.ES256"/>).</param>
    /// <returns>A byte array containing the fixed-length IEEE P1363 signature (64 bytes for ES256).</returns>
    public byte[] Sign(ReadOnlySpan<byte> data, EcdsaAlgorithm algorithm) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EcdsaKeyPair));
        Preca.ThrowIfNull(algorithm);

        return this._ecdsa.SignData(
            data.ToArray(),
            algorithm.HashName,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>
    /// Exports the private scalar (d) into secure unmanaged memory.
    /// The caller must dispose the returned <see cref="Secret{Byte}"/>.
    /// </summary>
    public Secret<byte> ExportPrivateKeyScalar() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EcdsaKeyPair));

        ECParameters parameters = this._ecdsa.ExportParameters(true);
        try {
            return Secret<byte>.From(parameters.D.AsSpan());
        }
        finally {
            if(parameters.D is not null) {
                CryptographicOperations.ZeroMemory(parameters.D);
            }
        }
    }

    /// <summary>Disposes the private key and the corresponding public key instances.</summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this.PublicKey.Dispose();
            this._ecdsa.Dispose();
            this._disposeState.SetDisposed();
        }
    }
}