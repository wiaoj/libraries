using Wiaoj.Results;
using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Ensure)]
public sealed class ResultEnsureTests {

    // ── Ensure (extension, sync predicate) ───────────────────────────────────

    [Fact]
    public void Ensure_SyncPredicate_WhenSuccess_PredicateTrue_ReturnsOriginal() {
        Result<int> result = SuccessInt(10).Ensure(v => v > 0, SomeError);
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void Ensure_SyncPredicate_WhenSuccess_PredicateFalse_ReturnsError() {
        Result<int> result = SuccessInt(-1).Ensure(v => v > 0, SomeError);
        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void Ensure_SyncPredicate_WhenAlreadyError_SkipsPredicate() {
        bool predicateCalled = false;
        FailureInt().Ensure(v => { predicateCalled = true; return v > 0; }, AnotherError);
        Assert.False(predicateCalled);
    }

    [Fact]
    public void Ensure_SyncPredicate_WhenAlreadyError_PreservesOriginalError() {
        Result<int> result = FailureInt().Ensure(_ => false, AnotherError);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void Ensure_ValueIndependent_PredicateTrue_ReturnsOriginal() {
        Result<int> result = SuccessInt().Ensure(() => true, SomeError);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Ensure_ValueIndependent_PredicateFalse_ReturnsError() {
        Result<int> result = SuccessInt().Ensure(() => false, SomeError);
        Assert.True(result.IsFailure);
    }

    // ── EnsureAsync — Task<Result<T>> + sync predicate ────────────────────────

    [Fact]
    public async Task EnsureAsync_TaskResult_SyncPredicate_WhenSuccess_PredicateTrue() {
        Result<int> result = await SuccessIntTask(5)
            .EnsureAsync(v => v > 0, SomeError);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureAsync_TaskResult_SyncPredicate_WhenSuccess_PredicateFalse() {
        Result<int> result = await SuccessIntTask(-1)
            .EnsureAsync(v => v > 0, SomeError);
        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public async Task EnsureAsync_TaskResult_SyncPredicate_WhenError_SkipsPredicate() {
        bool called = false;
        await FailureIntTask().EnsureAsync(v => { called = true; return v > 0; }, AnotherError);
        Assert.False(called);
    }

    // ── EnsureAsync — Task<Result<T>> + async predicate ───────────────────────

    [Fact]
    public async Task EnsureAsync_TaskResult_AsyncPredicate_PredicateTrue() {
        Result<int> result = await SuccessIntTask(5)
            .EnsureAsync(v => Task.FromResult(v > 0), SomeError);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureAsync_TaskResult_AsyncPredicate_PredicateFalse() {
        Result<int> result = await SuccessIntTask(-5)
            .EnsureAsync(v => Task.FromResult(v > 0), SomeError);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task EnsureAsync_TaskResult_AsyncPredicate_WhenError_SkipsPredicate() {
        bool called = false;
        await FailureIntTask()
            .EnsureAsync(v => { called = true; return Task.FromResult(v > 0); }, AnotherError);
        Assert.False(called);
    }

    // ── EnsureAsync — Result<T> + async predicate ─────────────────────────────

    [Fact]
    public async Task EnsureAsync_SyncResult_AsyncPredicate_PredicateTrue() {
        Result<int> result = await SuccessInt(10)
            .EnsureAsync(v => Task.FromResult(v > 0), SomeError);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureAsync_SyncResult_AsyncPredicate_PredicateFalse() {
        Result<int> result = await SuccessInt(-10)
            .EnsureAsync(v => Task.FromResult(v > 0), SomeError);
        Assert.True(result.IsFailure);
    }

    // ── EnsureAsync — dynamic error factory ──────────────────────────────────

    [Fact]
    public async Task EnsureAsync_DynamicErrorFactory_WhenPredicateFalse_UsesFactory() {
        Result<int> result = await SuccessIntTask(-3)
            .EnsureAsync(
                predicate: v => Task.FromResult(v > 0),
                errorFactory: v => Task.FromResult(Error.Validation("Range.Invalid", $"Value {v} is not positive")));

        Assert.True(result.IsFailure);
        Assert.Equal("Range.Invalid", result.FirstError.Code);
        Assert.Contains("-3", result.FirstError.Description);
    }

    [Fact]
    public async Task EnsureAsync_DynamicErrorFactory_WhenPredicateTrue_DoesNotCallFactory() {
        bool factoryCalled = false;
        Result<int> result = await SuccessIntTask(5)
            .EnsureAsync(
                predicate: v => Task.FromResult(v > 0),
                errorFactory: v => { factoryCalled = true; return Task.FromResult(SomeError); });

        Assert.False(factoryCalled);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureAsync_DynamicErrorFactory_WhenAlreadyError_SkipsBoth() {
        bool predicateCalled = false;
        bool factoryCalled = false;

        await FailureIntTask()
            .EnsureAsync(
                predicate: v => { predicateCalled = true; return Task.FromResult(v > 0); },
                errorFactory: v => { factoryCalled = true; return Task.FromResult(SomeError); });

        Assert.False(predicateCalled);
        Assert.False(factoryCalled);
    }

    // ── Chaining Ensure ───────────────────────────────────────────────────────

    [Fact]
    public void Ensure_Chained_FirstFailingPredicate_StopsChain() {
        bool secondCalled = false;
        Result<int> result = SuccessInt(5)
            .Ensure(v => v > 10, SomeError)
            .Ensure(v => { secondCalled = true; return v > 0; }, AnotherError);

        Assert.False(secondCalled);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void Ensure_Chained_AllPassing_ReturnsSuccess() {
        Result<int> result = SuccessInt(5)
            .Ensure(v => v > 0, SomeError)
            .Ensure(v => v < 100, AnotherError);
        Assert.True(result.IsSuccess);
    }
}