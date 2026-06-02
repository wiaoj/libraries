## 1. Asymmetric algoritmalar — VO mu, başka yapı mı?

**Cevap: VO değil.** Algoritmalar **enum + behavior**, key'ler ise type (class/struct).

Doğru ayrım:

```
TYPES (sınıf/struct — gerçek nesne):
  RsaKeyPair, RsaPublicKey
  EcdsaKeyPair, EcdsaPublicKey
  Ed25519KeyPair, Ed25519PublicKey

ALGORITMA (enum/static — "ne ile imzaladım"):
  SigningAlgorithm: RS256, RS384, RS512, PS256, PS384, PS512, ES256, ES384, ES512, EdDSA
  EncryptionAlgorithm: RsaOaep, RsaOaepSha256
```

**Neden?** Çünkü algoritma **key'in özelliği değil, kullanım metodunun özelliği**.

Aynı `RsaKeyPair`:
- `RS256` ile imzalanabilir (PKCS#1 + SHA256)
- `RS512` ile imzalanabilir (PKCS#1 + SHA512)
- `PS256` ile imzalanabilir (PSS + SHA256)
- `RSA-OAEP` ile encrypt edilebilir

Aynı key, farklı algoritma. Yani:

```csharp
RsaKeyPair key = ...;

byte[] sig1 = key.Sign(data, SigningAlgorithm.RS256);
byte[] sig2 = key.Sign(data, SigningAlgorithm.PS256);

byte[] enc = key.Encrypt(data, EncryptionAlgorithm.RsaOaepSha256);
```

Algoritma = **enum + algoritma metadata** (hash size, padding, RFC sayısı). Key = **gerçek private/public material**.

### Önerilen yapı

```
Wiaoj.Primitives.Cryptography.Asymmetric/
├── Keys/
│   ├── RsaKeyPair.cs            (class — RSA private+public material)
│   ├── RsaPublicKey.cs          (class — sadece public)
│   ├── EcdsaKeyPair.cs
│   ├── EcdsaPublicKey.cs
│   ├── Ed25519KeyPair.cs
│   └── Ed25519PublicKey.cs
├── Algorithms/
│   ├── SigningAlgorithm.cs      (enum: RS256, PS256, ES256, EdDSA, ...)
│   ├── EncryptionAlgorithm.cs   (enum: RsaOaep, RsaOaepSha256)
│   ├── KeyCurve.cs              (enum: P256, P384, P521)
│   └── AlgorithmMetadata.cs     (static lookup — hash size, padding, IANA name)
├── Serialization/
│   ├── JwkSerializer.cs         (static — JWK ↔ key parse/format)
│   └── PemSerializer.cs         (static — PEM ↔ key)
└── Generation/
    └── AsymmetricKeyGenerator.cs (static — Create RSA/ECDSA/Ed25519 with params)
```

**Key'ler VO mu, class mı?**
- `RsaKeyPair` = **class**, çünkü içeride .NET `RSA` instance tutuyor (disposable, mutable, IDisposable)
- `EcdsaKeyPair` = **class**, aynı sebep
- `Ed25519KeyPair` = **struct/readonly record olabilir** (32+32 byte sabit, immutable)

VO genelde **value semantics + immutable + equality by value** demek. RSA/ECDSA key'ler immutable ama equality genelde reference-based yapılır (key material karşılaştırma security risk — timing attack). Yani **class + immutable property'ler** daha doğru.

### Algoritma enum'unun değeri

```csharp
public enum SigningAlgorithm {
    RS256, RS384, RS512,
    PS256, PS384, PS512,
    ES256, ES384, ES512,
    EdDSA
}

public static class AlgorithmMetadata {
    public static string IanaName(SigningAlgorithm a) => a switch {
        SigningAlgorithm.RS256 => "RS256",
        SigningAlgorithm.PS256 => "PS256",
        SigningAlgorithm.EdDSA => "EdDSA",
        // ...
    };

    public static HashAlgorithmName HashName(SigningAlgorithm a) => a switch {
        SigningAlgorithm.RS256 or SigningAlgorithm.PS256 or SigningAlgorithm.ES256
            => HashAlgorithmName.SHA256,
        // ...
    };

    public static RSASignaturePadding RsaPadding(SigningAlgorithm a) => a switch {
        SigningAlgorithm.RS256 or SigningAlgorithm.RS384 or SigningAlgorithm.RS512
            => RSASignaturePadding.Pkcs1,
        SigningAlgorithm.PS256 or SigningAlgorithm.PS384 or SigningAlgorithm.PS512
            => RSASignaturePadding.Pss,
        _ => throw new InvalidOperationException()
    };
}
```

Bu **switch'leri tek yerde toplar**. JWT signer, JWKS endpoint, DPoP verifier — hepsi `AlgorithmMetadata`'dan okur. Şu an Vaultex'te bu switch'ler **dağınık**.

## 2. Wiaoj.Security'de şu an yaptığımız gibi şifreleme yapısı — eklenmeli mi?

Soruyu netleştireyim: "Asymmetric key wrapper'lar tamam, ama bunlara KeyRing/rotation/SecretProtector gibi bir framework de eklemeli miyiz?"

**Cevap: Evet ama farklı amaçla, ve şu an değil.**

### Symmetric (mevcut) ve Asymmetric (yeni) farkı

**Symmetric encryption (mevcut Wiaoj.Security):**
- Use case: data-at-rest encryption (DB field, secret value)
- Rotation: yeni key oluştur, eski blob'lar eski key'le decrypt edilir, transparent
- Key sayısı: zaman içinde 10-20 versiyon birikir
- Sahibi: uygulamanın kendi master key'i

**Asymmetric keys (yeni eklenecek):**
- Use case: signing (JWT, PASETO), verification (JWKS public yayını), encryption (id_token encryption)
- Rotation: yeni key oluştur, **JWKS'e ek olarak yayınla** (eski + yeni aynı anda yayında), grace period sonrası eski silinir
- Key sayısı: 1-3 aktif key yayında olur
- Sahibi: tek bir authority (issuer)

**İkisi de "key rotation" yapar ama mantıkları farklı**:
- Symmetric KeyRing → "old key for decrypt, new for encrypt"
- Asymmetric SigningKeyStore → "all valid keys published, old for verify, new for sign"

### Vaultex'te zaten var

Sen şu an zaten yapıyorsun:
- `ISigningKeyStore` → `RedisSigningKeyStore` (RSA signing keys + rotation)
- `IPasetoKeyStore` → `RedisPasetoKeyStore` (Ed25519 PASETO keys)
- `KeyRotationService` (HostedService — periyodik rotate)

Yani **asymmetric key rotation framework'ün zaten var** — sadece her algoritma için ayrı interface var, dağınık.

### Wiaoj.Security'ye eklenecek değer

**Unified abstraction:**

```csharp
// Wiaoj.Security.AsymmetricKeys/
public interface IAsymmetricKeyStore {
    Task<RsaKeyPair> GetActiveRsaSigningKeyAsync();
    Task<IReadOnlyList<RsaPublicKey>> GetAllValidRsaPublicKeysAsync();
    Task RotateRsaAsync();
    // aynı ECDSA, Ed25519 için
}

public class KeyRingForSigning<TKey> {
    // Versioned + grace period + JWKS publication ready
}
```

**Bu Vaultex'in ihtiyaç duyduğu şey:**
- `ISigningKeyStore` + `IPasetoKeyStore` birleşir
- Multi-algorithm desteklemek için interface çoğaltmaya gerek kalmaz
- Yeni algoritma eklemek = yeni `TKey` tipi, framework değişmez

### Ama dikkat — şu an gerekli mi?

**Hayır, şimdi değil. Sebebi:**

Vaultex'in mevcut `ISigningKeyStore` + `IPasetoKeyStore` **çalışıyor**. Refactor değeri:
- ✅ Daha temiz API
- ✅ Yeni algoritma eklemek kolay
- ❌ Davranış değişmiyor
- ❌ Performans değişmiyor
- ❌ Security artmıyor

Yani **kosmetik refactor**, fonksiyonel değer sıfır. Şu an Vaultex'in **gerçek ihtiyacı**:
- Distributed lock (refresh token rotation race)
- Backplane (key rotation broadcast)
- Doc temizliği
- Endpoint reorganizasyonu

Bu **library work** (asymmetric framework) → **3-6 ay sonra**, Wiaoj.Security release'inin bir parçası olarak yap. Vaultex feature gelişimini bloklamasın.

## Net plan

### Şimdi yapılacak (Vaultex'i bloklamayan, basit library work)

1. **Wiaoj.Primitives.Cryptography.Asymmetric** namespace oluştur
2. **Key types** (`RsaKeyPair`, `RsaPublicKey`, vb.) — sadece wrapper, .NET RSA'yı kapatma
3. **JWK serializer** — Vaultex `DPoPProofValidator`'ı şimdiden kullanabilir, manuel parse silinir
4. **SigningAlgorithm enum + AlgorithmMetadata** — Vaultex'teki dağınık switch'leri tek yerde topla

**Süre:** 1-2 gün (sen veya agent). Vaultex'i etkilemez (gradual adoption).

### İleride (Wiaoj.Security v2 olarak)

5. **`IAsymmetricKeyStore` unified interface**
6. **`KeyRingForSigning<TKey>` rotation framework**
7. **Vaultex `ISigningKeyStore` + `IPasetoKeyStore` → bu interface'e migrate**

**Süre:** 1 hafta, ayrı sprint.

## Cevaplar net olarak

**1. Algoritmalar VO mu?**
- **Key'ler:** class (RSA/ECDSA disposable wrapper) veya struct (Ed25519 immutable byte material)
- **Algoritma:** enum + static metadata helper (`SigningAlgorithm.RS256` + `AlgorithmMetadata.HashName(...)`)
- VO değil, "value type" değil — bunlar **behavior carrier**'lar

**2. Wiaoj.Security'ye şifreleme tarzı bir asymmetric framework eklenmeli mi?**
- **Evet ama sonra.** Şu an Vaultex'in zaten çalışan `ISigningKeyStore` + `IPasetoKeyStore`'u var. Refactor değeri var ama acil değil.
- **Şimdi sadece:** key type wrapper'ları + JWK serializer + algoritma enum
- **İleride:** unified `IAsymmetricKeyStore` + KeyRing-style framework