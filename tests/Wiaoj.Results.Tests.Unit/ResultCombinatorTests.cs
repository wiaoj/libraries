namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Combinators)]
public sealed class ResultCombinatorTests {

    // ── Then ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Then_WhenSuccess_ExecutesNext() {
        Result<int> result = 10;
        Result<string> next = result.Then(v => Result<string>.Success($"val:{v}"));
        Assert.True(next.IsSuccess);
        Assert.Equal("val:10", next.Value);
    }

    [Fact]
    public void Then_WhenError_PropagatesErrors() {
        Result<int> result = SomeError;
        bool nextCalled = false;
        Result<string> next = result.Then(v => {
            nextCalled = true;
            return Result<string>.Success($"val:{v}");
        });
        Assert.False(nextCalled);
        Assert.True(next.IsError);
        Assert.Equal(SomeError, next.FirstError);
    }

    [Fact]
    public void Then_WhenSuccessButNextFails_ReturnsNextErrors() {
        Result<int> result = 10;
        Result<string> next = result.Then(_ => (Result<string>)AnotherError);
        Assert.True(next.IsError);
        Assert.Equal(AnotherError, next.FirstError);
    }

    [Fact]
    public void Then_ChainedMultipleTimes_StopsAtFirstError() {
        int callCount = 0;
        Result<int> result = (Result<int>)SomeError;

        Result<string> final = result
            .Then(v => { callCount++; return Result<int>.Success(v + 1); })
            .Then(v => { callCount++; return Result<string>.Success($"{v}"); });

        Assert.Equal(0, callCount);
        Assert.True(final.IsError);
    }

    // ── Map ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_WhenSuccess_TransformsValue() {
        Result<int> result = 5;
        Result<string> mapped = result.Map(v => $"num:{v}");
        Assert.True(mapped.IsSuccess);
        Assert.Equal("num:5", mapped.Value);
    }

    [Fact]
    public void Map_WhenError_PropagatesErrors() {
        Result<int> result = SomeError;
        bool mapperCalled = false;
        Result<string> mapped = result.Map(v => { mapperCalled = true; return $"{v}"; });
        Assert.False(mapperCalled);
        Assert.True(mapped.IsError);
        Assert.Equal(SomeError, mapped.FirstError);
    }

    [Fact]
    public void Map_ChainedTwice_AppliesBothTransformations() {
        Result<int> result = 3;
        Result<string> mapped = result
            .Map(v => v * 2)
            .Map(v => $"result:{v}");
        Assert.Equal("result:6", mapped.Value);
    }

    // ── Do ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Do_WhenSuccess_ExecutesSideEffect() {
        Result<int> result = 42;
        int captured = 0;
        Result<int> returned = result.Do(v => { captured = v; });
        Assert.Equal(42, captured);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Do_WhenError_DoesNotExecuteSideEffect() {
        Result<int> result = SomeError;
        bool called = false;
        result.Do(_ => { called = true; });
        Assert.False(called);
    }

    [Fact]
    public void Do_ReturnsOriginalResult() {
        Result<int> result = 7;
        Result<int> returned = result.Do(_ => { });
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Do_Parameterless_WhenSuccess_ExecutesSideEffect() {
        Result<int> result = 5;
        bool called = false;
        result.Do(() => { called = true; });
        Assert.True(called);
    }

    [Fact]
    public void Do_Parameterless_WhenError_DoesNotExecute() {
        Result<int> result = SomeError;
        bool called = false;
        result.Do(() => { called = true; });
        Assert.False(called);
    }

    // ── Ensure ────────────────────────────────────────────────────────────────

    [Fact]
    public void Ensure_WhenSuccessAndPredicateTrue_ReturnsOriginal() {
        Result<int> result = 10;
        Result<int> ensured = result.Ensure(v => v > 0, SomeError);
        Assert.True(ensured.IsSuccess);
        Assert.Equal(10, ensured.Value);
    }

    [Fact]
    public void Ensure_WhenSuccessAndPredicateFalse_ReturnsError() {
        Result<int> result = -5;
        Result<int> ensured = result.Ensure(v => v > 0, SomeError);
        Assert.True(ensured.IsError);
        Assert.Equal(SomeError, ensured.FirstError);
    }

    [Fact]
    public void Ensure_WhenAlreadyError_DoesNotCallPredicate() {
        Result<int> result = SomeError;
        bool predicateCalled = false;
        result.Ensure(v => { predicateCalled = true; return v > 0; }, AnotherError);
        Assert.False(predicateCalled);
    }

    [Fact]
    public void Ensure_WhenAlreadyError_PreservesOriginalErrors() {
        Result<int> result = SomeError;
        Result<int> ensured = result.Ensure(_ => false, AnotherError);
        Assert.Equal(SomeError, ensured.FirstError);
    }

    [Fact]
    public void Ensure_ValueIndependent_WhenPredicateTrue_ReturnsOriginal() {
        Result<int> result = 10;
        Result<int> ensured = result.Ensure(() => true, SomeError);
        Assert.True(ensured.IsSuccess);
    }

    [Fact]
    public void Ensure_ValueIndependent_WhenPredicateFalse_ReturnsError() {
        Result<int> result = 10;
        Result<int> ensured = result.Ensure(() => false, SomeError);
        Assert.True(ensured.IsError);
    }

    // ── Recover ───────────────────────────────────────────────────────────────

    [Fact]
    public void Recover_WhenError_ReturnsFallbackValue() {
        Result<int> result = SomeError;
        Result<int> recovered = result.Recover(_ => 99);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(99, recovered.Value);
    }

    [Fact]
    public void Recover_WhenSuccess_DoesNotCallFallback() {
        Result<int> result = 42;
        bool called = false;
        Result<int> recovered = result.Recover(_ => { called = true; return 99; });
        Assert.False(called);
        Assert.Equal(42, recovered.Value);
    }

    [Fact]
    public void Recover_ReceivesErrorsInFallback() {
        List<Error> errors = [SomeError, AnotherError];
        Result<int> result = errors;
        int errorCount = 0;
        result.Recover(e => { errorCount = e.Count; return 0; });
        Assert.Equal(2, errorCount);
    }

    // ── IfSuccess ─────────────────────────────────────────────────────────────

    [Fact]
    public void IfSuccess_WhenSuccess_ExecutesAction() {
        Result<int> result = 5;
        int captured = 0;
        result.IfSuccess(v => { captured = v; });
        Assert.Equal(5, captured);
    }

    [Fact]
    public void IfSuccess_WhenError_DoesNotExecuteAction() {
        Result<int> result = SomeError;
        bool called = false;
        result.IfSuccess(_ => { called = true; });
        Assert.False(called);
    }

    [Fact]
    public void IfSuccess_ReturnsOriginalResult() {
        Result<int> result = 5;
        Result<int> returned = result.IfSuccess(_ => { });
        Assert.Equal(result, returned);
    }

    // ── IfFailure ─────────────────────────────────────────────────────────────

    [Fact]
    public void IfFailure_WhenError_ExecutesAction() {
        Result<int> result = SomeError;
        bool called = false;
        result.IfFailure(_ => { called = true; });
        Assert.True(called);
    }

    [Fact]
    public void IfFailure_WhenSuccess_DoesNotExecuteAction() {
        Result<int> result = 42;
        bool called = false;
        result.IfFailure(_ => { called = true; });
        Assert.False(called);
    }

    [Fact]
    public void IfFailure_ReceivesErrors() {
        List<Error> errors = [SomeError, AnotherError];
        Result<int> result = errors;
        int errorCount = 0;
        result.IfFailure(e => { errorCount = e.Count; });
        Assert.Equal(2, errorCount);
    }

    [Fact]
    public void IfFailure_ReturnsOriginalResult() {
        Result<int> result = SomeError;
        Result<int> returned = result.IfFailure(_ => { });
        Assert.Equal(result, returned);
    }

    // ── Chaining ──────────────────────────────────────────────────────────────

    [Fact]
    public void FullChain_SuccessPath_ExecutesAllSteps() {
        List<string> log = [];

        Result<string> final = SuccessInt(5)
            .Do(v => log.Add($"do:{v}"))
            .Map(v => v * 2)
            .Ensure(v => v > 0, SomeError)
            .Then(v => Result<string>.Success($"ok:{v}"))
            .IfSuccess(v => log.Add($"if:{v}"));

        Assert.True(final.IsSuccess);
        Assert.Equal("ok:10", final.Value);
        Assert.Equal(["do:5", "if:ok:10"], log);
    }

    [Fact]
    public void FullChain_ErrorPath_ShortCircuitsAtFirstError() {
        int steps = 0;

        Result<string> final = SuccessInt(5)
            .Then(_ => { steps++; return FailureInt(); })
            .Map(v => { steps++; return $"{v}"; })
            .Then(v => { steps++; return Result<string>.Success(v); });

        Assert.Equal(1, steps);
        Assert.True(final.IsError);
    }
}