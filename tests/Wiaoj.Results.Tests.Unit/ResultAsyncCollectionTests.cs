using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Async)]
public sealed class ResultAsyncCollectionTests {

    public sealed class TheCombineAsyncMethod {
        [Fact]
        public async Task CombineAsync_WhenAllTasksSucceed_ReturnsCombinedList() {
            // Arrange
            List<Task<Result<int>>> tasks = [
                Task.FromResult(Result.Success(1)),
                Task.FromResult(Result.Success(2)),
                Task.FromResult(Result.Success(3))
            ];

            // Act
            Result<IReadOnlyList<int>> result = await tasks.CombineAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal([1, 2, 3], result.Value);
        }

        [Fact]
        public async Task CombineAsync_WhenAnyTaskFails_AggregatesAllErrors() {
            // Arrange
            List<Task<Result<int>>> tasks = [
                Task.FromResult(Result.Success(1)),
                Task.FromResult(Result.Failure<int>(SomeError)),
                Task.FromResult(Result.Failure<int>(AnotherError))
            ];

            // Act
            Result<IReadOnlyList<int>> result = await tasks.CombineAsync();

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains(SomeError, result.Errors);
            Assert.Contains(AnotherError, result.Errors);
        }
    }

    public sealed class ThePartitionAsyncMethod {
        [Fact]
        public async Task PartitionAsync_MixedTasks_CorrectlySplitsSuccessesAndFailures() {
            // Arrange
            List<Task<Result<string>>> tasks = [
                Task.FromResult(Result.Success("item-1")),
                Task.FromResult(Result.Failure<string>(SomeError)),
                Task.FromResult(Result.Success("item-2")),
                Task.FromResult(Result.Failure<string>(AnotherError))
            ];

            // Act
            var (successes, failures) = await tasks.PartitionAsync();

            // Assert
            Assert.Equal(["item-1", "item-2"], successes);
            Assert.Equal(2, failures.Count);
            Assert.Equal(SomeError, failures[0]);
            Assert.Equal(AnotherError, failures[1]);
        }
    }
}