using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Wiaoj.Primitives.Obfuscation;

/// <summary>
/// A generalized, stateless Feistel Cipher implementation for 64-bit and 128-bit integers.
/// Used to scramble sequential IDs into random-looking numbers while preserving their bit length.
/// </summary>
internal static class IdCipher {
    private const int Rounds = 4; // Balance between security and performance (3 is min, 4 is good)

    // Golden Ratio constant for better bit distribution (phi)
    // 2^32 / phi ≈ 2654435761
    private const uint GoldenRatioPrime = 2654435761u;

    /// <summary>
    /// Derives round keys from a user seed string using SHA256.
    /// This ensures high entropy even from weak passwords.
    /// </summary>
    public static uint[] DeriveKeys(string seed) {
        if(string.IsNullOrEmpty(seed))
            throw new ArgumentNullException(nameof(seed));

        // Use SHA256 to expand the seed into 32 bytes
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));

        // Create keys for the rounds (we need 'Rounds' keys)
        // Since SHA256 gives 8 uints (32 bytes), and we typically need 4 rounds, we are safe.
        uint[] keys = new uint[Rounds];
        for(int i = 0; i < Rounds; i++) {
            keys[i] = BitConverter.ToUInt32(hash, (i * 4) % hash.Length);
        }
        return keys;
    }

    /// <summary>
    /// Scrambles a 64-bit integer (reversible).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Encrypt64(ulong value, uint[] keys) {
        uint left = (uint)(value >> 32);
        uint right = (uint)value;

        for(int i = 0; i < Rounds; i++) {
            uint temp = right;
            // Feistel: NewRight = OldLeft ^ F(OldRight, Key)
            right = left ^ RoundFunction(right, keys[i]);
            left = temp; // NewLeft = OldRight
        }

        // Final Swap: High part gets 'right', Low part gets 'left'
        return ((ulong)right << 32) | left;
    }

    /// <summary>
    /// Unscrambles a 64-bit integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Decrypt64(ulong value, uint[] keys) {
        uint left = (uint)value;
        uint right = (uint)(value >> 32);

        // Reverse Rounds
        for(int i = Rounds - 1; i >= 0; i--) {
            uint temp = left;
            // Reverse Feistel: OldLeft = NewRight ^ F(OldRight, Key)
            // Bizim değişken adlandırmamızda: left = right ^ F(left, Key)
            left = right ^ RoundFunction(left, keys[i]);
            right = temp;
        }

        // Final Combine: High=Left, Low=Right
        return ((ulong)left << 32) | right;
    }

    /// <summary>
    /// Scrambles a 128-bit integer (reversible).
    /// Uses a "Wide Block" approach treating 64-bit halves as the L/R blocks.
    /// </summary>
    public static Int128 Encrypt128(Int128 value, uint[] keys) {
        ulong left = (ulong)(value >> 64);
        ulong right = (ulong)value;

        for(int i = 0; i < Rounds; i++) {
            ulong temp = right;
            right = left ^ Encrypt64(right, keys);
            left = temp;
        }

        return ((Int128)right << 64) | left;
    }

    public static Int128 Decrypt128(Int128 value, uint[] keys) {
        ulong left = (ulong)value;
        ulong right = (ulong)(value >> 64);

        for(int i = Rounds - 1; i >= 0; i--) {
            ulong temp = left;
            left = right ^ Encrypt64(left, keys);
            right = temp;
        }

        return ((Int128)left << 64) | right;
    }

    /// <summary>
    /// The non-linear round function "F".
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RoundFunction(uint val, uint key) {
        // MurmurHash3 finalizer mix-inspired
        uint x = (val ^ key) * GoldenRatioPrime;
        return (x << 13) | (x >> 19); // Rotate left 13
    }
}