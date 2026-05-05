using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// Provides type-safe encrypt/decrypt operations for a specific secret domain,
/// with built-in key rotation support.
/// </summary>
/// <typeparam name="TContext">The secret domain. All operations are constrained to this domain at compile time.</typeparam>
public interface ISecretProtector<TContext> where TContext : ISecretContext {

    /// <summary>The version of the key currently used for new encryptions.</summary>
    KeyVersion CurrentKeyVersion { get; }

    /// <summary>Encrypts raw bytes using the current key.</summary>
    EncryptedSecret<TContext> Protect(ReadOnlySpan<byte> plainSecret);

    /// <summary>Encrypts a UTF-8 string using the current key.</summary>
    EncryptedSecret<TContext> Protect(string plainText);

    /// <summary>
    /// Decrypts an <see cref="EncryptedSecret{TContext}"/> into secure unmanaged memory.
    /// The caller must dispose the returned <see cref="Secret{T}"/>.
    /// </summary>
    Secret<byte> Unprotect(in EncryptedSecret<TContext> encrypted);

    /// <summary>
    /// Returns <see langword="true"/> when the secret's <see cref="EncryptedSecret{TContext}.KeyVersion"/>
    /// is older than <see cref="CurrentKeyVersion"/>.
    /// </summary>
    bool NeedsRotation(in EncryptedSecret<TContext> encrypted);

    /// <summary>
    /// Re-encrypts the secret with the current key.
    /// Returns the original unchanged if <see cref="NeedsRotation"/> is <see langword="false"/>.
    /// The caller must persist the returned value to complete rotation.
    /// </summary>
    EncryptedSecret<TContext> Rotate(in EncryptedSecret<TContext> encrypted);
}