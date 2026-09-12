using System.Runtime.CompilerServices;

namespace Wiaoj.Querying.Extensions;
/// <summary>
/// Provides extension methods for <see cref="ReadOnlySpan{T}"/> of characters.
/// </summary>
internal static class ReadOnlySpanCharExtensions {
    extension(ReadOnlySpan<char> span) {
        /// <summary>
        /// Determines whether this span and another specified span have the same value, ignoring case using ordinal rules.
        /// </summary>
        /// <param name="other">The span to compare to this instance.</param>
        /// <returns><see langword="true"/> if the value of the other span is the same as this instance; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EqualsOrdinalIgnoreCase(ReadOnlySpan<char> other) {
            return span.Equals(other, StringComparison.OrdinalIgnoreCase);
        }
    }
}