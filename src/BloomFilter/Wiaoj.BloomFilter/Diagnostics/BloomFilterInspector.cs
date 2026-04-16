using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Wiaoj.BloomFilter.Advanced;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Diagnostics;

/// <summary>
/// A powerful diagnostic utility to visually inspect the memory density, shard state, 
/// and raw bit arrays of standard and advanced Wiaoj Bloom Filters.
/// </summary>
[RequiresUnreferencedCode("BloomFilterInspector performs reflection on internal bit arrays.")]
public static class BloomFilterInspector {

    /// <summary>
    /// Generates a comprehensive ASCII representation of the Bloom Filter's internal state.
    /// </summary>
    public static string GetVisualRepresentation(IBloomFilter filter) {
        StringBuilder sb = new();

        // 1. Eger DI üzerinden gelen bir Wrapper ise, içindeki gerçek filtreyi çıkart
        filter = UnwrapFilter(filter);

        sb.AppendLine(new string('=', 70));
        sb.AppendLine($" WIAOJ BLOOM FILTER INSPECTION: {filter.Name}");
        sb.AppendLine(new string('=', 70));

        Type type = filter.GetType();
        sb.AppendLine($" Type       : {GetFriendlyTypeName(type)}");
        sb.AppendLine($" Total Bits : {filter.Configuration.SizeInBits:N0}");
        sb.AppendLine($" Pop Count  : {filter.GetPopCount():N0}");
        sb.AppendLine(new string('-', 70));

        // 2. Filtre tipine göre Recursive (Özyineli) denetim yap
        InspectNode(filter, sb, 0, "Root");

        sb.AppendLine(new string('=', 70));
        return sb.ToString();
    }

    private static IBloomFilter UnwrapFilter(IBloomFilter filter) { 
        if(filter.GetType().IsGenericType && filter.GetType().GetGenericTypeDefinition() == typeof(TypedBloomFilterWrapper<>)) {
            var innerProp = filter.GetType().GetProperty("InnerFilter", BindingFlags.NonPublic | BindingFlags.Instance);
            if(innerProp != null) filter = (IBloomFilter)innerProp.GetValue(filter)!;
        }
         
        if(filter is LazyBloomFilterProxy proxy) {
            var loaded = proxy.GetInnerIfCreated();
            if(loaded != null) filter = loaded;
        }

        return filter;
    }

    private static void InspectNode(IBloomFilter filter, StringBuilder sb, int indentLevel, string label) {
        if(filter is LazyBloomFilterProxy proxy) {
            var inner = proxy.GetInnerIfCreated();
            if(inner != null) {
                InspectNode(inner, sb, indentLevel, label); // İçindekine devam et
                return;
            }
            sb.AppendLine($"{new string(' ', indentLevel * 2)}>> [Proxy: Not Initialized]");
            return;
        }

        Type type = filter.GetType();
        string indent = new(' ', indentLevel * 2);

        if(type == typeof(ScalableBloomFilter)) {
            InspectScalableFilter(filter, sb, indentLevel, label);
        }
        else if(type == typeof(RotatingBloomFilter)) {
            InspectRotatingFilter(filter, sb, indentLevel, label);
        }
        else if(type == typeof(ShardedBloomFilter)) {
            InspectShardedFilter(filter, sb, indentLevel, label);
        }
        else if(filter is InMemoryBloomFilter inMemory) {
            InspectInMemoryFilter(inMemory, sb, indentLevel, label);
        }
        else {
            sb.AppendLine($"{indent}>> [Unknown Filter Type: {GetFriendlyTypeName(type)}]");
        }
    }

    private static void InspectScalableFilter(IBloomFilter filter, StringBuilder sb, int indent, string label) {
        sb.AppendLine($"{new string(' ', indent)}▼ SCALABLE LAYER GROUP: {label}");

        var layersField = filter.GetType().GetField("_layers", BindingFlags.NonPublic | BindingFlags.Instance);
        if(layersField?.GetValue(filter) is IPersistentBloomFilter[] layers) {
            for(int i = 0; i < layers.Length; i++) {
                string subLabel = $"Layer {i} {(i == layers.Length - 1 ? "(ACTIVE/WRITE)" : "(READ-ONLY)")}";
                InspectNode(layers[i], sb, indent + 1, subLabel);
            }
        }
    }

