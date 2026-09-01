namespace Wiaoj.Benchmarks.Webhooks;

public static class BenchmarkCompletionTracker {
    private static TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static int _remaining;

    public static void Reset(int expectedCount) {
        _remaining = expectedCount;
        _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public static void SignalItemCompleted() {
        if(Interlocked.Decrement(ref _remaining) <= 0) {
            _tcs.TrySetResult();
        }
    }

    public static async Task WaitForCompletionAsync(TimeSpan timeout = default) {
        if(timeout == default) timeout = TimeSpan.FromSeconds(30);

        using CancellationTokenSource cts = new(timeout);
        try {
            await _tcs.Task.WaitAsync(cts.Token);
        }
        catch(OperationCanceledException) {
            throw new TimeoutException($"[TIMEOUT] 10 saniyede tamamlanamadı! İşlenmeyi bekleyen kalan mesaj sayısı: {_remaining}");
        }
    }
}