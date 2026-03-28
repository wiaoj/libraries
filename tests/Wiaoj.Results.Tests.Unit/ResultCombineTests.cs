namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", "Combine")]
public sealed class ResultCombineTests {

    // ── Result.All (array) ────────────────────────────────────────────────────

    [Fact]
    public void All_WhenAllSucceed_ReturnsSuccess() {
        Result<Success> result = Result.All(
            Result.Success(), Result.Success(), Result.Success());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void All_WhenOneFails_ReturnsError() {
        Result<Success> result = Result.All(
            Result.Success(),
            Result.Failure(SomeError),
            Result.Success());
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void All_WhenOneFails_ContainsThatError() {
        Result<Success> result = Result.All(
            Result.Success(),
            Result.Failure(SomeError));
        Assert.Contains(SomeError, result.Errors);
    }

    [Fact]
    public void All_CollectsErrorsFromAllFailingResults() {
        Result<Success> result = Result.All(
            Result.Failure(SomeError),
            Result.Success(),
            Result.Failure(AnotherError));
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(SomeError, result.Errors);
        Assert.Contains(AnotherError, result.Errors);
    }

    [Fact]
    public void All_WhenMultipleErrorsPerResult_CollectsAll() {
        List<Error> errors1 = [SomeError, AnotherError];
        List<Error> errors2 = [NotFoundError];
        Result<Success> r1 = errors1;
        Result<Success> r2 = errors2;

        Result<Success> result = Result.All(r1, r2);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void All_WithEmptyParams_ReturnsSuccess() {
        Result<Success> result = Result.All();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void All_WhenAllFail_CollectsAllErrors() {
        Result<Success> result = Result.All(
            Result.Failure(SomeError),
            Result.Failure(AnotherError),
            Result.Failure(NotFoundError));
        Assert.Equal(3, result.Errors.Count);
    }

    // ── Result.All (IEnumerable) ──────────────────────────────────────────────

    [Fact]
    public void All_IEnumerable_WhenAllSucceed_ReturnsSuccess() {
        IEnumerable<Result<Success>> results = [
            Result.Success(), Result.Success()
        ];
        Assert.True(Result.All(results).IsSuccess);
    }

    [Fact]
    public void All_IEnumerable_WhenOneFails_CollectsErrors() {
        IEnumerable<Result<Success>> results = [
            Result.Failure(SomeError), Result.Success()
        ];
        Result<Success> result = Result.All(results);
        Assert.True(result.IsFailure);
        Assert.Contains(SomeError, result.Errors);
    }

    // ── Result.Combine (2-tuple) ──────────────────────────────────────────────

    [Fact]
    public void Combine2_WhenBothSucceed_ReturnsTuple() {
        Result<(int, string)> result = Result.Combine(
            Result<int>.Success(1), Result<string>.Success("a"));
        Assert.True(result.IsSuccess);
        Assert.Equal((1, "a"), result.Value);
    }

    [Fact]
    public void Combine2_WhenFirstFails_ReturnsFirstErrors() {
        Result<(int, string)> result = Result.Combine(
            (Result<int>)SomeError,
            Result<string>.Success("a"));
        Assert.True(result.IsFailure);
        Assert.Contains(SomeError, result.Errors);
    }

    [Fact]
    public void Combine2_WhenSecondFails_ReturnsSecondErrors() {
        Result<(int, string)> result = Result.Combine(
            Result<int>.Success(1),
            (Result<string>)AnotherError);
        Assert.True(result.IsFailure);
        Assert.Contains(AnotherError, result.Errors);
    }

    [Fact]
    public void Combine2_WhenBothFail_CollectsAllErrors() {
        Result<(int, string)> result = Result.Combine(
            (Result<int>)SomeError,
            (Result<string>)AnotherError);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(SomeError, result.Errors);
        Assert.Contains(AnotherError, result.Errors);
    }

    // ── Result.Combine (3-tuple) ──────────────────────────────────────────────

    [Fact]
    public void Combine3_WhenAllSucceed_ReturnsTuple() {
        Result<(int, string, bool)> result = Result.Combine(
            Result<int>.Success(1),
            Result<string>.Success("b"),
            Result<bool>.Success(true));
        Assert.True(result.IsSuccess);
        Assert.Equal((1, "b", true), result.Value);
    }

    [Fact]
    public void Combine3_WhenAllFail_CollectsAllErrors() {
        Result<(int, string, bool)> result = Result.Combine(
            (Result<int>)SomeError,
            (Result<string>)AnotherError,
            (Result<bool>)NotFoundError);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Combine3_WhenMiddleFails_ContainsItsError() {
        Result<(int, string, bool)> result = Result.Combine(
            Result<int>.Success(1),
            (Result<string>)SomeError,
            Result<bool>.Success(true));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal(SomeError, result.FirstError);
    }

    // ── Result.Combine (4-tuple) ──────────────────────────────────────────────

    [Fact]
    public void Combine4_WhenAllSucceed_ReturnsTuple() {
        Result<(int, string, bool, double)> result = Result.Combine(
            Result<int>.Success(1),
            Result<string>.Success("c"),
            Result<bool>.Success(false),
            Result<double>.Success(3.14));
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Item1);
        Assert.Equal("c", result.Value.Item2);
        Assert.False(result.Value.Item3);
        Assert.Equal(3.14, result.Value.Item4);
    }

    [Fact]
    public void Combine4_WhenOneFails_IsError() {
        Result<(int, string, bool, double)> result = Result.Combine(
            Result<int>.Success(1),
            (Result<string>)SomeError,
            Result<bool>.Success(false),
            Result<double>.Success(1.0));
        Assert.True(result.IsFailure);
    }
}