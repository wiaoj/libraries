namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Combinators)]
public sealed class ResultCombinatorTests {

    public sealed class TheThenMethod {
        [Fact]
        public void Then_WhenSuccess_ExecutesNextAndReturnsItsResult() {
            // Arrange
            Result<int> initial = 10;

            // Act
            Result<string> next = initial.Then(v => Result.Success($"val:{v}"));

            // Assert
            Assert.True(next.IsSuccess);
            Assert.Equal("val:10", next.Value);
        }

        [Fact]
        public void Then_WhenFailure_ShortCircuitsAndPropagatesErrors() {
            // Arrange
            Result<int> initial = SomeError;
            bool nextInvoked = false;

            // Act
            Result<string> next = initial.Then(v => {
                nextInvoked = true;
                return Result.Success($"val:{v}");
            });

            // Assert
            Assert.False(nextInvoked);
            Assert.True(next.IsFailure);
            Assert.Equal(SomeError, next.FirstError);
        }
    }

    public sealed class TheMapMethod {
        [Fact]
        public void Map_WhenSuccess_TransformsValue() {
            // Arrange
            Result<int> initial = 5;

            // Act
            Result<string> mapped = initial.Map(v => $"num:{v}");

            // Assert
            Assert.True(mapped.IsSuccess);
            Assert.Equal("num:5", mapped.Value);
        }

        [Fact]
        public void Map_WhenFailure_PropagatesErrorsWithoutInvokingMapper() {
            // Arrange
            Result<int> initial = SomeError;
            bool mapperInvoked = false;

            // Act
            Result<string> mapped = initial.Map(v => {
                mapperInvoked = true;
                return $"{v}";
            });

            // Assert
            Assert.False(mapperInvoked);
            Assert.True(mapped.IsFailure);
            Assert.Equal(SomeError, mapped.FirstError);
        }
    }

    public sealed class TheDoMethod {
        [Fact]
        public void Do_WhenSuccess_ExecutesSideEffectAndReturnsSelf() {
            // Arrange
            Result<int> initial = 42;
            int capturedValue = 0;

            // Act
            Result<int> returned = initial.Do(v => { capturedValue = v; });

            // Assert
            Assert.Equal(42, capturedValue);
            Assert.Equal(initial, returned);
        }

        [Fact]
        public void Do_WhenFailure_DoesNotExecuteSideEffect() {
            // Arrange
            Result<int> initial = SomeError;
            bool actionInvoked = false;

            // Act
            initial.Do(_ => { actionInvoked = true; });

            // Assert
            Assert.False(actionInvoked);
        }
    }

    public sealed class TheRecoverMethod {
        [Fact]
        public void Recover_WhenFailureWithSingleError_ReturnsFallbackValue() {
            // Arrange
            Result<int> initial = SomeError;

            // Act
            Result<int> recovered = initial.Recover(errors => {
                Assert.Single(errors);
                return 99;
            });

            // Assert
            Assert.True(recovered.IsSuccess);
            Assert.Equal(99, recovered.Value);
        }

        [Fact]
        public void Recover_WhenFailureWithMultipleErrors_PassesAllErrorsToFallback() {
            // Arrange
            List<Error> errors = [SomeError, AnotherError];
            Result<int> initial = errors;

            // Act
            Result<int> recovered = initial.Recover(e => e.Count * 10);

            // Assert
            Assert.True(recovered.IsSuccess);
            Assert.Equal(20, recovered.Value);
        }

        [Fact]
        public void Recover_WhenSuccess_DoesNotInvokeFallback() {
            // Arrange
            Result<int> initial = 42;
            bool fallbackInvoked = false;

            // Act
            Result<int> recovered = initial.Recover(_ => {
                fallbackInvoked = true;
                return 99;
            });

            // Assert
            Assert.False(fallbackInvoked);
            Assert.Equal(42, recovered.Value);
        }
    }

    public sealed class TheIfSuccessAndIfFailureMethods {
        [Fact]
        public void IfSuccess_WhenSuccess_ExecutesAction() {
            // Arrange
            Result<int> initial = 5;
            int capturedValue = 0;

            // Act
            initial.IfSuccess(v => { capturedValue = v; });

            // Assert
            Assert.Equal(5, capturedValue);
        }

        [Fact]
        public void IfFailure_WhenFailureWithSingleError_ExecutesActionWithErrors() {
            // Arrange
            Result<int> initial = SomeError;
            bool actionInvoked = false;
            IReadOnlyList<Error>? capturedErrors = null;

            // Act
            initial.IfFailure(errors => {
                actionInvoked = true;
                capturedErrors = errors;
            });

            // Assert
            Assert.True(actionInvoked);
            Assert.NotNull(capturedErrors);
            Assert.Single(capturedErrors);
            Assert.Equal(SomeError, capturedErrors[0]);
        }
    }
}