    private static void InspectRotatingFilter(IBloomFilter filter, StringBuilder sb, int indent, string label) {
        sb.AppendLine($"{new string(' ', indent)}▼ ROTATING TIME WINDOWS: {label}");

        var shardsField = filter.GetType().GetField("_shards", BindingFlags.NonPublic | BindingFlags.Instance);
        if(shardsField?.GetValue(filter) is Array shardsArray) {
            for(int i = 0; i < shardsArray.Length; i++) {
                var shardObj = shardsArray.GetValue(i);
                if(shardObj == null) continue;

                var filterProp = shardObj.GetType().GetField("Filter", BindingFlags.Public | BindingFlags.Instance);
                var expProp = shardObj.GetType().GetField("Expiration", BindingFlags.Public | BindingFlags.Instance);

                IPersistentBloomFilter memFilter = (IPersistentBloomFilter)filterProp!.GetValue(shardObj)!;
                UnixTimestamp expTime = (UnixTimestamp)expProp!.GetValue(shardObj)!;

                string subLabel = $"Time Shard {i} (Expires: {expTime.ToDateTimeLocal():yyyy-MM-dd HH:mm:ss})";
                InspectNode(memFilter, sb, indent + 1, subLabel);
            }
        }
    }

    private static void InspectShardedFilter(IBloomFilter filter, StringBuilder sb, int indent, string label) {
        sb.AppendLine($"{new string(' ', indent)}▼ SHARDED MULTI-THREADED GROUP: {label}");

        var shardsField = typeof(ShardedBloomFilter).GetField("_shards", BindingFlags.NonPublic | BindingFlags.Instance);
        if(shardsField?.GetValue(filter) is InMemoryBloomFilter[] shards) {
            for(int i = 0; i < shards.Length; i++) {
                InspectInMemoryFilter(shards[i], sb, indent + 1, $"Shard #{i}");
            }
        }
    }

    private static void InspectInMemoryFilter(InMemoryBloomFilter filter, StringBuilder sb, int indentLevel, string label) {
        string indent = new(' ', indentLevel * 2);

        long sizeBits = filter.Configuration.SizeInBits;
        long popCount = filter.GetPopCount();

        Percentage fillRatio = Percentage.FromDouble((double)popCount / sizeBits);
        double memMb = (sizeBits / 8.0) / (1024 * 1024);

        sb.AppendLine($"{indent}■ {label}");
        sb.AppendLine($"{indent}  ├ Memory     : ~{memMb:F2} MB");
        sb.AppendLine($"{indent}  ├ Capacity   : {filter.Configuration.ExpectedItems:N0} items");
        sb.AppendLine($"{indent}  ├ Fill Ratio : {DrawProgressBar(fillRatio)} {fillRatio}");

        // Reflection ile bellek dizisine ulaşıp allocation yapmadan bitleri okuyoruz
        string bitSample = GetBitSample(filter);
        sb.AppendLine($"{indent}  └ Bit Sample : [{bitSample} ...]");
    }

    private static string GetBitSample(InMemoryBloomFilter filter) {
        try {
            var bitsField = typeof(InMemoryBloomFilter).GetField("_bits", BindingFlags.NonPublic | BindingFlags.Instance);
            if(bitsField?.GetValue(filter) is PooledBitArray bitArray) {
                var arrayField = typeof(PooledBitArray).GetField("_array", BindingFlags.NonPublic | BindingFlags.Instance);
                if(arrayField?.GetValue(bitArray) is ulong[] rawBits && rawBits.Length > 0) {

                    // Sadece ilk 64 biti (1 ulong) visualize et
                    ulong sampleWord = rawBits[0];
                    Span<char> sample = stackalloc char[64];
                    for(int i = 0; i < 64; i++) {
                        sample[i] = (sampleWord & (1UL << i)) != 0 ? '1' : '0';
                    }
                    return sample.ToString();
                }
            }
        }
        catch { /* Diagnostic tool olduğu için hataları yutuyoruz, crash etmesin. */ }
        return "N/A";
    }

    private static string DrawProgressBar(Percentage percentage, int barSize = 20) {
        int filled = (int)Math.Round(percentage.Value * barSize);
        filled = Math.Clamp(filled, 0, barSize);
        return $"[{new string('█', filled)}{new string('-', barSize - filled)}]";
    }

    private static string GetFriendlyTypeName(Type type) {
        if(!type.IsGenericType) return type.Name;
        var genericTypeName = type.GetGenericTypeDefinition().Name;
        genericTypeName = genericTypeName[..genericTypeName.IndexOf('`')];
        var genericArgs = string.Join(", ", type.GetGenericArguments().Select(a => a.Name));
        return $"{genericTypeName}<{genericArgs}>";
    }
}