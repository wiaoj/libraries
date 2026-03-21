using Wiaoj.Results;
using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Disposal)]
public sealed class ResultDisposalTests {

    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class TrackingDisposable : IDisposable {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class TrackingAsyncDisposable : IAsyncDisposable {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class TrackingBothDisposable : IDisposable, IAsyncDisposable {
        public bool SyncDisposed { get; private set; }
        public bool AsyncDisposed { get; private set; }
        public void Dispose() => SyncDisposed = true;
        public ValueTask DisposeAsync() { AsyncDisposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class NonDisposable {
        public bool Used { get; set; }
    }

    // ── Consume ───────────────────────────────────────────────────────────────

    [Fact]
    public void Consume_WhenSuccess_ExecutesActionAndDisposesValue() {
        TrackingDisposable disposable = new();
        Result<TrackingDisposable> result = disposable;
        bool actionCalled = false;

        result.Consume(v => { actionCalled = true; Assert.Same(disposable, v); });

        Assert.True(actionCalled);
        Assert.True(disposable.Disposed);
    }

    [Fact]
    public void Consume_WhenError_DoesNotExecuteAction() {
        bool called = false;
        Result<TrackingDisposable> result = SomeError;
        result.Consume(_ => { called = true; });
        Assert.False(called);
    }

    [Fact]
    public void Consume_WhenSuccess_NonDisposableValue_ExecutesActionWithoutDisposing() {
        NonDisposable obj = new();
        Result<NonDisposable> result = obj;
        result.Consume(v => { v.Used = true; });
        Assert.True(obj.Used);
    }

    [Fact]
    public void Consume_ReturnsOriginalResult() {
        TrackingDisposable disposable = new();
        Result<TrackingDisposable> result = disposable;
        Result<TrackingDisposable> returned = result.Consume(_ => { });
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Consume_WhenActionThrows_ValueIsStillDisposed() {
        TrackingDisposable disposable = new();
        Result<TrackingDisposable> result = disposable;

        Assert.Throws<InvalidOperationException>(() =>
            result.Consume(_ => throw new InvalidOperationException("test")));

        Assert.True(disposable.Disposed);
    }

    // ── ConsumeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeAsync_AsyncDisposable_WhenSuccess_ExecutesAndDisposes() {
        TrackingAsyncDisposable disposable = new();
        Result<TrackingAsyncDisposable> result = disposable;
        bool called = false;

        await result.ConsumeAsync(async (v, _) => { await Task.Yield(); called = true; });

        Assert.True(called);
        Assert.True(disposable.Disposed);
    }

    [Fact]
    public async Task ConsumeAsync_SyncDisposable_FallsBackToSyncDispose() {
        TrackingDisposable disposable = new();
        Result<TrackingDisposable> result = disposable;

        await result.ConsumeAsync(async (_, _) => { await Task.Yield(); });

        Assert.True(disposable.Disposed);
    }

    [Fact]
    public async Task ConsumeAsync_BothDisposable_PrefersAsync() {
        TrackingBothDisposable disposable = new();
        Result<TrackingBothDisposable> result = disposable;

        await result.ConsumeAsync(async (_, _) => { await Task.Yield(); });

        Assert.True(disposable.AsyncDisposed);
        Assert.False(disposable.SyncDisposed);
    }

    [Fact]
    public async Task ConsumeAsync_WhenError_DoesNotExecuteAction() {
        bool called = false;
        Result<TrackingAsyncDisposable> result = SomeError;
        await result.ConsumeAsync(async (_, _) => { await Task.Yield(); called = true; });
        Assert.False(called);
    }

    [Fact]
    public async Task ConsumeAsync_NonDisposable_ExecutesAction() {
        NonDisposable obj = new();
        Result<NonDisposable> result = obj;
        await result.ConsumeAsync(async (v, _) => { await Task.Yield(); v.Used = true; });
        Assert.True(obj.Used);
    }

    // ── DisposeValue ──────────────────────────────────────────────────────────

    [Fact]
    public void DisposeValue_WhenSuccess_DisposesValue() {
        TrackingDisposable disposable = new();
        Result<TrackingDisposable> result = disposable;
        result.DisposeValue();
        Assert.True(disposable.Disposed);
    }

    [Fact]
    public void DisposeValue_WhenSuccess_NonDisposable_DoesNothing() {
        NonDisposable obj = new();
        Result<NonDisposable> result = obj;
        result.DisposeValue(); // should not throw
    }

    [Fact]
    public void DisposeValue_WhenError_DoesNotThrow() {
        Result<TrackingDisposable> result = SomeError;
        result.DisposeValue(); // should not throw
    }

    [Fact]
    public void DisposeValue_WhenSuccess_CalledTwice_DisposesOnce() {
        TrackingDisposable disposable = new();
        Result<TrackingDisposable> result = disposable;
        result.DisposeValue();
        result.DisposeValue(); // should not throw
        Assert.True(disposable.Disposed);
    }

    // ── DisposeValueAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeValueAsync_AsyncDisposable_WhenSuccess_AsyncDisposed() {
        TrackingAsyncDisposable disposable = new();
        Result<TrackingAsyncDisposable> result = disposable;
        await result.DisposeValueAsync();
        Assert.True(disposable.Disposed);
    }

    [Fact]
    public async Task DisposeValueAsync_SyncOnlyDisposable_FallsBackToSyncDispose() {
        TrackingDisposable disposable = new();
        Result<TrackingDisposable> result = disposable;
        await result.DisposeValueAsync();
        Assert.True(disposable.Disposed);
    }

    [Fact]
    public async Task DisposeValueAsync_BothDisposable_PrefersAsync() {
        TrackingBothDisposable disposable = new();
        Result<TrackingBothDisposable> result = disposable;
        await result.DisposeValueAsync();
        Assert.True(disposable.AsyncDisposed);
        Assert.False(disposable.SyncDisposed);
    }

    [Fact]
    public async Task DisposeValueAsync_WhenError_DoesNotThrow() {
        Result<TrackingAsyncDisposable> result = SomeError;
        await result.DisposeValueAsync(); // should not throw
    }

    [Fact]
    public async Task DisposeValueAsync_NonDisposable_DoesNotThrow() {
        NonDisposable obj = new();
        Result<NonDisposable> result = obj;
        await result.DisposeValueAsync(); // should not throw
    }
}