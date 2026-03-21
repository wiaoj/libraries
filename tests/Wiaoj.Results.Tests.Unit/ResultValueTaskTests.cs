using Wiaoj.Results;
using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.ValueTask)]
public sealed class ResultValueTaskTests {

    private static ValueTask<Result<int>> SuccessIntVT(int v = 42) => ValueTask.FromResult(SuccessInt(v));
    private static ValueTask<Result<int>> FailureIntVT() => ValueTask.FromResult(FailureInt());
    private static ValueTask<Result<string>> SuccessStringVT(string v = "ok") => ValueTask.FromResult(SuccessString(v));

    // ── ThenAsync (ValueTask left) ────────────────────────────────────────────

    [Fact]
    public async Task ThenAsync_ValueTask_WhenSuccess_ExecutesNext() {
        Result<string> result = await SuccessIntVT(5)
            .ThenAsync(v => ValueTask.FromResult(Result<string>.Success($"vt:{v}")));
        Assert.Equal("vt:5", result.Value);
    }

    [Fact]
    public async Task ThenAsync_ValueTask_WhenError_PropagatesErrors() {
        bool called = false;
        Result<string> result = await FailureIntVT()
            .ThenAsync(v => { called = true; return ValueTask.FromResult(Result<string>.Success($"{v}")); });
        Assert.False(called);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ThenAsync_ValueTask_WithCancellationToken_WhenCancelled_Throws() {
        CancellationToken cancelled = new CancellationToken(canceled: true);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
           await SuccessIntVT()
                .ThenAsync(
                    (int _, CancellationToken ct) => ValueTask.FromResult(Result<string>.Success("x")),
                    cancelled);
        });
    }

    // ── ThenAsync (Result<T> left, ValueTask next) ────────────────────────────

    [Fact]
    public async Task ThenAsync_SyncResult_ValueTaskNext_WhenSuccess_Executes() {
        Result<string> result = await SuccessInt(8)
            .ThenAsync(v => ValueTask.FromResult(Result<string>.Success($"r:{v}")));
        Assert.Equal("r:8", result.Value);
    }

    [Fact]
    public async Task ThenAsync_SyncResult_ValueTaskNext_WhenError_Propagates() {
        bool called = false;
        Result<string> result = await FailureInt()
            .ThenAsync(v => { called = true; return ValueTask.FromResult(Result<string>.Success($"{v}")); });
        Assert.False(called);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ThenAsync_SyncResult_ValueTaskWithCT_WhenSuccess_Executes() {
        Result<string> result = await SuccessInt(3)
            .ThenAsync((v, _) => ValueTask.FromResult(Result<string>.Success($"ct:{v}")));
        Assert.Equal("ct:3", result.Value);
    }

    // ── MatchAsync (ValueTask) ────────────────────────────────────────────────

    [Fact]
    public async Task MatchAsync_ValueTask_SyncHandlers_WhenSuccess_CallsOnValue() {
        string output = await SuccessIntVT(10)
            .MatchAsync(v => $"val:{v}", _ => "err");
        Assert.Equal("val:10", output);
    }

    [Fact]
    public async Task MatchAsync_ValueTask_SyncHandlers_WhenError_CallsOnError() {
        string output = await FailureIntVT()
            .MatchAsync(v => "val", _ => "error");
        Assert.Equal("error", output);
    }

    [Fact]
    public async Task MatchAsync_ValueTask_AsyncHandlers_WhenSuccess_CallsOnValue() {
        string output = await SuccessIntVT(7)
            .MatchAsync(
                v => ValueTask.FromResult($"async:{v}"),
                _ => ValueTask.FromResult("err"));
        Assert.Equal("async:7", output);
    }

    [Fact]
    public async Task MatchAsync_ValueTask_AsyncHandlers_WhenError_CallsOnError() {
        string output = await FailureIntVT()
            .MatchAsync(
                v => ValueTask.FromResult($"{v}"),
                _ => ValueTask.FromResult("async-err"));
        Assert.Equal("async-err", output);
    }

    [Fact]
    public async Task MatchAsync_ValueTask_WithCancellationToken_WhenCancelled_Throws() {
        CancellationToken cancelled = new CancellationToken(canceled: true);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await SuccessIntVT()
                .MatchAsync(v => $"{v}", _ => "err", cancelled);
        });
    }

    // ── MapAsync (ValueTask) ──────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_ValueTask_SyncMapper_WhenSuccess_TransformsValue() {
        Result<string> result = await SuccessIntVT(3)
            .MapAsync(v => $"m:{v}");
        Assert.Equal("m:3", result.Value);
    }

    [Fact]
    public async Task MapAsync_ValueTask_SyncMapper_WhenError_Propagates() {
        Result<string> result = await FailureIntVT()
            .MapAsync(v => $"{v}");
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task MapAsync_ValueTask_AsyncMapper_WhenSuccess_TransformsValue() {
        Result<string> result = await SuccessIntVT(5)
            .MapAsync(v => ValueTask.FromResult($"av:{v}"));
        Assert.Equal("av:5", result.Value);
    }

    [Fact]
    public async Task MapAsync_ValueTask_AsyncMapper_WhenError_Propagates() {
        bool called = false;
        Result<string> result = await FailureIntVT()
            .MapAsync(v => { called = true; return ValueTask.FromResult($"{v}"); });
        Assert.False(called);
        Assert.True(result.IsError);
    }

    // ── AsValueTask ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AsValueTask_FromResult_WrapsInValueTask() {
        ValueTask<Result<int>> vt = SuccessInt(11).AsValueTask();
        Result<int> result = await vt;
        Assert.Equal(11, result.Value);
    }

    [Fact]
    public async Task AsValueTask_FromTask_ConvertsToValueTask() {
        ValueTask<Result<int>> vt = SuccessIntTask(22).AsValueTask();
        Result<int> result = await vt;
        Assert.Equal(22, result.Value);
    }
}