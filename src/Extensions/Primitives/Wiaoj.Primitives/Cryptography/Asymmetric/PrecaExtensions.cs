using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

internal static partial class PrecaExtensions {
    extension(Preca) {

        /// <summary>
        /// Validates that a cryptographic Base64Url parameter is not empty or uninitialized.
        /// </summary>
        [DebuggerStepThrough, StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfEmpty(
            Base64UrlString argument,
            [CallerArgumentExpression(nameof(argument))] string? paramName = null) {

            Preca.ThrowIf(
                argument.IsEmpty,
                static (name) => new ArgumentException($"Cryptographic parameter '{name}' cannot be empty or uninitialized.", name),
                paramName);
        }

        /// <summary>
        /// Validates that the requested RSA key size meets the minimum security standard (2048 bits)
        /// and is aligned to a 64-bit boundary, as required by <see cref="System.Security.Cryptography.RSA.Create(int)"/>.
        /// </summary>
        [DebuggerStepThrough, StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfInvalidRsaKeySize(
            int keySizeInBits,
            [CallerArgumentExpression(nameof(keySizeInBits))] string? paramName = null) {

            const int MinimumSecureRsaKeySize = 2048;
            const int RequiredAlignmentInBits = 64;

            Preca.ThrowIfLessThan(keySizeInBits, MinimumSecureRsaKeySize, paramName);

            Preca.ThrowIf(
                keySizeInBits % RequiredAlignmentInBits != 0,
                static (name) => new ArgumentOutOfRangeException(name, $"RSA key size must be a multiple of {RequiredAlignmentInBits} bits."),
                paramName);
        }
    }
}