namespace Wiaoj.Benchmarks.Webhooks;

public static class BenchmarkCompletionTracker {
    private static TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static int _remaining;

    public static void Reset(int expectedCount) {
        Interlocked.Exchange(ref _remaining, expectedCount);
        _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public static void SignalItemCompleted() {
        if(Interlocked.Decrement(ref _remaining) == 0) {
            _tcs.TrySetResult();
        }
    }

    public static async Task WaitForCompletionAsync(TimeSpan? timeout = null) {
        TimeSpan waitTimeout = timeout ?? TimeSpan.FromSeconds(60);
        using var cts = new CancellationTokenSource(waitTimeout);

        try {
            await _tcs.Task.WaitAsync(cts.Token);
        }
        catch(OperationCanceledException) {
            int left = Volatile.Read(ref _remaining);
            throw new TimeoutException($"[TIMEOUT] {waitTimeout.TotalSeconds} saniyede bitmedi! Kalan işlenmemiş mesaj: {left}");
        }
    }
}