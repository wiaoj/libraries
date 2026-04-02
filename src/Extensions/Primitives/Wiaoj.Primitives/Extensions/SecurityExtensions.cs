namespace Wiaoj.Primitives.Extensions;
public static class SecurityExtensions {

    extension(int byteCount) {
        // Kullanım: 32.ToSecret() -> 32 byte'lık rastgele secret
        public Secret<byte> ToSecret() => Secret.Generate(byteCount);
    }

    extension(SecretFactory factory) {
        // Standartlara özel isimlendirilmiş extensionlar
        public Secret<byte> Aes256Key() => Secret.Generate(32);
        public Secret<byte> Aes128Key() => Secret.Generate(16);
        public Secret<byte> SecureSalt() => Secret.Generate(16);

        /// <summary>
        /// Generates a cryptographically strong 64-byte (512-bit) random key 
        /// optimally sized for HMAC-SHA-512 operations.
        /// </summary>
        public Secret<byte> HmacSha512Key() {
            return Secret<byte>.Generate(64);
        }

        /// <summary>
        /// Generates a cryptographically strong 32-byte (256-bit) random key 
        /// optimally sized for HMAC-SHA-256 operations.
        /// </summary>
        public Secret<byte> HmacSha256Key() {
            return Secret<byte>.Generate(32);
        }
    }
}