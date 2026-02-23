using Microsoft.Extensions.Options;
using System.Buffers;
using Wiaoj.ObjectPool;

namespace Wiaoj.DistributedCounter.Internal;
internal sealed class DistributedCounterService(
    ICounterStorage storage,
    ICounterKeyBuilder keyBuilder,
    IDistributedCounterFactory factory,
    IOptions<DistributedCounterOptions> options,
    IObjectPool<Dictionary<string, CounterValue>> pool)
    : IDistributedCounterService {

    public async ValueTask<CounterValueCollection> GetValuesAsync(IEnumerable<string> counterNames, CancellationToken cancellationToken = default) {
        // 1. Dictionary Kirala
        PooledObject<Dictionary<string, CounterValue>> pooledDict = pool.Lease();
        Dictionary<string, CounterValue> resultDict = pooledDict.Item;

        int count = counterNames is ICollection<string> col ? col.Count : counterNames.Count();

        // Boşsa hemen dön
        if(count == 0) return new CounterValueCollection(resultDict, pooledDict);

        // 2. ArrayPool'dan geçici dizileri kirala
        CounterKey[] keysArray = ArrayPool<CounterKey>.Shared.Rent(count);
        CounterValue[] valuesArray = ArrayPool<CounterValue>.Shared.Rent(count);
        string[] namesArray = ArrayPool<string>.Shared.Rent(count);

        try {
            // 3. İsimleri ve Keyleri Hazırla
            int index = 0;
            foreach(string name in counterNames) {
                keysArray[index] = keyBuilder.Build(name, options.Value);
                namesArray[index] = name;
                index++;
            }

            // 4. Memory Sarmalayıcıları (Sadece count kadarını gösterir)
            ReadOnlyMemory<CounterKey> keysMem = new(keysArray, 0, count);
            Memory<CounterValue> valuesMem = new(valuesArray, 0, count);

            // 5. Storage'a Gönder (Doldurması için)
            await storage.GetManyAsync(keysMem, valuesMem, cancellationToken);

            // 6. Sonuçları İsimlerle Eşleştir
            Span<CounterValue> valuesSpan = valuesMem.Span; // Span ile hızlı erişim
            for(int i = 0; i < count; i++) {
                resultDict[namesArray[i]] = valuesSpan[i];
            }

            // 7. Paketle
            return new CounterValueCollection(resultDict, pooledDict);
        }
        catch {
            // Hata olursa dictionary'yi havuza geri bırak (Leak olmasın)
            pooledDict.Dispose();
            throw;
        }
        finally {
            // 8. Kiralık dizileri iade et
            ArrayPool<CounterKey>.Shared.Return(keysArray);
            ArrayPool<CounterValue>.Shared.Return(valuesArray);
            ArrayPool<string>.Shared.Return(namesArray, clearArray: true); // String referanslarını temizle!
        }
    }

    public async ValueTask FlushAllAsync(CancellationToken cancellationToken = default) {
        // Get only buffered counters from factory
        IEnumerable<BufferedDistributedCounter> bufferedCounters = ((IBufferedCounterSource)factory).GetBufferedCounters();

        IEnumerable<Task> tasks = bufferedCounters.Select(c => c.FlushAsync(cancellationToken).AsTask());
        await Task.WhenAll(tasks);
    }

    public async ValueTask ResetAllAsync(CancellationToken cancellationToken = default) {
        // 1. Get all instances tracked by factory
        IBufferedCounterSource source = (IBufferedCounterSource)factory;

        IEnumerable<IDistributedCounter> allCounters = source.GetAllTrackedCounters();

        // 2. Perform Reset on each (Clears local state + deletes from Storage)
        IEnumerable<Task> tasks = allCounters.Select(c => c.ResetAsync(cancellationToken).AsTask());
        await Task.WhenAll(tasks);

        // 3. Clear factory internal cache so new instances can be created fresh
        source.ClearCache();
    }
}