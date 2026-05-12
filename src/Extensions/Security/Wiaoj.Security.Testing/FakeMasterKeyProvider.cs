using System.Security.Cryptography;
using Wiaoj.Primitives;

namespace Wiaoj.Security.Testing;

/// <summary>
/// A non-secure <see cref="IMasterKeyProvider"/> that provides a fixed, 
/// in-memory master key for testing purposes.
/// </summary>
public sealed class FakeMasterKeyProvider : IMasterKeyProvider {
    private readonly byte[] _fixedKey;

    /// <summary>
    /// Initializes a new instance with a fixed 32-byte key.
    /// </summary>
    public FakeMasterKeyProvider() {
        // Just a predictable dummy key for testing
        _fixedKey = new byte[32];
        for (int i = 0; i < 32; i++) _fixedKey[i] = (byte)i;
    }

    /// <summary>
    /// Returns a <see cref="MasterKey"/> wrapped around the fixed in-memory material.
    /// </summary>
    public ValueTask<MasterKey> GetMasterKeyAsync(CancellationToken ct = default) {
        // We create a new MasterKey instance. 
        // Note: MasterKey takes ownership of the material, but since this is a fake,
        // we can just clone the fixed key to avoid disposal issues in the fake provider.
        byte[] clone = (byte[])_fixedKey.Clone();
        return ValueTask.FromResult(new MasterKey(Secret.From(clone)));
    }
}
