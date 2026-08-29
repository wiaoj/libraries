using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Disposal)]
public sealed class ResultDisposalTests {

    private sealed class TrackingDisposable : IDisposable {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class TrackingAsyncDisposable : IAsyncDisposable {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync() {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class TheConsumeMethod {
        [Fact]
        public void Consume_WhenSuccess_ExecutesActionAndDisposesValue() {
            // Arrange
            TrackingDisposable disposable = new();
            Result<TrackingDisposable> result = disposable;
            bool actionExecuted = false;

            // Act
            result.Consume(v => {
                actionExecuted = true;
                Assert.Same(disposable, v);
            });

            // Assert
            Assert.True(actionExecuted);
            Assert.True(disposable.Disposed);
        }

        [Fact]
        public void Consume_WhenFailure_DoesNotExecuteAction() {
            // Arrange
            Result<TrackingDisposable> result = SomeError;
            bool actionExecuted = false;

            // Act
            result.Consume(_ => { actionExecuted = true; });

            // Assert
            Assert.False(actionExecuted);
        }
    }

    public sealed class TheDisposeValueMethods {
        [Fact]
        public void DisposeValue_WhenSuccess_DisposesUnderlyingValue() {
            // Arrange
            TrackingDisposable disposable = new();
            Result<TrackingDisposable> result = disposable;

            // Act
            result.DisposeValue();

            // Assert
            Assert.True(disposable.Disposed);
        }

        [Fact]
        public async Task DisposeValueAsync_WhenSuccess_AsynchronouslyDisposesUnderlyingValue() {
            // Arrange
            TrackingAsyncDisposable disposable = new();
            Result<TrackingAsyncDisposable> result = disposable;

            // Act
            await result.DisposeValueAsync();

            // Assert
            Assert.True(disposable.Disposed);
        }
    }
}