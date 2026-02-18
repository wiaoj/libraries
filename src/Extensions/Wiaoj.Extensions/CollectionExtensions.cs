using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Extensions;

public static class CollectionExtensions {
    public static void AddRange<T>(this ICollection<T> target, params IEnumerable<T>[] collections) {
        Preca.ThrowIfNull(target);
        if (collections is null) return;

        if (target is List<T> list) {
            int totalCount = 0;
            foreach (IEnumerable<T> c in collections) {
                if (c is ICollection<T> col) totalCount += col.Count;
                else totalCount += c?.Count() ?? 0;
            }

            list.Capacity = Math.Max(list.Capacity, list.Count + totalCount);

            // Elemanları ekle
            foreach (IEnumerable<T> c in collections) {
                if (c is null) continue;

                list.AddRange(c);
            }
        }
        else {
            foreach (IEnumerable<T> c in collections) {
                if (c is null) continue;

                foreach (T? item in c)
                    target.Add(item); 
            }
        }
    }

    public static void CopyToValueList<T>(this IEnumerable<T> source, ref ValueList<T> target) {

    }
}