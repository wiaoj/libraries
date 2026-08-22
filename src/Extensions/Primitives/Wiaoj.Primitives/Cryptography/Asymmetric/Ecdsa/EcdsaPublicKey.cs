using System.Diagnostics;
using System.Security.Cryptography;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

/// <summary>
/// Represents an Elliptic Curve (ECDSA) public key used for digital signature verification.
/// </summary>
/// <remarks>
/// Holds strictly public parameters (<c>Curve</c>, <c>X</c>, <c>Y</c>), preventing private key leakage to JWKS or DPoP proofs.
/// </remarks>
[DebuggerDisplay("EcdsaPublicKey ({CurveName}, KeySize: {KeySizeInBits}-bit)")]
public sealed class EcdsaPublicKey : IDisposable {
    private readonly ECDsa _ecdsa;
    private readonly DisposeState _disposeState = new();

    /// <summary>Gets the standard curve name (e.g. <c>"P-256"</c>).</summary>
    public string CurveName { get; }

    /// <summary>Gets the Base64Url-encoded X coordinate on the elliptic curve.</summary>
    public Base64UrlString X { get; }

    /// <summary>Gets the Base64Url-encoded Y coordinate on the elliptic curve.</summary>
    public Base64UrlString Y { get; }

    /// <summary>Gets the key size in bits (256, 384, or 521).</summary>
    public int KeySizeInBits {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(EcdsaPublicKey));
            return this._ecdsa.KeySize;
        }
    }

    internal EcdsaPublicKey(ECDsa ecdsa, string curveName, Base64UrlString x, Base64UrlString y) {
        this._ecdsa = ecdsa;
        this.CurveName = curveName;
        this.X = x;
        this.Y = y;
    }

    /// <summary>
    /// Creates an <see cref="EcdsaPublicKey"/> from a curve name and Base64Url-encoded coordinates.
    /// </summary>
    /// <param name="curveName">The curve name (e.g. <c>"P-256"</c>, <c>"P-384"</c>, <c>"P-521"</c>).</param>
    /// <param name="x">The Base64Url-encoded X coordinate.</param>
    /// <param name="y">The Base64Url-encoded Y coordinate.</param>
    /// <returns>A validated <see cref="EcdsaPublicKey"/> instance.</returns>
    public static EcdsaPublicKey Create(string curveName, Base64UrlString x, Base64UrlString y) {
        Preca.ThrowIfNullOrWhiteSpace(curveName);
        Preca.ThrowIfEmpty(x);
        Preca.ThrowIfEmpty(y);

        ECCurve curve = curveName switch {
            "P-256" => ECCurve.NamedCurves.nistP256,
            "P-384" => ECCurve.NamedCurves.nistP384,
            "P-521" => ECCurve.NamedCurves.nistP521,
            _ => throw new NotSupportedException($"Elliptic curve '{curveName}' is not supported.")
        };

        byte[] xBytes = x.ToBytes();
        byte[] yBytes = y.ToBytes();

        try {
            ECParameters parameters = new() {
                Curve = curve,
                Q = new ECPoint {
                    X = xBytes,
                    Y = yBytes
                }
            };

            ECDsa ecdsa = ECDsa.Create(parameters);
            return new EcdsaPublicKey(ecdsa, curveName, x, y);
        }
        finally {
            CryptographicOperations.ZeroMemory(xBytes);
            CryptographicOperations.ZeroMemory(yBytes);
        }
    }

    /// <summary>
    /// Verifies a digital signature against the provided data in IEEE P1363 (R || S) format as mandated by JOSE/JWT (RFC 7518 Section 3.4).
    /// </summary>
    /// <param name="data">The original data span.</param>
    /// <param name="signature">The IEEE P1363 signature span (64 bytes for ES256).</param>
    /// <param name="algorithm">The ECDSA algorithm (e.g. <see cref="EcdsaAlgorithm.ES256"/>).</param>
    /// <returns><see langword="true"/> if the signature is valid; otherwise, <see langword="false"/>.</returns>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, EcdsaAlgorithm algorithm) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(EcdsaPublicKey));
        Preca.ThrowIfNull(algorithm);

        return this._ecdsa.VerifyData(
            data,
            signature,
            algorithm.HashName,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>Disposes the underlying cryptographic resources.</summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._ecdsa.Dispose();
            this._disposeState.SetDisposed();
        }
    }
}