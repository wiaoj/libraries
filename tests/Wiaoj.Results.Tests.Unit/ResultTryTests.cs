namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", "Try")]
public sealed class ResultTryTests {

    // ── Try<T> (sync, value) ──────────────────────────────────────────────────

    [Fact]
    public void Try_WhenOperationSucceeds_ReturnsValue() {
        Result<int> result = Result.Try(() => 42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Try_WhenOperationThrows_ReturnsError() {
        Result<int> result = Result.Try<int>(() => throw new InvalidOperationException("boom"));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Try_WhenOperationThrows_UsesDefaultExceptionHandler() {
        Result<int> result = Result.Try<int>(
            () => throw new InvalidOperationException("boom"));
        Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
    }

    [Fact]
    public void Try_WithCustomExceptionHandler_UsesProvidedHandler() {
        Result<int> result = Result.Try<int>(
            () => throw new InvalidOperationException("boom"),
            ex => Error.Validation("Custom.Code", ex.Message));
        Assert.Equal("Custom.Code", result.FirstError.Code);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void Try_ArgumentException_MapsToValidation() {
        Result<int> result = Result.Try<int>(
            () => throw new ArgumentException("bad arg"));
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void Try_TimeoutException_MapsToTimeout() {
        Result<int> result = Result.Try<int>(
            () => throw new TimeoutException("too slow"));
        Assert.Equal(ErrorType.Timeout, result.FirstError.Type);
    }

    [Fact]
    public void Try_UnauthorizedAccessException_MapsToUnauthorized() {
        Result<int> result = Result.Try<int>(
            () => throw new UnauthorizedAccessException());
        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    // ── Try (void) ────────────────────────────────────────────────────────────

    [Fact]
    public void Try_VoidOperation_WhenSucceeds_ReturnsSuccess() {
        Result<Success> result = Result.Try(() => { });
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Try_VoidOperation_WhenThrows_ReturnsError() {
        Result<Success> result = Result.Try(
            () => throw new InvalidOperationException("fail"));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Try_VoidOperation_WithCustomHandler_MapsError() {
        Result<Success> result = Result.Try(
            () => throw new Exception("x"),
            ex => Error.Failure("V.Code", ex.Message));
        Assert.Equal("V.Code", result.FirstError.Code);
    }

    // ── TryAsync (CT overload) ────────────────────────────────────────────────

    [Fact]
    public async Task TryAsync_WhenOperationSucceeds_ReturnsValue() {
        Result<string> result = await Result.TryAsync(
            async ct => { await Task.Yield(); return "ok"; });
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task TryAsync_WhenOperationThrows_ReturnsError() {
        Result<string> result = await Result.TryAsync<string>(
            _ => throw new InvalidOperationException("async boom"));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
    }

    [Fact]
    public async Task TryAsync_WithCustomHandler_MapsException() {
        Result<string> result = await Result.TryAsync<string>(
            _ => throw new InvalidOperationException("msg"),
            ex => Error.Validation("Async.Code", ex.Message));
        Assert.Equal("Async.Code", result.FirstError.Code);
    }

    [Fact]
    public async Task TryAsync_WhenCancellationRequested_Rethrows() {
        CancellationToken cancelled = new(canceled: true);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await Result.TryAsync(
                async ct => { ct.ThrowIfCancellationRequested(); return "x"; },
                cancellationToken: cancelled);
        });
    }

    [Fact]
    public async Task TryAsync_CancellationIsNotSwallowedByCustomHandler() {
        CancellationToken cancelled = new(canceled: true);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await Result.TryAsync(
                async ct => { ct.ThrowIfCancellationRequested(); return "x"; },
                exceptionHandler: _ => SomeError,
                cancellationToken: cancelled);
        });
    }

    // ── TryAsync (void, CT overload) ──────────────────────────────────────────

    [Fact]
    public async Task TryAsync_VoidOperation_WhenSucceeds_ReturnsSuccess() {
        Result<Success> result = await Result.TryAsync(
            async ct => { await Task.Yield(); });
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TryAsync_VoidOperation_WhenThrows_ReturnsError() {
        Result<Success> result = await Result.TryAsync(
            ct => throw new InvalidOperationException("void async fail"));
        Assert.True(result.IsFailure);
    }

    // ── TryAsync (no CT overload) ─────────────────────────────────────────────

    [Fact]
    public async Task TryAsync_NoCt_WhenSucceeds_ReturnsValue() {
        Result<int> result = await Result.TryAsync(() => Task.FromResult(7));
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task TryAsync_NoCt_WhenThrows_ReturnsError() {
        Result<int> result = await Result.TryAsync<int>(
            () => throw new Exception("no ct boom"));
        Assert.True(result.IsFailure);
    }
}