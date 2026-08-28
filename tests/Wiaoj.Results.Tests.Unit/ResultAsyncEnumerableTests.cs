using System.Runtime.CompilerServices;
using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.AsyncEnumerable)]
public sealed class ResultAsyncEnumerableTests {

    private static async IAsyncEnumerable<Result<T>> ToAsyncStream<T>(
        IEnumerable<Result<T>> items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        foreach(Result<T> item in items) {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }

    public sealed class TheWhereSuccessMethod {
        [Fact]
        public async Task WhereSuccess_YieldsOnlyValuesFromSuccessfulResults() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(10),
                Result.Failure<int>(SomeError),
                Result.Success(20),
                Result.Failure<int>(AnotherError),
                Result.Success(30)
            ];
            IAsyncEnumerable<Result<int>> stream = ToAsyncStream(source);
            List<int> collected = [];

            // Act
            await foreach(int value in stream.WhereSuccess()) {
                collected.Add(value);
            }

            // Assert
            Assert.Equal([10, 20, 30], collected);
        }

        [Fact]
        public async Task WhereSuccess_WhenAllFail_YieldsNothing() {
            // Arrange
            List<Result<int>> source = [
                Result.Failure<int>(SomeError),
                Result.Failure<int>(AnotherError)
            ];
            IAsyncEnumerable<Result<int>> stream = ToAsyncStream(source);
            List<int> collected = [];

            // Act
            await foreach(int value in stream.WhereSuccess()) {
                collected.Add(value);
            }

            // Assert
            Assert.Empty(collected);
        }
    }

    public sealed class TheWhereFailureMethod {
        [Fact]
        public async Task WhereFailure_YieldsFlattenedErrorsFromFailedResults() {
            // Arrange
            List<Error> multipleErrors = [SomeError, AnotherError];
            List<Result<int>> source = [
                Result.Success(10),
                multipleErrors,
                Result.Success(20),
                NotFoundError
            ];
            IAsyncEnumerable<Result<int>> stream = ToAsyncStream(source);
            List<Error> collected = [];

            // Act
            await foreach(Error error in stream.WhereFailure()) {
                collected.Add(error);
            }

            // Assert
            Assert.Equal(3, collected.Count);
            Assert.Equal(SomeError, collected[0]);
            Assert.Equal(AnotherError, collected[1]);
            Assert.Equal(NotFoundError, collected[2]);
        }
    }

    public sealed class TheCombineAsyncMethod {
        [Fact]
        public async Task CombineAsync_WhenAllSucceed_ReturnsCombinedList() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(1),
                Result.Success(2),
                Result.Success(3)
            ];
            IAsyncEnumerable<Result<int>> stream = ToAsyncStream(source);

            // Act
            Result<IReadOnlyList<int>> result = await stream.CombineAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal([1, 2, 3], result.Value);
        }

        [Fact]
        public async Task CombineAsync_WhenAnyFails_AggregatesAllErrors() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(1),
                Result.Failure<int>(SomeError),
                Result.Failure<int>(AnotherError)
            ];
            IAsyncEnumerable<Result<int>> stream = ToAsyncStream(source);

            // Act
            Result<IReadOnlyList<int>> result = await stream.CombineAsync();

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains(SomeError, result.Errors);
            Assert.Contains(AnotherError, result.Errors);
        }

        [Fact]
        public async Task CombineAsync_WhenEmptyStream_ReturnsEmptySuccessList() {
            // Arrange
            IAsyncEnumerable<Result<int>> stream = ToAsyncStream(Enumerable.Empty<Result<int>>());

            // Act
            Result<IReadOnlyList<int>> result = await stream.CombineAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value);
        }
    }

    public sealed class ThePartitionAsyncMethod {
        [Fact]
        public async Task PartitionAsync_CorrectlySeparatesStreamIntoSuccessesAndFailures() {
            // Arrange
            List<Result<string>> source = [
                Result.Success("item-1"),
                Result.Failure<string>(SomeError),
                Result.Success("item-2"),
                Result.Failure<string>(AnotherError)
            ];
            IAsyncEnumerable<Result<string>> stream = ToAsyncStream(source);

            // Act
            var (successes, failures) = await stream.PartitionAsync();

            // Assert
            Assert.Equal(["item-1", "item-2"], successes);
            Assert.Equal(2, failures.Count);
            Assert.Equal(SomeError, failures[0]);
            Assert.Equal(AnotherError, failures[1]);
        }
    }

    public sealed class TheStreamTransformations {
        [Fact]
        public async Task MapAsync_TransformsEachSuccessInStream() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(2),
                Result.Failure<int>(SomeError),
                Result.Success(4)
            ];
            IAsyncEnumerable<Result<int>> stream = ToAsyncStream(source);
            List<Result<string>> collected = [];

            // Act
            await foreach(Result<string> item in stream.MapAsync(x => $"val:{x * 10}")) {
                collected.Add(item);
            }

            // Assert
            Assert.Equal(3, collected.Count);
            Assert.Equal("val:20", collected[0].Value);
            Assert.True(collected[1].IsFailure);
            Assert.Equal("val:40", collected[2].Value);
        }

        [Fact]
        public async Task ThenAsync_ChainsEachSuccessInStream() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(5),
                Result.Success(-1)
            ];
            IAsyncEnumerable<Result<int>> stream = ToAsyncStream(source);
            List<Result<string>> collected = [];

            // Act
            await foreach(Result<string> item in stream.ThenAsync(x =>
                Task.FromResult(x > 0 ? Result.Success($"positive:{x}") : Result.Failure<string>(SomeError)))) {
                collected.Add(item);
            }

            // Assert
            Assert.Equal(2, collected.Count);
            Assert.Equal("positive:5", collected[0].Value);
            Assert.True(collected[1].IsFailure);
        }
    }
}