namespace Wiaoj.Concurrency.Extensions;

internal static class WPrecaExtensions {

    extension(Preca) {
        public static void ThrowIfNotPowerOfTwo(int value) {
            if (value <= 0 || (value & (value - 1)) != 0) {
                throw new PrecaArgumentException("The value must be a power of two.", nameof(value));
            }
        }
    }
}