using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Async)]
public sealed class ResultAsyncTests {

    public sealed class TheThenAsyncMethod {
        [Fact]
        public async Task ThenAsync_TaskResult_WhenSuccess_ExecutesNextFunction() {
            // Arrange
            Task<Result<int>> task = SuccessIntTask(10);

            // Act
            Result<string> result = await task.ThenAsync(v => Task.FromResult(Result.Success($"val:{v}")));

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("val:10", result.Value);
        }

        [Fact]
        public async Task ThenAsync_TaskResult_WhenFailure_ShortCircuitsAndPropagatesErrors() {
            // Arrange
            Task<Result<int>> task = FailureIntTask();
            bool nextInvoked = false;

            // Act
            Result<string> result = await task.ThenAsync(v => {
                nextInvoked = true;
                return Task.FromResult(Result.Success($"{v}"));
            });

            // Assert
            Assert.False(nextInvoked);
            Assert.True(result.IsFailure);
        }

        [Fact]
        public async Task ThenAsync_SyncResult_WhenSuccess_ExecutesAsyncNext() {
            // Arrange
            Result<int> initial = SuccessInt(7);

            // Act
            Result<string> result = await initial.ThenAsync(v => Task.FromResult(Result.Success($"ok:{v}")));

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("ok:7", result.Value);
        }

        [Fact]
        public async Task ThenAsync_WithCancellationToken_WhenCancelled_ThrowsOperationCanceledException() {
            // Arrange
            CancellationToken cancelledToken = new(canceled: true);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () => {
                await SuccessIntTask(5).ThenAsync((_, ct) => Task.FromResult(Result.Success("x")), cancelledToken);
            });
        }
    }

    public sealed class TheMapAsyncMethod {
        [Fact]
        public async Task MapAsync_TaskResult_SyncMapper_TransformsValue() {
            // Arrange
            Task<Result<int>> task = SuccessIntTask(5);

            // Act
            Result<string> result = await task.MapAsync(v => $"mapped:{v}");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("mapped:5", result.Value);
        }

        [Fact]
        public async Task MapAsync_TaskResult_AsyncMapper_TransformsValue() {
            // Arrange
            Task<Result<int>> task = SuccessIntTask(3);

            // Act
            Result<string> result = await task.MapAsync(async v => {
                await Task.Yield();
                return $"async:{v}";
            });

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("async:3", result.Value);
        }

        [Fact]
        public async Task MapAsync_SyncResult_AsyncMapper_TransformsValue() {
            // Arrange
            Result<int> initial = SuccessInt(4);

            // Act
            Result<string> result = await initial.MapAsync(async v => {
                await Task.Yield();
                return $"wrapped:{v}";
            });

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("wrapped:4", result.Value);
        }
    }

    public sealed class TheMatchAsyncMethod {
        [Fact]
        public async Task MatchAsync_WhenSuccess_ExecutesOnValueAsync() {
            // Arrange
            Task<Result<int>> task = SuccessIntTask(42);

            // Act
            string output = await task.MatchAsync(
                onValue: v => $"val:{v}",
                onError: _ => "err"
            );

            // Assert
            Assert.Equal("val:42", output);
        }

        [Fact]
        public async Task MatchAsync_WhenFailure_ExecutesOnErrorAsync() {
            // Arrange
            Task<Result<int>> task = FailureIntTask();

            // Act
            string output = await task.MatchAsync(
                onValue: _ => "val",
                onError: _ => "err"
            );

            // Assert
            Assert.Equal("err", output);
        }
    }

    public sealed class TheDoAsyncAndSideEffects {
        [Fact]
        public async Task DoAsync_WhenSuccess_ExecutesAsyncSideEffect() {
            // Arrange
            Task<Result<int>> task = SuccessIntTask(21);
            int capturedValue = 0;

            // Act
            Result<int> result = await task.DoAsync(async (v, _) => {
                await Task.Yield();
                capturedValue = v;
            });

            // Assert
            Assert.Equal(21, capturedValue);
            Assert.Equal(21, result.Value);
        }

        [Fact]
        public async Task DoAsync_WhenFailure_DoesNotExecuteAsyncSideEffect() {
            // Arrange
            Task<Result<int>> task = FailureIntTask();
            bool actionExecuted = false;

            // Act
            await task.DoAsync(async (_, _) => {
                await Task.Yield();
                actionExecuted = true;
            });

            // Assert
            Assert.False(actionExecuted);
        }
    }
}