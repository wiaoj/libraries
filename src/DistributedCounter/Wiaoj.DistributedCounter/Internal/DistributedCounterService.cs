using Microsoft.Extensions.Options;
using System.Buffers;

namespace Wiaoj.DistributedCounter.Internal;

internal sealed class DistributedCounterService : IDistributedCounterService {
    private readonly ICounterStorage _defaultStorage;
    private readonly ICounterKeyBuilder _keyBuilder;
    private readonly IDistributedCounterFactory _factory;
    private readonly DistributedCounterOptions _options;
    private readonly IServiceProvider? _serviceProvider;

    public DistributedCounterService(
        ICounterStorage defaultStorage,
        ICounterKeyBuilder keyBuilder,
        IDistributedCounterFactory factory,
        IOptions<DistributedCounterOptions> options)
        : this(defaultStorage, keyBuilder, factory, options, null) {
    }

    public DistributedCounterService(
        ICounterStorage defaultStorage,
        ICounterKeyBuilder keyBuilder,
        IDistributedCounterFactory factory,
        IOptions<DistributedCounterOptions> options,
        IServiceProvider? serviceProvider) {
        this._defaultStorage = defaultStorage;
        this._keyBuilder = keyBuilder;
        this._factory = factory;
        this._options = options.Value;
        this._serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async ValueTask<CounterValueCollection> GetValuesAsync(
        IEnumerable<string> counterNames,
        CancellationToken cancellationToken) {

        int totalCount = counterNames is ICollection<string> col ? col.Count : counterNames.Count();

        Dictionary<string, CounterValue> resultDict = new(totalCount, StringComparer.Ordinal);

        if(totalCount == 0) return new CounterValueCollection(resultDict);

        try {
            // Group names by their resolved storage
            Dictionary<ICounterStorage, List<string>> storageGroups = [];
            foreach(string name in counterNames) {
                ICounterStorage storage = ResolveStorage(name);
                if(!storageGroups.TryGetValue(storage, out List<string>? list)) {
                    list = [];
                    storageGroups[storage] = list;
                }
                list.Add(name);
            }

            foreach(KeyValuePair<ICounterStorage, List<string>> group in storageGroups) {
                ICounterStorage storage = group.Key;
                List<string> names = group.Value;
                int count = names.Count;

                CounterKey[] keysArray = ArrayPool<CounterKey>.Shared.Rent(count);
                CounterValue[] valuesArray = ArrayPool<CounterValue>.Shared.Rent(count);

                try {
                    for(int i = 0; i < count; i++) {
                        keysArray[i] = this._keyBuilder.Build(names[i], this._options);
                    }

                    ReadOnlyMemory<CounterKey> keysMem = new(keysArray, 0, count);
                    Memory<CounterValue> valuesMem = new(valuesArray, 0, count);

                    await storage.GetManyAsync(keysMem, valuesMem, cancellationToken).ConfigureAwait(false);

                    Span<CounterValue> valuesSpan = valuesMem.Span;
                    for(int i = 0; i < count; i++) {
                        resultDict[names[i]] = valuesSpan[i];
                    }
                }
                finally {
                    ArrayPool<CounterKey>.Shared.Return(keysArray);
                    ArrayPool<CounterValue>.Shared.Return(valuesArray);
                }
            }

            return new CounterValueCollection(resultDict);
        }
        catch {
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask FlushAllAsync(CancellationToken cancellationToken) {
        IEnumerable<BufferedDistributedCounter> bufferedCounters = ((IBufferedCounterSource)this._factory).GetBufferedCounters();
        IEnumerable<Task> tasks = bufferedCounters.Select(c => c.FlushAsync(cancellationToken).AsTask());
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ResetAllAsync(CancellationToken cancellationToken) {
        IBufferedCounterSource source = (IBufferedCounterSource)this._factory;
        IEnumerable<IDistributedCounter> allCounters = source.GetAllTrackedCounters();

        IEnumerable<Task> tasks = allCounters.Select(c => c.ResetAsync(cancellationToken).AsTask());
        await Task.WhenAll(tasks).ConfigureAwait(false);

        source.ClearCache();
    }

    private ICounterStorage ResolveStorage(string name) {
        if(this._options.Registrations.TryGetValue(name, out CounterConfiguration? config) &&
           config?.StorageFactory is not null &&
           this._serviceProvider is not null) {
            return config.StorageFactory(this._serviceProvider);
        }
        return this._defaultStorage;
    }
}