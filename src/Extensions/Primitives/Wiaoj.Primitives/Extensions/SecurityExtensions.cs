using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Extensions;
/// <summary>
/// Provides fluent extension methods for <see cref="SecretFactory"/> to generate 
/// domain-specific cryptographic secrets with standard-compliant lengths.
/// </summary>
public static class SecretExtensions {
    extension(SecretFactory) {
        /// <summary>
        /// Generates a cryptographically strong 32-byte (256-bit) random key 
        /// optimally sized for AES-256 operations.
        /// </summary>
        public static Secret<byte> Aes256Key() {
            return Secret.Generate(32);
        }

        /// <summary>
        /// Generates a cryptographically strong 16-byte (128-bit) random key 
        /// optimally sized for AES-128 operations.
        /// </summary>
        public static Secret<byte> Aes128Key() {
            return Secret.Generate(16);
        }

        /// <summary>
        /// Generates a cryptographically strong 16-byte (128-bit) random salt,
        /// suitable for password hashing or key derivation processes.
        /// </summary>
        public static Secret<byte> SecureSalt() {
            return Secret.Generate(16);
        }

        /// <summary>
        /// Generates a cryptographically strong 64-byte (512-bit) random key,
        /// recommended as the minimum entropy size for HMAC-SHA-512 operations.
        /// </summary>
        public static Secret<byte> HmacSha512Key() {
            return Secret.Generate(HmacSha512Hash.HashSizeInBytes);
        }

        /// <summary>
        /// Generates a cryptographically strong 32-byte (256-bit) random key,
        /// recommended as the minimum entropy size for HMAC-SHA-256 operations.
        /// </summary>
        public static Secret<byte> HmacSha256Key() {
            return Secret.Generate(HmacSha256Hash.HashSizeInBytes);
        }
    }
}