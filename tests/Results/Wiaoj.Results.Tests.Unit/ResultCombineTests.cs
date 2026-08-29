using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Combine)]
public sealed class ResultCombineTests {

    public sealed class TheAllMethod {
        [Fact]
        public void All_WhenAllResultsSucceed_ReturnsSuccess() {
            // Arrange & Act
            Result<Success> result = Result.All(
                Result.Success(),
                Result.Success(),
                Result.Success()
            );

            // Assert
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void All_WhenSomeResultsFail_AggregatesAllErrors() {
            // Arrange & Act
            Result<Success> result = Result.All(
                Result.Failure(SomeError),
                Result.Success(),
                Result.Failure(AnotherError)
            );

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains(SomeError, result.Errors);
            Assert.Contains(AnotherError, result.Errors);
        }
    }

    public sealed class TheCombineTupleMethod {
        [Fact]
        public void Combine_TwoResults_WhenBothSucceed_ReturnsTuple() {
            // Arrange & Act
            Result<(int, string)> result = Result.Combine(
                Result.Success(10),
                Result.Success("test")
            );

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal((10, "test"), result.Value);
        }

        [Fact]
        public void Combine_TwoResults_WhenBothFail_AggregatesErrors() {
            // Arrange & Act
            Result<(int, string)> result = Result.Combine(
                Result.Failure<int>(SomeError),
                Result.Failure<string>(AnotherError)
            );

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains(SomeError, result.Errors);
            Assert.Contains(AnotherError, result.Errors);
        }
    }
}