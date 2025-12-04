using System.Security.Cryptography;

namespace Wiaoj.Serialization.Security.Abstractions;
/// <summary>
/// Defines the contract for an authenticated encryption algorithm,
/// which provides confidentiality, integrity, and authenticity.
/// </summary>
public interface IAuthenticatedEncryptor {
    /// <summary>
    /// Encrypts the provided plaintext.
    /// </summary>
    /// <param name="plainBytes">The raw data to encrypt.</param>
    /// <returns>
    /// The encrypted data, typically containing a nonce, authentication tag, and ciphertext.
    /// </returns>
    byte[] Encrypt(ReadOnlySpan<byte> plainBytes);

    /// <summary>
    /// Decrypts the provided encrypted data and verifies its integrity.
    /// </summary>
    /// <param name="encryptedData">The encrypted data to decrypt.</param>
    /// <returns>The original plaintext data.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown if decryption fails, which can indicate corrupted data, an incorrect key,
    /// or a failed integrity check (tampering).
    /// </exception>
    byte[] Decrypt(ReadOnlySpan<byte> encryptedData);

    CryptoStream CreateDecryptionStream(Stream streamToReadFrom);
}