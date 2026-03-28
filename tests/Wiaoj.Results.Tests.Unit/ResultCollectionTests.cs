namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", "Collection")]
public sealed class ResultCollectionTests {

    // ── IEnumerable<Result<T>>.Combine ────────────────────────────────────────

    [Fact]
    public void Combine_WhenAllSucceed_ReturnsAllValues() {
        IEnumerable<Result<int>> source = [
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3)
        ];
        Result<IReadOnlyList<int>> result = source.Combine();
        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], result.Value);
    }

    [Fact]
    public void Combine_WhenOneFails_ReturnsErrors() {
        IEnumerable<Result<int>> source = [
            Result<int>.Success(1),
            (Result<int>)SomeError,
            Result<int>.Success(3)
        ];
        Result<IReadOnlyList<int>> result = source.Combine();
        Assert.True(result.IsFailure);
        Assert.Contains(SomeError, result.Errors);
    }

    [Fact]
    public void Combine_WhenMultipleFail_CollectsAllErrors() {
        IEnumerable<Result<int>> source = [
            (Result<int>)SomeError,
            (Result<int>)AnotherError
        ];
        Result<IReadOnlyList<int>> result = source.Combine();
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(SomeError, result.Errors);
        Assert.Contains(AnotherError, result.Errors);
    }

    [Fact]
    public void Combine_WhenMultipleErrorsPerResult_CollectsAll() {
        List<Error> twoErrors = [SomeError, AnotherError];
        IEnumerable<Result<int>> source = [
            (Result<int>)twoErrors,
            (Result<int>)NotFoundError
        ];
        Result<IReadOnlyList<int>> result = source.Combine();
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Combine_WithEmptySource_ReturnsEmptyList() {
        Result<IReadOnlyList<int>> result =
            Enumerable.Empty<Result<int>>().Combine();
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void Combine_WhenAllFail_DoesNotIncludeValues() {
        IEnumerable<Result<int>> source = [
            (Result<int>)SomeError,
            (Result<int>)AnotherError
        ];
        Result<IReadOnlyList<int>> result = source.Combine();
        Assert.True(result.IsFailure);
    }

    // ── WhereSuccess ──────────────────────────────────────────────────────────

    [Fact]
    public void WhereSuccess_ReturnsOnlySuccessfulValues() {
        IEnumerable<Result<int>> source = [
            Result<int>.Success(1),
            (Result<int>)SomeError,
            Result<int>.Success(3),
            (Result<int>)AnotherError
        ];
        int[] values = source.WhereSuccess().ToArray();
        Assert.Equal([1, 3], values);
    }

    [Fact]
    public void WhereSuccess_WhenAllSucceed_ReturnsAll() {
        IEnumerable<Result<int>> source = [
            Result<int>.Success(1),
            Result<int>.Success(2)
        ];
        Assert.Equal([1, 2], source.WhereSuccess().ToArray());
    }

    [Fact]
    public void WhereSuccess_WhenAllFail_ReturnsEmpty() {
        IEnumerable<Result<int>> source = [
            (Result<int>)SomeError,
            (Result<int>)AnotherError
        ];
        Assert.Empty(source.WhereSuccess());
    }

    [Fact]
    public void WhereSuccess_IsLazy_DoesNotEvaluateUpfront() {
        int evaluated = 0;
        IEnumerable<Result<int>> source = Yield();
        IEnumerable<int> lazy = source.WhereSuccess();
        Assert.Equal(0, evaluated); // nothing evaluated yet

        lazy.ToList();
        Assert.Equal(3, evaluated);

        IEnumerable<Result<int>> Yield() {
            evaluated++; yield return Result<int>.Success(1);
            evaluated++; yield return (Result<int>)SomeError;
            evaluated++; yield return Result<int>.Success(3);
        }
    }

    // ── WhereFailure ──────────────────────────────────────────────────────────

    [Fact]
    public void WhereFailure_ReturnsOnlyErrors() {
        IEnumerable<Result<int>> source = [
            Result<int>.Success(1),
            (Result<int>)SomeError,
            Result<int>.Success(3),
            (Result<int>)AnotherError
        ];
        Error[] errors = source.WhereFailure().ToArray();
        Assert.Equal(2, errors.Length);
        Assert.Contains(SomeError, errors);
        Assert.Contains(AnotherError, errors);
    }

    [Fact]
    public void WhereFailure_FlattensMultipleErrorsPerResult() {
        List<Error> twoErrors = [SomeError, AnotherError];
        IEnumerable<Result<int>> source = [
            (Result<int>)twoErrors,
            (Result<int>)NotFoundError
        ];
        Error[] errors = source.WhereFailure().ToArray();
        Assert.Equal(3, errors.Length);
    }

    [Fact]
    public void WhereFailure_WhenAllSucceed_ReturnsEmpty() {
        IEnumerable<Result<int>> source = [
            Result<int>.Success(1), Result<int>.Success(2)
        ];
        Assert.Empty(source.WhereFailure());
    }

    // ── ToResult (reference type) ─────────────────────────────────────────────

    [Fact]
    public void ToResult_ReferenceType_WhenNotNull_ReturnsSuccess() {
        string? value = "hello";
        Result<string> result = value.ToResult(SomeError);
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void ToResult_ReferenceType_WhenNull_ReturnsError() {
        string? value = null;
        Result<string> result = value.ToResult(SomeError);
        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void ToResult_ReferenceType_LazyFactory_WhenNotNull_DoesNotCallFactory() {
        string? value = "x";
        bool called = false;
        value.ToResult(() => { called = true; return SomeError; });
        Assert.False(called);
    }

    [Fact]
    public void ToResult_ReferenceType_LazyFactory_WhenNull_CallsFactory() {
        string? value = null;
        bool called = false;
        Result<string> result = value.ToResult(() => { called = true; return SomeError; });
        Assert.True(called);
        Assert.True(result.IsFailure);
    }

    // ── ToResult (value type) ─────────────────────────────────────────────────

    [Fact]
    public void ToResult_ValueType_WhenHasValue_ReturnsSuccess() {
        int? value = 42;
        Result<int> result = value.ToResult(SomeError);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToResult_ValueType_WhenNull_ReturnsError() {
        int? value = null;
        Result<int> result = value.ToResult(SomeError);
        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void ToResult_ValueType_LazyFactory_WhenHasValue_DoesNotCallFactory() {
        int? value = 1;
        bool called = false;
        value.ToResult(() => { called = true; return SomeError; });
        Assert.False(called);
    }

    [Fact]
    public void ToResult_ValueType_LazyFactory_WhenNull_CallsFactory() {
        int? value = null;
        bool called = false;
        value.ToResult(() => { called = true; return SomeError; });
        Assert.True(called);
    }

    // ── ValueTask<T>.AsResult() ───────────────────────────────────────────────

    [Fact]
    public async Task AsResult_ValueTask_WrapsValueInSuccess() {
        ValueTask<int> vt = ValueTask.FromResult(99);
        Result<int> result = await vt.AsResult();
        Assert.True(result.IsSuccess);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task AsResult_ValueTask_WithNullableRef_WhenNotNull_ReturnsSuccess() {
        ValueTask<string?> vt = ValueTask.FromResult<string?>("hello");
        Result<string> result = await vt.AsResult(SomeError);
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public async Task AsResult_ValueTask_WithNullableRef_WhenNull_ReturnsError() {
        ValueTask<string?> vt = ValueTask.FromResult<string?>(null);
        Result<string> result = await vt.AsResult(SomeError);
        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.FirstError);
    }
}