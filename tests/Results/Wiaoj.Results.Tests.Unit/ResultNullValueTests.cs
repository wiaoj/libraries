namespace Wiaoj.Results.Tests.Unit;

/// <summary>
/// Direct coverage for the rule that a <see langword="null"/> value is a failure
/// (<c>Result.NullValue</c>, <see cref="ErrorType.Unexpected"/>), not a success carrying no value.
/// </summary>
[Trait("Category", Category.StateAndInvariants)]
public sealed class ResultNullValueTests {

    private const string NullValueCode = "Result.NullValue";

    public sealed class TheNullIsFailureRule {
        [Fact]
        public void ImplicitConversion_WhenValueIsNull_ProducesFailureWithNullValueError() {
            // Arrange & Act
            Result<string?> result = (string?)null;

            // Assert
            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            Error error = Assert.Single(result.Errors);
            Assert.Equal(NullValueCode, error.Code);
            Assert.Equal(ErrorType.Unexpected, error.Type);
            Assert.Equal(NullValueCode, result.FirstError.Code);
        }

        [Fact]
        public void Success_WhenValueIsNull_ProducesSameFailureAsImplicitConversion() {
            // Arrange & Act
            Result<string?> result = Result.Success<string?>(null);

            // Assert
            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            Error error = Assert.Single(result.Errors);
            Assert.Equal(NullValueCode, error.Code);
            Assert.Equal(ErrorType.Unexpected, error.Type);
        }

        [Fact]
        public void TryGetValue_WhenValueIsNull_ReturnsFalse() {
            // Arrange
            Result<string?> result = (string?)null;

            // Act
            bool got = result.TryGetValue(out string? value);

            // Assert
            Assert.False(got);
            Assert.Null(value);
        }
    }

    public sealed class TheValueTypesStaySuccessful {
        [Fact]
        public void ImplicitConversion_NonNullableValueTypeDefault_IsStillSuccess() {
            // Arrange & Act - 0 is a value, not an absence, and must not be treated as null.
            Result<int> result = 0;

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal(0, result.Value);
            Assert.True(result.TryGetValue(out int value));
            Assert.Equal(0, value);
        }
    }

    public sealed class ThroughCombinators {
        [Fact]
        public void Map_WhenMapperReturnsNull_ProducesFailureRatherThanSuccessWithNullValue() {
            // Arrange
            Result<int> source = 5;

            // Act - the success path maps into a null reference value.
            Result<string> mapped = source.Map(_ => (string?)null);

            // Assert
            Assert.True(mapped.IsFailure);
            Assert.False(mapped.IsSuccess);
            Error error = Assert.Single(mapped.Errors);
            Assert.Equal(NullValueCode, error.Code);
            Assert.Equal(ErrorType.Unexpected, error.Type);
        }
    }
}
