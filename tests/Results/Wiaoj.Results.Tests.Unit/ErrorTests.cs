namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Error)]
public sealed class ErrorTests {

    public sealed class TheBuiltInFactoryMethods {
        [Fact]
        public void Failure_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.Failure();

            // Assert
            Assert.Equal("General.Failure", error.Code);
            Assert.Equal(ErrorType.Failure, error.Type);
            Assert.False(string.IsNullOrEmpty(error.Description));
            Assert.Null(error.Metadata);
        }

        [Fact]
        public void Failure_CustomParameters_StoresValuesCorrectly() {
            // Arrange & Act
            Error error = Error.Failure("Order.Failed", "Order processing failed.");

            // Assert
            Assert.Equal("Order.Failed", error.Code);
            Assert.Equal("Order processing failed.", error.Description);
            Assert.Equal(ErrorType.Failure, error.Type);
        }

        [Fact]
        public void Unexpected_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.Unexpected();

            // Assert
            Assert.Equal("General.Unexpected", error.Code);
            Assert.Equal(ErrorType.Unexpected, error.Type);
        }

        [Fact]
        public void Validation_StoresCodeAndDescription() {
            // Arrange & Act
            Error error = Error.Validation("User.Email.Invalid", "Email format is invalid.");

            // Assert
            Assert.Equal("User.Email.Invalid", error.Code);
            Assert.Equal("Email format is invalid.", error.Description);
            Assert.Equal(ErrorType.Validation, error.Type);
        }

        [Fact]
        public void NotFound_WithResourceAndId_FormatsDescriptionCorrectly() {
            // Arrange & Act
            Error error = Error.NotFound("User", 42);

            // Assert
            Assert.Equal("User.NotFound", error.Code);
            Assert.Contains("User", error.Description);
            Assert.Contains("42", error.Description);
            Assert.Equal(ErrorType.NotFound, error.Type);
        }

        [Fact]
        public void Conflict_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.Conflict();

            // Assert
            Assert.Equal("Resource.Conflict", error.Code);
            Assert.Equal(ErrorType.Conflict, error.Type);
        }

        [Fact]
        public void Unauthorized_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.Unauthorized();

            // Assert
            Assert.Equal("Auth.Unauthorized", error.Code);
            Assert.Equal(ErrorType.Unauthorized, error.Type);
        }

        [Fact]
        public void Forbidden_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.Forbidden();

            // Assert
            Assert.Equal("Auth.Forbidden", error.Code);
            Assert.Equal(ErrorType.Forbidden, error.Type);
        }

        [Fact]
        public void RateLimitExceeded_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.RateLimitExceeded();

            // Assert
            Assert.Equal("RateLimit.Exceeded", error.Code);
            Assert.Equal(ErrorType.RateLimit, error.Type);
        }

        [Fact]
        public void Timeout_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.Timeout();

            // Assert
            Assert.Equal("Request.Timeout", error.Code);
            Assert.Equal(ErrorType.Timeout, error.Type);
        }

        [Fact]
        public void ServiceUnavailable_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.ServiceUnavailable();

            // Assert
            Assert.Equal("Service.Unavailable", error.Code);
            Assert.Equal(ErrorType.Unavailable, error.Type);
        }

        [Fact]
        public void Gone_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.Gone();

            // Assert
            Assert.Equal("Resource.Gone", error.Code);
            Assert.Equal(ErrorType.Gone, error.Type);
        }

        [Fact]
        public void UnprocessableEntity_SetsValuesCorrectly() {
            // Arrange & Act
            Error error = Error.UnprocessableEntity("Rule.Violated", "Business rule violation.");

            // Assert
            Assert.Equal("Rule.Violated", error.Code);
            Assert.Equal("Business rule violation.", error.Description);
            Assert.Equal(ErrorType.UnprocessableEntity, error.Type);
        }
    }

    public sealed class TheFromExceptionMethod {
        [Fact]
        public void FromException_TimeoutException_MapsToTimeoutErrorType() {
            // Arrange
            TimeoutException exception = new("Operation timed out");

            // Act
            Error error = Error.FromException(exception);

            // Assert
            Assert.Equal(ErrorType.Timeout, error.Type);
            Assert.Equal("Exception.Timeout", error.Code);
            Assert.Equal(exception.Message, error.Description);
        }

        [Fact]
        public void FromException_UnauthorizedAccessException_MapsToUnauthorizedErrorType() {
            // Arrange
            UnauthorizedAccessException exception = new("Access is denied");

            // Act
            Error error = Error.FromException(exception);

            // Assert
            Assert.Equal(ErrorType.Unauthorized, error.Type);
            Assert.Equal("Exception.Unauthorized", error.Code);
            Assert.Equal(exception.Message, error.Description);
        }

        [Fact]
        public void FromException_ArgumentException_MapsToValidationErrorType() {
            // Arrange
            ArgumentException exception = new("Invalid argument");

            // Act
            Error error = Error.FromException(exception);

            // Assert
            Assert.Equal(ErrorType.Validation, error.Type);
            Assert.Equal("Exception.Argument", error.Code);
        }

        [Fact]
        public void FromException_UnknownException_MapsToUnexpectedErrorType() {
            // Arrange
            InvalidOperationException exception = new("Invalid operation");

            // Act
            Error error = Error.FromException(exception);

            // Assert
            Assert.Equal(ErrorType.Unexpected, error.Type);
            Assert.Contains(nameof(InvalidOperationException), error.Code);
        }

        [Fact]
        public void FromException_WithIncludeTypeTrue_AttachesExceptionTypeMetadata() {
            // Arrange
            Exception exception = new("General error");

            // Act
            Error error = Error.FromException(exception, includeType: true);

            // Assert
            Assert.NotNull(error.Metadata);
            Assert.True(error.Metadata.ContainsKey("ExceptionType"));
            Assert.Equal(typeof(Exception).FullName, error.Metadata["ExceptionType"]);
        }

        [Fact]
        public void FromException_WithNull_ThrowsArgumentNullException() {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => Error.FromException(null!));
        }

        [Fact]
        public void FromException_KeepsTheOriginatingExceptionAsCause() {
            // Arrange
            InvalidOperationException exception = new("Invalid operation");

            // Act
            Error error = Error.FromException(exception);

            // Assert
            Assert.Same(exception, error.Cause);
        }

        [Fact]
        public void FromException_CauseRetainsTheStackTrace() {
            // Arrange
            Exception exception = Capture(new InvalidOperationException("with a stack"));

            // Act
            Error error = Error.FromException(exception);

            // Assert - the point of keeping the exception: the frames survive
            Assert.NotNull(error.Cause?.StackTrace);
            Assert.Contains(nameof(Capture), error.Cause!.StackTrace!, StringComparison.Ordinal);
        }

        [Fact]
        public void FromException_OperationCanceledException_MapsToCancelledNotUnexpected() {
            // Arrange - a cancellation is not a server fault
            using CancellationTokenSource source = new();
            OperationCanceledException exception = new("cancelled", source.Token);

            // Act
            Error error = Error.FromException(exception);

            // Assert
            Assert.Equal(ErrorType.Cancelled, error.Type);
            Assert.Equal("Exception.Cancelled", error.Code);
            Assert.Same(exception, error.Cause);
        }

        [Fact]
        public void FromException_TaskCanceledException_MapsToCancelledNotUnexpected() {
            // Arrange - what HttpClient throws when the request is abandoned
            TaskCanceledException exception = new("the request was canceled", innerException: null);

            // Act
            Error error = Error.FromException(exception);

            // Assert
            Assert.Equal(ErrorType.Cancelled, error.Type);
        }

        [Fact]
        public void FromException_WithIncludeTypeTrue_KeepsBothMetadataAndCause() {
            // Arrange
            InvalidOperationException exception = new("Invalid operation");

            // Act
            Error error = Error.FromException(exception, includeType: true);

            // Assert
            Assert.NotNull(error.Metadata);
            Assert.Equal(typeof(InvalidOperationException).FullName, error.Metadata["ExceptionType"]);
            Assert.Same(exception, error.Cause);
        }

        [Fact]
        public void WithCause_Null_IsRejected() {
            Assert.Throws<ArgumentNullException>(() => Error.Failure().WithCause(null!));
        }

        private static Exception Capture(Exception exception) {
            try {
                throw exception;
            }
            catch(Exception caught) {
                return caught;
            }
        }
    }

    public sealed class TheWithMetadataMethod {
        [Fact]
        public void WithMetadata_PreservesTheCause() {
            // Arrange
            InvalidOperationException exception = new("Invalid operation");
            Error error = Error.FromException(exception);

            // Act
            Error updated = error.WithMetadata("TraceId", "tx-999");

            // Assert - fluent decoration must not drop the stack trace
            Assert.Same(exception, updated.Cause);
            Assert.Equal("tx-999", updated.Metadata!["TraceId"]);
        }

        [Fact]
        public void WithMetadata_AddsNewEntryWithoutMutatingOriginal() {
            // Arrange
            Error original = Error.Failure();

            // Act
            Error updated = original.WithMetadata("TraceId", "tx-999");

            // Assert
            Assert.Null(original.Metadata);
            Assert.NotNull(updated.Metadata);
            Assert.Equal("tx-999", updated.Metadata["TraceId"]);
        }

        [Fact]
        public void WithMetadata_ChainedCalls_AccumulatesEntries() {
            // Arrange
            Error error = Error.Failure()
                .WithMetadata("Key1", "Value1")
                .WithMetadata("Key2", 42);

            // Act & Assert
            Assert.NotNull(error.Metadata);
            Assert.Equal(2, error.Metadata.Count);
            Assert.Equal("Value1", error.Metadata["Key1"]);
            Assert.Equal(42, error.Metadata["Key2"]);
        }

        [Fact]
        public void WithMetadata_WhenKeyIsNullOrWhiteSpace_ThrowsArgumentException() {
            // Arrange
            Error error = Error.Failure();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => error.WithMetadata(null!, "value"));
            Assert.Throws<ArgumentException>(() => error.WithMetadata("", "value"));
            Assert.Throws<ArgumentException>(() => error.WithMetadata("   ", "value"));
        }

        [Fact]
        public void WithMetadata_WhenValueIsNull_StoresNullSuccessfully() {
            // Arrange
            Error error = Error.Failure().WithMetadata("OptionalData", null);

            // Act & Assert
            Assert.NotNull(error.Metadata);
            Assert.True(error.Metadata.ContainsKey("OptionalData"));
            Assert.Null(error.Metadata["OptionalData"]);
        }

        [Fact]
        public void WithMetadata_WhenOverwritingExistingKey_ReplacesValueAndPreservesCount() {
            // Arrange
            Error initial = Error.Failure().WithMetadata("Attempt", 1);

            // Act
            Error updated = initial.WithMetadata("Attempt", 2);

            // Assert
            Assert.Single(updated.Metadata!);
            Assert.Equal(2, updated.Metadata!["Attempt"]);
            Assert.Equal(1, initial.Metadata!["Attempt"]);
        }
    }

    public sealed class TheCustomErrorTypeAndNoneSentinel {
        [Fact]
        public void Custom_CreatesErrorWithSpecifiedType() {
            // Arrange
            ErrorType rateLimit = new("CustomLimit");

            // Act
            Error error = Error.Custom(rateLimit, "Rate.Exceeded", "Limit reached");

            // Assert
            Assert.Equal(rateLimit, error.Type);
            Assert.Equal("Rate.Exceeded", error.Code);
        }

        [Fact]
        public void None_RepresentsSentinelWithErrorType() {
            // Arrange & Act & Assert
            Assert.Equal("None", Error.None.Code);
            Assert.NotEqual(Error.None, Error.Failure());
        }
    }

    public sealed class TheEqualityAndComparison {
        [Fact]
        public void Equals_WhenErrorsDifferOnlyByCause_ReturnsTrue() {
            // Arrange - the cause is diagnostic payload, not identity
            Error first = Error.FromException(ThrowAway(new InvalidOperationException("one")));
            Error second = Error.FromException(ThrowAway(new InvalidOperationException("one")));

            // Act & Assert
            Assert.NotSame(first.Cause, second.Cause);
            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void ToString_PrintsTheShapeButNeverTheStackTrace() {
            // Arrange
            Error error = Error.FromException(ThrowAway(new InvalidOperationException("boom")));

            // Act
            string printed = error.ToString();

            // Assert
            Assert.Contains("Code = Exception.InvalidOperationException", printed, StringComparison.Ordinal);
            Assert.DoesNotContain("Cause", printed, StringComparison.Ordinal);
            Assert.DoesNotContain("at Wiaoj", printed, StringComparison.Ordinal);
        }

        private static Exception ThrowAway(Exception exception) {
            try {
                throw exception;
            }
            catch(Exception caught) {
                return caught;
            }
        }

        [Fact]
        public void Equals_WhenBothErrorsHaveSameValuesWithoutMetadata_ReturnsTrue() {
            // Arrange
            Error first = Error.Failure("Code", "Desc");
            Error second = Error.Failure("Code", "Desc");

            // Act & Assert
            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Equals_WhenBothErrorsHaveDifferentCodes_ReturnsFalse() {
            // Arrange
            Error first = Error.Failure("Code.A", "Desc");
            Error second = Error.Failure("Code.B", "Desc");

            // Act & Assert
            Assert.NotEqual(first, second);
            Assert.False(first == second);
        }

        [Fact]
        public void Equals_WhenBothErrorsHaveIdenticalMetadataContent_ReturnsTrue() {
            // Arrange
            Error first = Error.Failure("User.NotFound", "User not found.")
                .WithMetadata("UserId", 42)
                .WithMetadata("TenantId", "tenant-alpha");

            Error second = Error.Failure("User.NotFound", "User not found.")
                .WithMetadata("UserId", 42)
                .WithMetadata("TenantId", "tenant-alpha");

            // Act & Assert
            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Equals_WhenBothErrorsHaveDifferentMetadataValues_ReturnsFalse() {
            // Arrange
            Error first = Error.Failure("User.NotFound", "User not found.")
                .WithMetadata("UserId", 42);

            Error second = Error.Failure("User.NotFound", "User not found.")
                .WithMetadata("UserId", 99);

            // Act & Assert
            Assert.NotEqual(first, second);
            Assert.False(first == second);
        }

        [Fact]
        public void Equals_WhenOneHasMetadataAndOtherIsNull_ReturnsFalse() {
            // Arrange
            Error first = Error.Failure("User.NotFound", "User not found.")
                .WithMetadata("UserId", 42);

            Error second = Error.Failure("User.NotFound", "User not found.");

            // Act & Assert
            Assert.NotEqual(first, second);
            Assert.False(first == second);
        }

        [Fact]
        public void EqualsAndGetHashCode_WhenMetadataKeysAddedInDifferentOrder_AreEqualAndProduceSameHashCode() {
            // Arrange
            Error first = Error.Failure("Order.Failed", "Description")
                .WithMetadata("KeyA", "ValA")
                .WithMetadata("KeyB", 100);

            Error second = Error.Failure("Order.Failed", "Description")
                .WithMetadata("KeyB", 100)
                .WithMetadata("KeyA", "ValA");

            // Act & Assert
            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }
    }
}