using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Wiaoj.Extensions;
public static class EnumerableExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? source) {
        if(source is null) return true;

        // Optimizasyon: Eğer Count özelliği varsa (List, Array, Collection vb.) direkt ona bak.
        // Bu sayede Enumerator oluşturmaktan (allocation) kurtulursun.
        if(source is ICollection<T> collection) {
            return collection.Count == 0;
        }

        // IReadOnlyCollection (genelde List'ler bunu da implemente eder)
        if(source is IReadOnlyCollection<T> readOnlyCollection) {
            return readOnlyCollection.Count == 0;
        }

        // Fallback: Mecburen Any()
        return !source.Any();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasItems<T>([NotNullWhen(true)] this IEnumerable<T>? source) {
        return source is not null && source.IsNullOrEmpty();
    }
}