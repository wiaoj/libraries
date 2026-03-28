using Wiaoj.Results;
using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Bridge)]
public sealed class ResultBridgeTests {

    // ── AsResult ──────────────────────────────────────────────────────────────

    [Fact]
    public void AsResult_FromValue_CreatesSuccessResult() {
        Result<int> result = 42.AsResult();
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void AsResult_FromString_CreatesSuccessResult() {
        Result<string> result = "hello".AsResult();
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public async Task AsResult_FromTask_WrapsValueInSuccess() {
        Result<int> result = await Task.FromResult(99).AsResult();
        Assert.True(result.IsSuccess);
        Assert.Equal(99, result.Value);
    }

    // ── AsTask ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AsTask_FromSuccessResult_ReturnsCompletedTask() {
        Task<Result<int>> task = SuccessInt(7).AsTask();
        Result<int> result = await task;
        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task AsTask_FromErrorResult_ReturnsCompletedTaskWithError() {
        Task<Result<int>> task = FailureInt().AsTask();
        Result<int> result = await task;
        Assert.True(result.IsFailure);
    }

    // ── EnsureNotNull ─────────────────────────────────────────────────────────

    [Fact]
    public void EnsureNotNull_WhenValueIsNotNull_ReturnsNonNullableSuccess() {
        Result<string?> nullable = (string?)"hello";
        Result<string> result = nullable.EnsureNotNull(SomeError);
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void EnsureNotNull_WhenValueIsNull_ReturnsError() {
        Result<string?> nullable = (string?)null;
        Result<string> result = nullable.EnsureNotNull(SomeError);
        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void EnsureNotNull_WhenAlreadyError_PropagatesOriginalError() {
        Result<string?> nullable = (Result<string?>)SomeError;
        Result<string> result = nullable.EnsureNotNull(AnotherError);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public async Task EnsureNotNullAsync_WhenValueIsNotNull_ReturnsSuccess() {
        Task<Result<string?>> task = Task.FromResult((Result<string?>)(string?)"world");
        Result<string> result = await task.EnsureNotNullAsync(SomeError);
        Assert.True(result.IsSuccess);
        Assert.Equal("world", result.Value);
    }

    [Fact]
    public async Task EnsureNotNullAsync_WhenValueIsNull_ReturnsError() {
        Task<Result<string?>> task = Task.FromResult((Result<string?>)(string?)null);
        Result<string> result = await task.EnsureNotNullAsync(SomeError);
        Assert.True(result.IsFailure);
    }

    // ── MapError ──────────────────────────────────────────────────────────────

    [Fact]
    public void MapError_WhenError_ReplacesWithNewError() {
        Result<int> result = FailureInt().MapError(AnotherError);
        Assert.True(result.IsFailure);
        Assert.Equal(AnotherError, result.FirstError);
    }

    [Fact]
    public void MapError_WhenSuccess_DoesNothing() {
        Result<int> result = SuccessInt().MapError(AnotherError);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void MapError_WithMapper_WhenError_TransformsFirstError() {
        Result<int> result = FailureInt()
            .MapError(e => Error.Failure($"{e.Code}.Mapped", $"mapped: {e.Description}"));
        Assert.True(result.IsFailure);
        Assert.Contains(".Mapped", result.FirstError.Code);
    }

    [Fact]
    public void MapError_WithMapper_WhenSuccess_DoesNothing() {
        bool mapperCalled = false;
        Result<int> result = SuccessInt()
            .MapError(e => { mapperCalled = true; return AnotherError; });
        Assert.False(mapperCalled);
        Assert.True(result.IsSuccess);
    }

    // ── MapSuccess ────────────────────────────────────────────────────────────

    [Fact]
    public void MapSuccess_WhenSuccess_DiscardValue() {
        Result<Success> result = SuccessInt(99).MapSuccess();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void MapSuccess_WhenError_PropagatesError() {
        Result<Success> result = FailureInt().MapSuccess();
        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public async Task MapSuccessAsync_WhenSuccess_DiscardValue() {
        Result<Success> result = await SuccessIntTask().MapSuccessAsync();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task MapSuccessAsync_WhenError_PropagatesError() {
        Result<Success> result = await FailureIntTask().MapSuccessAsync();
        Assert.True(result.IsFailure);
    }
}