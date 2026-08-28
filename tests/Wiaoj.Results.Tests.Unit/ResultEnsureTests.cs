using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Ensure)]
public sealed class ResultEnsureTests {

    public sealed class TheEnsureSynchronousMethod {
        [Fact]
        public void Ensure_WhenSuccessAndPredicateTrue_ReturnsOriginalSuccess() {
            // Arrange
            Result<int> result = SuccessInt(10);

            // Act
            Result<int> ensured = result.Ensure(v => v > 0, SomeError);

            // Assert
            Assert.True(ensured.IsSuccess);
            Assert.Equal(10, ensured.Value);
        }

        [Fact]
        public void Ensure_WhenSuccessAndPredicateFalse_ReturnsSpecifiedError() {
            // Arrange
            Result<int> result = SuccessInt(-5);

            // Act
            Result<int> ensured = result.Ensure(v => v > 0, SomeError);

            // Assert
            Assert.True(ensured.IsFailure);
            Assert.Equal(SomeError, ensured.FirstError);
        }

        [Fact]
        public void Ensure_WhenAlreadyFailure_SkipsPredicateAndPreservesErrors() {
            // Arrange
            Result<int> result = FailureInt();
            bool predicateInvoked = false;

            // Act
            Result<int> ensured = result.Ensure(v => {
                predicateInvoked = true;
                return v > 0;
            }, AnotherError);

            // Assert
            Assert.False(predicateInvoked);
            Assert.Equal(SomeError, ensured.FirstError);
        }
    }

    public sealed class TheEnsureAsyncMethod {
        [Fact]
        public async Task EnsureAsync_TaskResult_AsyncPredicateTrue_ReturnsSuccess() {
            // Arrange
            Task<Result<int>> task = SuccessIntTask(5);

            // Act
            Result<int> result = await task.EnsureAsync(v => Task.FromResult(v > 0), SomeError);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.Value);
        }

        [Fact]
        public async Task EnsureAsync_TaskResult_AsyncPredicateFalse_ReturnsError() {
            // Arrange
            Task<Result<int>> task = SuccessIntTask(-5);

            // Act
            Result<int> result = await task.EnsureAsync(v => Task.FromResult(v > 0), SomeError);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(SomeError, result.FirstError);
        }

        [Fact]
        public async Task EnsureAsync_WithDynamicErrorFactory_GeneratesErrorOnlyOnPredicateFailure() {
            // Arrange
            Task<Result<int>> task = SuccessIntTask(-3);

            // Act
            Result<int> result = await task.EnsureAsync(
                predicate: v => Task.FromResult(v > 0),
                errorFactory: v => Task.FromResult(Error.Validation("Range.Invalid", $"Value {v} must be positive")));

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Range.Invalid", result.FirstError.Code);
            Assert.Contains("-3", result.FirstError.Description);
        }
    }
}