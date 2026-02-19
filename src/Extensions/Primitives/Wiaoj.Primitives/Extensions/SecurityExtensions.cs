using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
 