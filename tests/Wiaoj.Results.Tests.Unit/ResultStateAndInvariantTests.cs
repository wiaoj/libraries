namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.StateAndInvariants)]
public sealed class ResultStateAndInvariantTests {

    public sealed class TheStateProperties {
        [Fact]
        public void IsSuccess_WhenCreatedFromValue_ReturnsTrue() {
            // Arrange & Act
            Result<int> result = 42;

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
        }

        [Fact]
        public void IsFailure_WhenCreatedFromError_ReturnsTrue() {
            // Arrange & Act
            Result<int> result = SomeError;

            // Assert
            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void DefaultStruct_IsFailure_ReturnsTrue() {
            // Arrange
            Result<int> defaultResult = default;

            // Act & Assert
            Assert.True(defaultResult.IsFailure);
            Assert.False(defaultResult.IsSuccess);
        }
    }

    public sealed class TheDefaultStructBehavior {
        [Fact]
        public void DefaultStruct_Properties_ReturnSafeUninitializedState() {
            // Arrange
            Result<int> defaultResult = default;

            // Act & Assert
            Assert.True(defaultResult.IsFailure);
            Assert.False(defaultResult.IsSuccess);
            Assert.Equal(Error.Uninitialized, defaultResult.FirstError);
            Assert.Single(defaultResult.Errors);
            Assert.Equal(Error.Uninitialized, defaultResult.Errors[0]);
        }

        [Fact]
        public void DefaultStruct_ValueAccess_ThrowsInvalidOperationException() {
            // Arrange
            Result<int> defaultResult = default;

            // Act & Assert - Accessing Value on failure state is strictly forbidden
            Assert.Throws<InvalidOperationException>(() => _ = defaultResult.Value);
        }

        [Fact]
        public void DefaultStruct_WhenMapped_PropagatesUninitializedError() {
            // Arrange
            Result<int> defaultResult = default;

            // Act
            Result<string> mapped = defaultResult.Map(x => x.ToString());

            // Assert
            Assert.True(mapped.IsFailure);
            Assert.Equal(Error.Uninitialized, mapped.FirstError);
            Assert.Equal(Error.Uninitialized, mapped.Errors[0]);
        }
    }

    public sealed class TheValueProperty {
        [Fact]
        public void Value_WhenSuccess_ReturnsUnderlyingValue() {
            // Arrange
            Result<int> result = 99;

            // Act
            int value = result.Value;

            // Assert
            Assert.Equal(99, value);
        }

        [Fact]
        public void Value_WhenFailure_ThrowsInvalidOperationException() {
            // Arrange
            Result<int> result = SomeError;

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = result.Value);
            Assert.Contains("IsSuccess", exception.Message);
        }

        [Fact]
        public void Value_WhenDefaultStruct_ThrowsInvalidOperationException() {
            // Arrange
            Result<int> defaultResult = default;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _ = defaultResult.Value);
        }

        [Fact]
        public void Value_WhenSuccessWithNullableTypeAndNullValue_ReturnsNull() {
            // Arrange
            Result<string?> result = Result.Success<string?>(null);

            // Act & Assert
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value);
        }
    }

    public sealed class TheFirstErrorProperty {
        [Fact]
        public void FirstError_WhenSingleError_ReturnsError() {
            // Arrange
            Result<int> result = SomeError;

            // Act
            Error firstError = result.FirstError;

            // Assert
            Assert.Equal(SomeError, firstError);
        }

        [Fact]
        public void FirstError_WhenMultipleErrors_ReturnsFirstErrorInList() {
            // Arrange
            List<Error> errors = [SomeError, AnotherError];
            Result<int> result = errors;

            // Act
            Error firstError = result.FirstError;

            // Assert
            Assert.Equal(SomeError, firstError);
        }

        [Fact]
        public void FirstError_WhenSuccess_ThrowsInvalidOperationException() {
            // Arrange
            Result<int> result = 42;

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = result.FirstError);
            Assert.Contains("IsFailure", exception.Message);
        }

        [Fact]
        public void FirstError_WhenDefaultStruct_ReturnsUninitializedSentinel() {
            // Arrange
            Result<int> defaultResult = default;

            // Act
            Error firstError = defaultResult.FirstError;

            // Assert
            Assert.Equal(Error.Uninitialized, firstError);
        }
    }

    public sealed class TheErrorsProperty {
        [Fact]
        public void Errors_WhenSuccess_ReturnsEmptyList() {
            // Arrange
            Result<int> result = 42;

            // Act
            IReadOnlyList<Error> errors = result.Errors;

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Errors_WhenSingleError_ReturnsListWithSingleElement() {
            // Arrange
            Result<int> result = SomeError;

            // Act
            IReadOnlyList<Error> errors = result.Errors;

            // Assert
            Assert.Single(errors);
            Assert.Equal(SomeError, errors[0]);
        }

        [Fact]
        public void Errors_WhenMultipleErrors_ReturnsAllErrorsInOrder() {
            // Arrange
            List<Error> originalErrors = [SomeError, AnotherError];
            Result<int> result = originalErrors;

            // Act
            IReadOnlyList<Error> errors = result.Errors;

            // Assert
            Assert.Equal(2, errors.Count);
            Assert.Equal(SomeError, errors[0]);
            Assert.Equal(AnotherError, errors[1]);
        }

        [Fact]
        public void Errors_WhenDefaultStruct_ReturnsListWithUninitializedSentinel() {
            // Arrange
            Result<int> defaultResult = default;

            // Act
            IReadOnlyList<Error> errors = defaultResult.Errors;

            // Assert
            Assert.Single(errors);
            Assert.Equal(Error.Uninitialized, errors[0]);
        }
    }

    public sealed class TheImplicitConversions {
        [Fact]
        public void ImplicitConversion_FromValue_CreatesSuccessResult() {
            // Arrange & Act
            Result<string> result = "test";

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("test", result.Value);
        }

        [Fact]
        public void ImplicitConversion_FromError_CreatesFailureResult() {
            // Arrange & Act
            Result<string> result = SomeError;

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(SomeError, result.FirstError);
        }

        [Fact]
        public void ImplicitConversion_FromErrorList_CreatesFailureResult() {
            // Arrange
            List<Error> errors = [SomeError, AnotherError];

            // Act
            Result<int> result = errors;

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);
        }

        [Fact]
        public void ImplicitConversion_FromEmptyErrorList_ThrowsArgumentException() {
            // Arrange
            List<Error> emptyErrors = [];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => { Result<int> _ = emptyErrors; });
        }
    }

    public sealed class TheEqualityAndHashMembers {
        [Fact]
        public void Equals_WhenBothSuccessWithSameValue_ReturnsTrue() {
            // Arrange
            Result<int> first = 42;
            Result<int> second = 42;

            // Act & Assert
            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Equals_WhenBothFailureWithSameError_ReturnsTrue() {
            // Arrange
            Result<int> first = SomeError;
            Result<int> second = SomeError;

            // Act & Assert
            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Equals_WhenOneSuccessAndOneFailure_ReturnsFalse() {
            // Arrange
            Result<int> success = 42;
            Result<int> failure = SomeError;

            // Act & Assert
            Assert.NotEqual(success, failure);
        }
    }
}