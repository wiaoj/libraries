using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.ValueTask)]
public sealed class ResultValueTaskTests {

    private static ValueTask<Result<int>> SuccessIntVT(int value = 42) => ValueTask.FromResult(SuccessInt(value));
    private static ValueTask<Result<int>> FailureIntVT() => ValueTask.FromResult(FailureInt());

    public sealed class TheThenAsyncMethod {
        [Fact]
        public async Task ThenAsync_ValueTask_WhenSuccess_ExecutesNext() {
            // Arrange
            ValueTask<Result<int>> initial = SuccessIntVT(5);

            // Act
            Result<string> result = await initial.ThenAsync(v => ValueTask.FromResult(Result.Success($"vt:{v}")));

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("vt:5", result.Value);
        }

        [Fact]
        public async Task ThenAsync_ValueTask_WhenFailure_ShortCircuits() {
            // Arrange
            ValueTask<Result<int>> initial = FailureIntVT();
            bool nextInvoked = false;

            // Act
            Result<string> result = await initial.ThenAsync(v => {
                nextInvoked = true;
                return ValueTask.FromResult(Result.Success($"{v}"));
            });

            // Assert
            Assert.False(nextInvoked);
            Assert.True(result.IsFailure);
        }
    }

    public sealed class TheEnsureAsyncMethod {
        [Fact]
        public async Task EnsureAsync_ValueTask_WhenPredicateTrue_ReturnsSuccess() {
            // Arrange
            ValueTask<Result<int>> initial = SuccessIntVT(10);

            // Act
            Result<int> result = await initial.EnsureAsync(v => v > 0, SomeError);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(10, result.Value);
        }

        [Fact]
        public async Task EnsureAsync_ValueTask_WhenPredicateFalse_ReturnsError() {
            // Arrange
            ValueTask<Result<int>> initial = SuccessIntVT(-5);

            // Act
            Result<int> result = await initial.EnsureAsync(v => v > 0, SomeError);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(SomeError, result.FirstError);
        }

        [Fact]
        public async Task EnsureAsync_ValueTask_AsyncPredicate_ReturnsExpectedResult() {
            // Arrange
            ValueTask<Result<int>> initial = SuccessIntVT(10);

            // Act
            Result<int> result = await initial.EnsureAsync(v => ValueTask.FromResult(v > 5), SomeError);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(10, result.Value);
        }
    }

    public sealed class TheDoAsyncAndTapAsyncMethods {
        [Fact]
        public async Task DoAsync_ValueTask_WhenSuccess_ExecutesAction() {
            // Arrange
            ValueTask<Result<int>> initial = SuccessIntVT(20);
            int capturedValue = 0;

            // Act
            Result<int> result = await initial.DoAsync(v => { capturedValue = v; });

            // Assert
            Assert.Equal(20, capturedValue);
            Assert.Equal(20, result.Value);
        }

        [Fact]
        public async Task TapAsync_ValueTask_WhenFailure_DoesNotExecuteAction() {
            // Arrange
            ValueTask<Result<int>> initial = FailureIntVT();
            bool executed = false;

            // Act
            await initial.TapAsync(_ => { executed = true; });

            // Assert
            Assert.False(executed);
        }
    }

    public sealed class TheBiMapAsyncMethod {
        [Fact]
        public async Task BiMapAsync_ValueTask_WhenSuccess_MapsSuccessValue() {
            // Arrange
            ValueTask<Result<int>> initial = SuccessIntVT(7);

            // Act
            Result<string> result = await initial.BiMapAsync(
                v => $"num:{v}",
                _ => AnotherError);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("num:7", result.Value);
        }

        [Fact]
        public async Task BiMapAsync_ValueTask_WhenFailure_MapsError() {
            // Arrange
            ValueTask<Result<int>> initial = FailureIntVT();

            // Act
            Result<string> result = await initial.BiMapAsync(
                v => $"num:{v}",
                _ => AnotherError);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(AnotherError, result.FirstError);
        }
    }

    public sealed class TheAsValueTaskMethod {
        [Fact]
        public async Task AsValueTask_FromResult_WrapsInCompletedValueTask() {
            // Arrange
            Result<int> initial = SuccessInt(11);

            // Act
            ValueTask<Result<int>> valueTask = initial.AsValueTask();
            Result<int> result = await valueTask;

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(11, result.Value);
        }
    }
}