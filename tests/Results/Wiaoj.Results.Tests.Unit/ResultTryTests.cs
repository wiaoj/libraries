using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Try)]
public sealed class ResultTryTests {

    public sealed class TheTrySynchronousMethod {
        [Fact]
        public void Try_WhenOperationSucceeds_ReturnsSuccessResultWithValue() {
            // Arrange & Act
            Result<int> result = Result.Try(() => 42);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Try_WhenOperationThrows_ConvertsExceptionToError() {
            // Arrange & Act
            Result<int> result = Result.Try<int>(() => throw new InvalidOperationException("Boom"));

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
        }

        [Fact]
        public void Try_WithCustomExceptionHandler_UsesProvidedHandler() {
            // Arrange & Act
            Result<int> result = Result.Try<int>(
                () => throw new InvalidOperationException("Custom"),
                ex => Error.Validation("Custom.Validation", ex.Message)
            );

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Custom.Validation", result.FirstError.Code);
            Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        }
    }

    public sealed class TheTryAsyncMethod {
        [Fact]
        public async Task TryAsync_WhenOperationSucceeds_ReturnsSuccessResult() {
            // Arrange & Act
            Result<string> result = await Result.TryAsync(async ct => {
                await Task.Yield();
                return "completed";
            });

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("completed", result.Value);
        }

        [Fact]
        public async Task TryAsync_WhenOperationThrows_ConvertsToError() {
            // Arrange & Act
            Result<string> result = await Result.TryAsync<string>(_ => throw new InvalidOperationException("Async fail"));

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
        }

        [Fact]
        public async Task TryAsync_WhenCancelled_RethrowsOperationCanceledException() {
            // Arrange
            CancellationToken cancelledToken = new(canceled: true);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () => {
                await Result.TryAsync(
                    async ct => {
                        ct.ThrowIfCancellationRequested();
                        await Task.Yield();
                        return "value";
                    },
                    cancellationToken: cancelledToken
                );
            });
        }
    }
}