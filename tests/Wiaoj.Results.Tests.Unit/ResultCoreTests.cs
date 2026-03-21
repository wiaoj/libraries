using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Core)]
public sealed class ResultCoreTests {

    // ── IsSuccess / IsError ───────────────────────────────────────────────────

    [Fact]
    public void IsSuccess_WhenCreatedFromValue_IsTrue() {
        Result<int> result = 42;
        Assert.True(result.IsSuccess);
        Assert.False(result.IsError);
    }

    [Fact]
    public void IsError_WhenCreatedFromError_IsTrue() {
        Result<int> result = SomeError;
        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
    }

    // ── Value access ──────────────────────────────────────────────────────────

    [Fact]
    public void Value_WhenSuccess_ReturnsValue() {
        Result<int> result = 99;
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public void Value_WhenError_ThrowsInvalidOperationException() {
        Result<int> result = SomeError;
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Value_WhenError_ExceptionMessageMentionsIsSuccess() {
        Result<int> result = SomeError;
        InvalidOperationException ex =
            Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("IsSuccess", ex.Message);
    }

    // ── FirstError access ─────────────────────────────────────────────────────

    [Fact]
    public void FirstError_WhenError_ReturnsFirstError() {
        List<Error> errors = [SomeError, AnotherError];
        Result<int> result = errors;
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void FirstError_WhenSuccess_ThrowsInvalidOperationException() {
        Result<int> result = 42;
        Assert.Throws<InvalidOperationException>(() => result.FirstError);
    }

    [Fact]
    public void FirstError_WhenSuccess_ExceptionMessageMentionsIsError() {
        Result<int> result = 42;
        InvalidOperationException ex =
            Assert.Throws<InvalidOperationException>(() => result.FirstError);
        Assert.Contains("IsError", ex.Message);
    }

    // ── Errors collection ─────────────────────────────────────────────────────

    [Fact]
    public void Errors_WhenSuccess_ReturnsEmptyList() {
        Result<int> result = 42;
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Errors_WhenError_ReturnsAllErrors() {
        List<Error> errors = [SomeError, AnotherError];
        Result<int> result = errors;
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(SomeError, result.Errors[0]);
        Assert.Equal(AnotherError, result.Errors[1]);
    }

    [Fact]
    public void Errors_WhenSingleError_ReturnsOneError() {
        Result<int> result = SomeError;
        Assert.Single(result.Errors);
        Assert.Equal(SomeError, result.Errors[0]);
    }

    // ── Implicit operators ────────────────────────────────────────────────────

    [Fact]
    public void ImplicitFromValue_CreatesSuccessResult() {
        Result<string> result = "hello";
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void ImplicitFromError_CreatesFailedResult() {
        Result<string> result = SomeError;
        Assert.True(result.IsError);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void ImplicitFromErrorList_CreatesFailedResult() {
        List<Error> errors = [SomeError, AnotherError];
        Result<int> result = errors;
        Assert.True(result.IsError);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ImplicitFromErrorArray_CreatesFailedResult() {
        Error[] errors = [SomeError, AnotherError];
        Result<int> result = errors;
        Assert.True(result.IsError);
        Assert.Equal(2, result.Errors.Count);
    }

    // ── Empty error list guard ────────────────────────────────────────────────

    [Fact]
    public void ImplicitFromEmptyErrorList_ThrowsArgumentException() {
        Assert.Throws<ArgumentException>(() => {
            Result<int> _ = new List<Error>();
        });
    }

    // ── Static factory ────────────────────────────────────────────────────────

    [Fact]
    public void StaticSuccess_CreatesSuccessResult() {
        Result<int> result = Result<int>.Success(77);
        Assert.True(result.IsSuccess);
        Assert.Equal(77, result.Value);
    }

    [Fact]
    public void ResultStaticSuccess_WithValue_CreatesSuccessResult() {
        Result<int> result = Result.Success(55);
        Assert.True(result.IsSuccess);
        Assert.Equal(55, result.Value);
    }

    [Fact]
    public void ResultStaticSuccess_Void_CreatesSuccessResult() {
        Result<Success> result = Result.Success();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ResultStaticFailure_WithError_CreatesFailedResult() {
        Result<Success> result = Result.Failure(SomeError);
        Assert.True(result.IsError);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void ResultStaticFailure_WithErrorList_CreatesFailedResult() {
        List<Error> errors = [SomeError, AnotherError];
        Result<Success> result = Result.Failure(errors);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ResultStaticFailureGeneric_CreatesFailedResultOfThatType() {
        Result<int> result = Result.Failure<int>(SomeError);
        Assert.True(result.IsError);
    }

    // ── Value semantics (record struct) ───────────────────────────────────────

    [Fact]
    public void TwoSuccessResults_WithSameValue_AreEqual() {
        Result<int> a = 42;
        Result<int> b = 42;
        Assert.Equal(a, b);
    }

    [Fact]
    public void TwoErrorResults_WithSameError_AreEqual() {
        Result<int> a = SomeError;
        Result<int> b = SomeError;
        Assert.Equal(a, b);
    }

    [Fact]
    public void SuccessResult_AndErrorResult_AreNotEqual() {
        Result<int> a = 42;
        Result<int> b = SomeError;
        Assert.NotEqual(a, b);
    }

    // ── Null value ────────────────────────────────────────────────────────────

    [Fact]
    public void SuccessResult_WithNullValue_IsSuccess() {
        Result<string?> result = (string?)null;
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }
}