using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Wiaoj.DistributedCounter.Internal; 
/// <summary>
/// Optimized default implementation of <see cref="ICounterKeyBuilder"/>.
/// Uses <see cref="string.Create"/> to minimize allocations and avoids redundant key parts.
/// </summary>
internal sealed class DefaultCounterKeyBuilder : ICounterKeyBuilder {
    private static readonly ConcurrentDictionary<Type, string> _typeNameCache = new();

    // 1. Simple Name: "prefix:name"
    public CounterKey Build(string name, DistributedCounterOptions options) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string prefix = options.GlobalKeyPrefix;
        return CounterKey.Parse(string.Concat(prefix, name));
    }

    // 2. Name + Generic Key: "prefix:name:id"
    public CounterKey Build<TKey>(string name, TKey key, DistributedCounterOptions options) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string prefix = options.GlobalKeyPrefix;
        string keyStr = FormatKeyInternal(key);

        // String.Create ile tek bir allocation
        string finalKey = string.Create(prefix.Length + name.Length + 1 + keyStr.Length, (prefix, name, keyStr), (span, state) => {
            state.prefix.AsSpan().CopyTo(span);
            span = span[state.prefix.Length..];

            state.name.AsSpan().CopyTo(span);
            span = span[state.name.Length..];

            span[0] = ':';
            state.keyStr.AsSpan().CopyTo(span[1..]);
        });

        return CounterKey.Parse(finalKey);
    }

    // 3. Typed Name: "prefix:TypeName:name" (Redundancy check eklendi)
    public CounterKey Build<TTag>(string name, DistributedCounterOptions options) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string typeName = GetCachedCleanTypeName(typeof(TTag));
        string prefix = options.GlobalKeyPrefix;

        // Eğer tip ismi ile verilen isim aynıysa (UserVisits:UserVisits), tekrarı önle.
        if(string.Equals(typeName, name, StringComparison.OrdinalIgnoreCase)) {
            return CounterKey.Parse(string.Concat(prefix, typeName));
        }

        return CounterKey.Parse(string.Concat(prefix, typeName, ":", name));
    }

    // 4. Typed + Generic Key: "prefix:TypeName:id"
    public CounterKey Build<TTag, TKey>(TKey key, DistributedCounterOptions options) {
        string typeName = GetCachedCleanTypeName(typeof(TTag));
        string keyStr = FormatKeyInternal(key);
        string prefix = options.GlobalKeyPrefix;

        string finalKey = string.Create(prefix.Length + typeName.Length + 1 + keyStr.Length, (prefix, typeName, keyStr), (span, state) => {
            state.prefix.AsSpan().CopyTo(span);
            span = span[state.prefix.Length..];

            state.typeName.AsSpan().CopyTo(span);
            span = span[state.typeName.Length..];

            span[0] = ':';
            state.keyStr.AsSpan().CopyTo(span[1..]);
        });

        return CounterKey.Parse(finalKey);
    }

    /// <summary>
    /// Formats the key using ISpanFormattable where possible to avoid boxing and extra strings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatKeyInternal<TKey>(TKey key) {
        if(key is null) return "null";

        // .NET 8+ ISpanFormattable kontrolü
        if(key is ISpanFormattable spanFormattable) {
            // Stackalloc ile heap allocation'dan kaçın
            Span<char> buffer = stackalloc char[128];
            if(spanFormattable.TryFormat(buffer, out int charsWritten, default, CultureInfo.InvariantCulture)) {
                return new string(buffer[..charsWritten]);
            }
        }

        return Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string GetCachedCleanTypeName(Type type) {
        return _typeNameCache.GetOrAdd(type, static t => {
            string name = GetCleanTypeName(t);
            // Redis standartlarına uyum için opsiyonel olarak lowercase yapabiliriz:
            // return name.ToLowerInvariant(); 
            return name;
        });
    }

    private static string GetCleanTypeName(Type type) {
        if(Nullable.GetUnderlyingType(type) is { } underlyingType) {
            return GetCleanTypeName(underlyingType) + "?";
        }

        if(!type.IsGenericType) return type.Name;

        // Generic tipi "List[Int32]" formatına getir (LINQ yerine döngü ile daha temiz)
        int backtickIndex = type.Name.IndexOf('`');
        string mainName = backtickIndex > 0 ? type.Name[..backtickIndex] : type.Name;

        Type[] args = type.GetGenericArguments();
        string[] argNames = new string[args.Length];
        for(int i = 0; i < args.Length; i++) {
            argNames[i] = GetCleanTypeName(args[i]);
        }

        return $"{mainName}[{string.Join(',', argNames)}]";
    }
}