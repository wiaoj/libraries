using Microsoft.Extensions.Options;

namespace Wiaoj.BloomFilter.Tests.Unit.Fakes;

public sealed class FakeOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> {
    public T CurrentValue => currentValue;
    public T Get(string? name) {
        return currentValue;
    }

    public IDisposable? OnChange(Action<T, string?> listener) {
        return null;
    }
}