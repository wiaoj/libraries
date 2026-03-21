namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Async)]
public sealed class ResultAsyncTests {

    // ── ThenAsync — Task<Result<T>> left ──────────────────────────────────────

    [Fact]
    public async Task ThenAsync_TaskResult_WhenSuccess_ExecutesNext() {
        Result<string> result = await SuccessIntTask(10)
            .ThenAsync(v => Task.FromResult(Result<string>.Success($"val:{v}")));
        Assert.True(result.IsSuccess);
        Assert.Equal("val:10", result.Value);
    }

    [Fact]
    public async Task ThenAsync_TaskResult_WhenError_PropagatesErrors() {
        bool nextCalled = false;
        Result<string> result = await FailureIntTask()
            .ThenAsync(v => { nextCalled = true; return Task.FromResult(Result<string>.Success($"{v}")); });
        Assert.False(nextCalled);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ThenAsync_TaskResult_WithCancellationToken_WhenCancelled_Throws() {
        CancellationToken cancelled = new(canceled: true);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await SuccessIntTask()
                .ThenAsync((_, ct) => Task.FromResult(Result<string>.Success("x")), cancelled);
        });
    }

    [Fact]
    public async Task ThenAsync_TaskResult_ToSyncNext_WhenSuccess_ExecutesNext() {
        Result<string> result = await SuccessIntTask(3)
            .ThenAsync(v => Result<string>.Success($"{v * 2}"));
        Assert.Equal("6", result.Value);
    }

    [Fact]
    public async Task ThenAsync_TaskResult_ToSyncNext_WhenError_Propagates() {
        Result<string> result = await FailureIntTask()
            .ThenAsync(v => Result<string>.Success($"{v}"));
        Assert.True(result.IsError);
        Assert.Equal(SomeError, result.FirstError);
    }

    // ── ThenAsync — Result<T> left ────────────────────────────────────────────

    [Fact]
    public async Task ThenAsync_SyncResult_WhenSuccess_ExecutesNext() {
        Result<string> result = await SuccessInt(7)
            .ThenAsync(v => Task.FromResult(Result<string>.Success($"ok:{v}")));
        Assert.Equal("ok:7", result.Value);
    }

    [Fact]
    public async Task ThenAsync_SyncResult_WhenError_DoesNotCallNext() {
        bool called = false;
        Result<string> result = await FailureInt()
            .ThenAsync(v => { called = true; return Task.FromResult(Result<string>.Success($"{v}")); });
        Assert.False(called);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ThenAsync_SyncResult_ReturningTaskT_WrapsInSuccess() {
        Result<string> result = await SuccessInt(4)
            .ThenAsync(v => Task.FromResult($"wrapped:{v}"));
        Assert.True(result.IsSuccess);
        Assert.Equal("wrapped:4", result.Value);
    }

    [Fact]
    public async Task ThenAsync_SyncResult_WithCancellationToken_WhenError_PropagatesWithoutCancelling() {
        CancellationToken ct = CancellationToken.None;
        Result<string> result = await FailureInt()
            .ThenAsync((v, token) => Task.FromResult(Result<string>.Success($"{v}")), ct);
        Assert.True(result.IsError);
    }

    // ── MatchAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MatchAsync_SyncHandlers_WhenSuccess_CallsOnValue() {
        string output = await SuccessIntTask(42)
            .MatchAsync(
                onValue: v => $"val:{v}",
                onError: _ => "error");
        Assert.Equal("val:42", output);
    }

    [Fact]
    public async Task MatchAsync_SyncHandlers_WhenError_CallsOnError() {
        string output = await FailureIntTask()
            .MatchAsync(
                onValue: v => "value",
                onError: _ => "error");
        Assert.Equal("error", output);
    }

    [Fact]
    public async Task MatchAsync_AsyncHandlers_WhenSuccess_CallsOnValue() {
        string output = await SuccessIntTask(10)
            .MatchAsync(
                onValue: v => Task.FromResult($"async:{v}"),
                onError: _ => Task.FromResult("err"));
        Assert.Equal("async:10", output);
    }

    [Fact]
    public async Task MatchAsync_AsyncHandlers_WhenError_CallsOnError() {
        string output = await FailureIntTask()
            .MatchAsync(
                onValue: v => Task.FromResult($"{v}"),
                onError: _ => Task.FromResult("async-err"));
        Assert.Equal("async-err", output);
    }

    [Fact]
    public async Task MatchAsync_WithCancellationToken_WhenCancelled_Throws() {
        CancellationToken cancelled = new(canceled: true);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await SuccessIntTask()
                .MatchAsync(v => $"{v}", _ => "err", cancelled);
        });
    }

    // ── MapAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_SyncMapper_WhenSuccess_TransformsValue() {
        Result<string> result = await SuccessIntTask(5)
            .MapAsync(v => $"mapped:{v}");
        Assert.Equal("mapped:5", result.Value);
    }

    [Fact]
    public async Task MapAsync_SyncMapper_WhenError_Propagates() {
        Result<string> result = await FailureIntTask()
            .MapAsync(v => $"{v}");
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task MapAsync_AsyncMapper_WhenSuccess_TransformsValue() {
        Result<string> result = await SuccessIntTask(3)
            .MapAsync(v => Task.FromResult($"a:{v}"));
        Assert.Equal("a:3", result.Value);
    }

    [Fact]
    public async Task MapAsync_ResultMapper_Flattens() {
        Result<string> result = await SuccessIntTask(9)
            .MapAsync(v => Result<string>.Success($"flat:{v}"));
        Assert.Equal("flat:9", result.Value);
    }

    [Fact]
    public async Task MapAsync_ResultMapper_WhenMapperFails_ReturnsMapperError() {
        Result<string> result = await SuccessIntTask()
            .MapAsync(_ => (Result<string>)AnotherError);
        Assert.True(result.IsError);
        Assert.Equal(AnotherError, result.FirstError);
    }

    // ── MapSuccessAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task MapSuccessAsync_WhenSuccess_DiscardValue() {
        Result<Success> result = await SuccessIntTask().MapSuccessAsync();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task MapSuccessAsync_WhenError_Propagates() {
        Result<Success> result = await FailureIntTask().MapSuccessAsync();
        Assert.True(result.IsError);
        Assert.Equal(SomeError, result.FirstError);
    }

    // ── RecoverAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RecoverAsync_TaskResult_WhenError_ReturnsFallback() {
        Result<int> result = await FailureIntTask()
            .RecoverAsync(_ => 99);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task RecoverAsync_TaskResult_WhenSuccess_DoesNotCallFallback() {
        bool called = false;
        Result<int> result = await SuccessIntTask(5)
            .RecoverAsync(_ => { called = true; return 99; });
        Assert.False(called);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task RecoverAsync_TaskResult_AsyncFallback_WhenError_ReturnsFallback() {
        Result<int> result = await FailureIntTask()
            .RecoverAsync(_ => Task.FromResult(77));
        Assert.Equal(77, result.Value);
    }

    [Fact]
    public async Task RecoverAsync_SyncResult_AsyncFallback_WhenError_ReturnsFallback() {
        Result<int> result = await FailureInt()
            .RecoverAsync(_ => Task.FromResult(55));
        Assert.Equal(55, result.Value);
    }

    // ── DoAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DoAsync_SyncAction_WhenSuccess_ExecutesSideEffect() {
        int captured = 0;
        await SuccessIntTask(21).DoAsync(v => { captured = v; });
        Assert.Equal(21, captured);
    }

    [Fact]
    public async Task DoAsync_SyncAction_WhenError_DoesNotExecute() {
        bool called = false;
        await FailureIntTask().DoAsync(_ => { called = true; });
        Assert.False(called);
    }

    [Fact]
    public async Task DoAsync_AsyncAction_WhenSuccess_ExecutesSideEffect() {
        int captured = 0;
        await SuccessIntTask(33)
            .DoAsync(async (v, _) => { await Task.Yield(); captured = v; });
        Assert.Equal(33, captured);
    }

    [Fact]
    public async Task DoAsync_AsyncAction_WhenError_DoesNotExecute() {
        bool called = false;
        await FailureIntTask()
            .DoAsync(async (_, _) => { await Task.Yield(); called = true; });
        Assert.False(called);
    }

    [Fact]
    public async Task DoAsync_SyncResult_AsyncAction_WhenSuccess_Executes() {
        int captured = 0;
        await SuccessInt(8)
            .DoAsync(async (v, _) => { await Task.Yield(); captured = v; });
        Assert.Equal(8, captured);
    }

    [Fact]
    public async Task DoAsync_ParameterlessAsyncAction_WhenSuccess_Executes() {
        bool called = false;
        await SuccessIntTask()
            .DoAsync(async _ => { await Task.Yield(); called = true; });
        Assert.True(called);
    }

    [Fact]
    public async Task DoAsync_ParameterlessAsyncAction_WhenError_DoesNotExecute() {
        bool called = false;
        await FailureIntTask()
            .DoAsync(async _ => { await Task.Yield(); called = true; });
        Assert.False(called);
    }

    [Fact]
    public async Task DoAsync_PreservesOriginalResultAfterExecution() {
        Result<int> result = await SuccessIntTask(7)
            .DoAsync((v, _) => Task.CompletedTask);
        Assert.Equal(7, result.Value);
    }

    // ── IfSuccessAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IfSuccessAsync_SyncAction_WhenSuccess_Executes() {
        int captured = 0;
        await SuccessIntTask(3).IfSuccessAsync(v => { captured = v; });
        Assert.Equal(3, captured);
    }

    [Fact]
    public async Task IfSuccessAsync_SyncAction_WhenError_DoesNotExecute() {
        bool called = false;
        await FailureIntTask().IfSuccessAsync(_ => { called = true; });
        Assert.False(called);
    }

    [Fact]
    public async Task IfSuccessAsync_AsyncAction_WhenSuccess_Executes() {
        int captured = 0;
        await SuccessIntTask(9)
            .IfSuccessAsync(async (v, _) => { await Task.Yield(); captured = v; });
        Assert.Equal(9, captured);
    }

    [Fact]
    public async Task IfSuccessAsync_AsyncAction_WhenError_DoesNotExecute() {
        bool called = false;
        await FailureIntTask()
            .IfSuccessAsync(async (_, _) => { await Task.Yield(); called = true; });
        Assert.False(called);
    }

    // ── IfFailureAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IfFailureAsync_SyncAction_WhenError_Executes() {
        bool called = false;
        await FailureIntTask().IfFailureAsync(_ => { called = true; });
        Assert.True(called);
    }

    [Fact]
    public async Task IfFailureAsync_SyncAction_WhenSuccess_DoesNotExecute() {
        bool called = false;
        await SuccessIntTask().IfFailureAsync(_ => { called = true; });
        Assert.False(called);
    }

    [Fact]
    public async Task IfFailureAsync_AsyncAction_WhenError_Executes() {
        bool called = false;
        await FailureIntTask()
            .IfFailureAsync(async (_, _) => { await Task.Yield(); called = true; });
        Assert.True(called);
    }

    [Fact]
    public async Task IfFailureAsync_ReceivesAllErrors() {
        List<Error> errors = [SomeError, AnotherError];
        Result<int> result = errors;
        int errorCount = 0;
        await Task.FromResult(result)
            .IfFailureAsync(e => { errorCount = e.Count; });
        Assert.Equal(2, errorCount);
    }
}