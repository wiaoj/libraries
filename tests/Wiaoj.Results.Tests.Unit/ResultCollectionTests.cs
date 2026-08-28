using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Collection)]
public sealed class ResultCollectionTests {

    public sealed class TheCombineMethod {
        [Fact]
        public void Combine_WhenAllResultsSucceed_ReturnsListWithValues() {
            // Arrange
            List<Result<int>> source = [Result.Success(1), Result.Success(2), Result.Success(3)];

            // Act
            Result<IReadOnlyList<int>> result = source.Combine();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal([1, 2, 3], result.Value);
        }

        [Fact]
        public void Combine_WhenAnyResultFails_ReturnsAggregatedErrors() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(1),
                Result.Failure<int>(SomeError),
                Result.Failure<int>(AnotherError)
            ];

            // Act
            Result<IReadOnlyList<int>> result = source.Combine();

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);
        }
    }

    public sealed class TheFilteringMethods {
        [Fact]
        public void WhereSuccess_ReturnsOnlyUnwrappedValuesFromSuccessfulResults() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(1),
                Result.Failure<int>(SomeError),
                Result.Success(3)
            ];

            // Act
            int[] successfulValues = source.WhereSuccess().ToArray();

            // Assert
            Assert.Equal([1, 3], successfulValues);
        }

        [Fact]
        public void WhereFailure_ReturnsOnlyErrorsFromFailedResults() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(1),
                Result.Failure<int>(SomeError),
                Result.Failure<int>(AnotherError)
            ];

            // Act
            Error[] failureErrors = source.WhereFailure().ToArray();

            // Assert
            Assert.Equal(2, failureErrors.Length);
            Assert.Contains(SomeError, failureErrors);
            Assert.Contains(AnotherError, failureErrors);
        }
    }

    public sealed class TheToResultExtensions {
        [Fact]
        public void ToResult_ReferenceType_WhenNotNull_ReturnsSuccess() {
            // Arrange
            string? value = "content";

            // Act
            Result<string> result = value.ToResult(SomeError);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("content", result.Value);
        }

        [Fact]
        public void ToResult_ReferenceType_WhenNull_ReturnsError() {
            // Arrange
            string? value = null;

            // Act
            Result<string> result = value.ToResult(SomeError);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(SomeError, result.FirstError);
        }
    }
}