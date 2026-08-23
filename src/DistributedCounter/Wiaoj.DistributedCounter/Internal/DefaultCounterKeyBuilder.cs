using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Wiaoj.DistributedCounter.Internal;

/// <summary>
/// Optimized default implementation of <see cref="ICounterKeyBuilder"/>.
/// Uses <c>string.Create</c> to minimize allocations and avoids redundant key parts.
/// </summary>
internal sealed class DefaultCounterKeyBuilder : ICounterKeyBuilder {
    private static readonly ConcurrentDictionary<Type, string> _typeNameCache = new();

    public CounterKey Build(string name, DistributedCounterOptions options) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string prefix = options.GlobalKeyPrefix;
        return CounterKey.Parse(string.Concat(prefix, name));
    }

    public CounterKey Build<TKey>(string name, TKey key, DistributedCounterOptions options) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string prefix = options.GlobalKeyPrefix;
        string keyStr = FormatKeyInternal(key);

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

    public CounterKey Build<TTag>(string name, DistributedCounterOptions options) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string typeName = GetCachedCleanTypeName(typeof(TTag));
        string prefix = options.GlobalKeyPrefix;

        if(string.Equals(typeName, name, StringComparison.OrdinalIgnoreCase)) {
            return CounterKey.Parse(string.Concat(prefix, typeName));
        }

        return CounterKey.Parse(string.Concat(prefix, typeName, ":", name));
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatKeyInternal<TKey>(TKey key) {
        if(key is null) return "null";

        if(key is ISpanFormattable spanFormattable) {
            Span<char> buffer = stackalloc char[128];
            if(spanFormattable.TryFormat(buffer, out int charsWritten, default, CultureInfo.InvariantCulture)) {
                return new string(buffer[..charsWritten]);
            }
        }

        return Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string GetCachedCleanTypeName(Type type) {
        return _typeNameCache.GetOrAdd(type, static t => GetCleanTypeName(t));
    }

    private static string GetCleanTypeName(Type type) {
        if(Nullable.GetUnderlyingType(type) is { } underlyingType) {
            return GetCleanTypeName(underlyingType) + "?";
        }

        if(!type.IsGenericType) return type.Name;

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