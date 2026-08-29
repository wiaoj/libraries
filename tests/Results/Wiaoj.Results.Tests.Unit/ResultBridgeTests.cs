using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Bridge)]
public sealed class ResultBridgeTests {

    public sealed class TheAsResultAndAsTaskMethods {
        [Fact]
        public void AsResult_FromValue_WrapsInSuccessfulResult() {
            // Arrange & Act
            Result<int> result = 42.AsResult();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public async Task AsTask_FromResult_ReturnsCompletedTaskContainingResult() {
            // Arrange
            Result<int> original = SuccessInt(7);

            // Act
            Task<Result<int>> task = original.AsTask();
            Result<int> result = await task;

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(7, result.Value);
        }
    }

    public sealed class TheEnsureNotNullMethod {
        [Fact]
        public void EnsureNotNull_WhenValueIsNotNull_ReturnsNonNullableSuccessResult() {
            // Arrange
            Result<string?> input = (string?)"value";

            // Act
            Result<string> result = input.EnsureNotNull(SomeError);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("value", result.Value);
        }

        [Fact]
        public void EnsureNotNull_WhenValueIsNull_ReturnsFailureResult() {
            // Arrange
            Result<string?> input = (string?)null;

            // Act
            Result<string> result = input.EnsureNotNull(SomeError);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(SomeError, result.FirstError);
        }
    }

    public sealed class TheMapErrorAndMapSuccessMethods {
        [Fact]
        public void MapError_WhenFailure_ReplacesErrorWithNewError() {
            // Arrange
            Result<int> failure = FailureInt();

            // Act
            Result<int> result = failure.MapError(AnotherError);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(AnotherError, result.FirstError);
        }

        [Fact]
        public void MapSuccess_WhenSuccess_DiscardsValueAndReturnsSuccessType() {
            // Arrange
            Result<int> success = SuccessInt(99);

            // Act
            Result<Success> result = success.MapSuccess();

            // Assert
            Assert.True(result.IsSuccess);
        }
    }

    public sealed class TheLinqQuerySyntaxExtensions {
        [Fact]
        public void Select_TransformsValueUsingQuerySyntax() {
            // Arrange
            Result<int> initial = 5;

            // Act
            Result<int> result = from x in initial select x * 2;

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(10, result.Value);
        }

        [Fact]
        public void SelectMany_ChainsMultipleResultsUsingQuerySyntax() {
            // Arrange
            Result<int> first = 10;
            Result<int> second = 20;

            // Act
            Result<int> result =
                from a in first
                from b in second
                select a + b;

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(30, result.Value);
        }
    }
}