using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Wiaoj.Results.AspNetCore.Tests;

[Trait("Category", "HttpMapping")]
public sealed class ResultHttpMappingTests {

    [Collection("ErrorTypeHttpMapper")]
    public sealed class TheErrorTypeToStatusCodeMapping {
        [Theory]
        [InlineData("Validation", StatusCodes.Status400BadRequest)]
        [InlineData("Unauthorized", StatusCodes.Status401Unauthorized)]
        [InlineData("Forbidden", StatusCodes.Status403Forbidden)]
        [InlineData("NotFound", StatusCodes.Status404NotFound)]
        [InlineData("MethodNotAllowed", StatusCodes.Status405MethodNotAllowed)]
        [InlineData("Conflict", StatusCodes.Status409Conflict)]
        [InlineData("Gone", StatusCodes.Status410Gone)]
        [InlineData("PreconditionFailed", StatusCodes.Status412PreconditionFailed)]
        [InlineData("UnsupportedMediaType", StatusCodes.Status415UnsupportedMediaType)]
        [InlineData("UnprocessableEntity", StatusCodes.Status422UnprocessableEntity)]
        [InlineData("PreconditionRequired", StatusCodes.Status428PreconditionRequired)] // RFC 6585
        [InlineData("RateLimit", StatusCodes.Status429TooManyRequests)]
        [InlineData("Timeout", StatusCodes.Status408RequestTimeout)]
        [InlineData("Cancelled", 499)] // nginx "Client Closed Request"; not a StatusCodes constant
        [InlineData("NotImplemented", StatusCodes.Status501NotImplemented)]
        [InlineData("Unavailable", StatusCodes.Status503ServiceUnavailable)]
        [InlineData("Unexpected", StatusCodes.Status500InternalServerError)]
        [InlineData("Uninitialized", StatusCodes.Status500InternalServerError)]
        [InlineData("Failure", StatusCodes.Status400BadRequest)]
        public void ToStatusCode_BuiltInErrorTypes_ReturnsExpectedHttpStatus(string typeName, int expectedStatusCode) {
            // Arrange
            ErrorType errorType = new(typeName);

            // Act
            int actualStatusCode = errorType.ToStatusCode();

            // Assert
            Assert.Equal(expectedStatusCode, actualStatusCode);
        }

        [Fact]
        public void ToStatusCode_EveryBuiltInErrorType_HasAMapping() {
            // Arrange - before 0.4.0 a built-in with no row folded silently into 500; this guard
            // makes the next ErrorType added without a mapping a test failure instead.
            ErrorType[] builtIn = typeof(ErrorType)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(ErrorType))
                .Select(field => (ErrorType)field.GetValue(null)!)
                .ToArray();

            Assert.NotEmpty(builtIn);

            foreach(ErrorType type in builtIn) {
                // Act & Assert
                Assert.True(ErrorTypeHttpMapper.TryMap(type, out int statusCode),
                    $"ErrorType.{type.Name} has no HTTP status code mapping in {nameof(ErrorTypeHttpMapper)}.");
                Assert.InRange(statusCode, 100, 599);
            }
        }

        [Fact]
        public void ToStatusCode_CustomUnmappedErrorType_ThrowsByDefault() {
            // Arrange
            ErrorType customType = new("CustomDomainFailure");

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => customType.ToStatusCode());

            // Assert
            Assert.Contains("CustomDomainFailure", exception.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ErrorTypeHttpMapper.Register), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ToStatusCode_UninitializedErrorType_AnswersInternalServerError() {
            // Arrange - #226 turns the nameless default value into ErrorType.Uninitialized, so reaching a
            // response with an uninitialized result is a mapped server fault, not an unmapped type.
            ErrorType uninitialized = default;

            // Act
            int statusCode = uninitialized.ToStatusCode();

            // Assert
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        }

        [Fact]
        public void TryMap_UninitializedErrorType_ReturnsTheBuiltInRowWithoutThrowing() {
            // Act
            bool mapped = ErrorTypeHttpMapper.TryMap(default, out int statusCode);

            // Assert
            Assert.True(mapped);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        }

        [Fact]
        public void ToStatusCode_SentinelUninitializedError_AnswersFiveHundredLikeBefore() {
            // Arrange & Act - Error.Uninitialized used ErrorType.Unexpected (500) and now uses
            // ErrorType.Uninitialized; the answer a caller sees must not move with the repoint.
            int viaUnexpected = ErrorType.Unexpected.ToStatusCode();
            int viaSentinel = Error.Uninitialized.Type.ToStatusCode();

            // Assert
            Assert.Equal(StatusCodes.Status500InternalServerError, viaUnexpected);
            Assert.Equal(viaUnexpected, viaSentinel);
        }

        [Fact]
        public void ToProblemDetails_SentinelUninitializedError_KeepsTheFiveHundredShape() {
            // Act
            ProblemDetails problem = Error.Uninitialized.ToProblemDetails();

            // Assert
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
            Assert.Equal("https://httpstatuses.io/500", problem.Type);
            Assert.Equal("Result.Uninitialized", problem.Title);
        }

        [Fact]
        public void TryMap_UnmappedErrorType_ReturnsFalseWithoutThrowing() {
            // Act
            bool mapped = ErrorTypeHttpMapper.TryMap(new("UnmappedProbe"), out int statusCode);

            // Assert
            Assert.False(mapped);
            Assert.Equal(0, statusCode);
        }

        [Fact]
        public void ToStatusCode_WithExplicitFallback_AnswersInsteadOfThrowing() {
            // Arrange
            ErrorType customType = new("FallbackProbe");

            // Act
            int statusCode = customType.ToStatusCode(StatusCodes.Status418ImATeapot);

            // Assert
            Assert.Equal(StatusCodes.Status418ImATeapot, statusCode);
        }
    }

    [Collection("ErrorTypeHttpMapper")]
    public sealed class TheProducerSuppliedMapping {

        [Fact]
        public void Register_CustomErrorType_UsedByToStatusCode() {
            // Arrange
            ErrorType locked = new("Locked");

            // Act
            ErrorTypeHttpMapper.Register(locked, StatusCodes.Status423Locked);
            int statusCode = locked.ToStatusCode();

            // Assert
            Assert.Equal(StatusCodes.Status423Locked, statusCode);

            // Cleanup
            Assert.True(ErrorTypeHttpMapper.Unregister(locked));
            Assert.Throws<InvalidOperationException>(() => locked.ToStatusCode());
        }

        [Fact]
        public void Register_BuiltInErrorType_OverridesTheDefaultMapping() {
            // Arrange
            ErrorType timeout = ErrorType.Timeout;

            // Act
            ErrorTypeHttpMapper.Register(timeout, StatusCodes.Status504GatewayTimeout);

            // Assert
            Assert.Equal(StatusCodes.Status504GatewayTimeout, timeout.ToStatusCode());

            // Cleanup - the built-in table must be reachable again
            ErrorTypeHttpMapper.Unregister(timeout);
            Assert.Equal(StatusCodes.Status408RequestTimeout, timeout.ToStatusCode());
        }

        [Fact]
        public void Unregister_WithoutRegistration_ReturnsFalse() {
            Assert.False(ErrorTypeHttpMapper.Unregister(new("NeverRegistered")));
        }

        [Fact]
        public void Unregister_UninitializedErrorType_ReturnsFalse() {
            // Assert - the name is never null any more, so this is an ordinary "nothing registered here"
            // answer rather than a null short-circuit.
            Assert.False(ErrorTypeHttpMapper.Unregister(default));
            Assert.False(ErrorTypeHttpMapper.Unregister(ErrorType.Uninitialized));
        }

        [Theory]
        [InlineData(99)]
        [InlineData(600)]
        public void Register_StatusCodeOutsideHttpStatusRange_IsRejected(int statusCode) {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => ErrorTypeHttpMapper.Register(new("RejectedRange"), statusCode));

            Assert.Equal("statusCode", exception.ParamName);
        }

        [Fact]
        public void Register_UninitializedErrorType_IsRejected() {
            Assert.ThrowsAny<ArgumentException>(
                () => ErrorTypeHttpMapper.Register(default, StatusCodes.Status409Conflict));
        }

        [Fact]
        public void Register_UninitializedErrorType_MessageNamesTheTypeAndSaysWhy() {
            // Act - the placeholder is gone: the type has a real name now, so the message can use it
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => ErrorTypeHttpMapper.Register(ErrorType.Uninitialized, StatusCodes.Status409Conflict));

            // Assert - a producer must not be able to re-key the uninitialized state to a client error
            Assert.Equal("errorType", exception.ParamName);
            Assert.Contains(nameof(ErrorType.Uninitialized), exception.Message, StringComparison.Ordinal);
            Assert.Equal(StatusCodes.Status500InternalServerError, default(ErrorType).ToStatusCode());
        }

        [Fact]
        public void ToStatusCode_WithLenientPolicy_KeepsTheLegacyFallThrough() {
            // Arrange
            ErrorType customType = new("LenientProbe");
            UnmappedErrorTypePolicy previous = ErrorTypeHttpMapper.OnUnmapped;

            try {
                ErrorTypeHttpMapper.OnUnmapped = UnmappedErrorTypePolicy.InternalServerError;

                // Act
                int statusCode = customType.ToStatusCode();

                // Assert
                Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
            }
            finally {
                ErrorTypeHttpMapper.OnUnmapped = previous;
            }
        }
    }

    public sealed class TheProblemDetailsCreation {
        [Fact]
        public void ToProblemDetails_SingleError_PopulatesStandardRfcFields() {
            // Arrange
            Error error = Error.NotFound("User.NotFound", "User with id '42' was not found.")
                .WithMetadata("UserId", 42);

            // Act
            ProblemDetails problem = error.ToProblemDetails();

            // Assert
            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal("User.NotFound", problem.Title);
            Assert.Equal("User with id '42' was not found.", problem.Detail);
            Assert.NotNull(problem.Extensions);
            Assert.True(problem.Extensions.ContainsKey("UserId"));
            Assert.Equal(42, problem.Extensions["UserId"]);
        }

        [Fact]
        public void ToProblemDetails_MultipleErrors_PopulatesErrorsExtensionList() {
            // Arrange
            List<Error> errors = [
                Error.Validation("Email.Invalid", "Email is not valid."),
                Error.Validation("Password.Weak", "Password is too weak.")
            ];
            Result<string> failureResult = errors;

            // Act
            ProblemDetails problem = failureResult.ToProblemDetails();

            // Assert
            Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
            Assert.Equal("Email.Invalid", problem.Title); // First error title
            Assert.NotNull(problem.Extensions);
            Assert.True(problem.Extensions.ContainsKey("errors"));
        }
    }

    public sealed class TheToHttpResultExtension {
        [Fact]
        public void ToHttpResult_SuccessWithValue_ReturnsOkResult() {
            // Arrange
            Result<string> result = Result.Success("Hello, World!");

            // Act
            Microsoft.AspNetCore.Http.IResult httpResult = result.ToHttpResult();

            // Assert
            Ok<string> okResult = Assert.IsType<Ok<string>>(httpResult);
            Assert.Equal("Hello, World!", okResult.Value);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        }

        [Fact]
        public void ToHttpResult_SuccessWithVoidType_ReturnsNoContentResult() {
            // Arrange
            Result<Success> result = Result.Success();

            // Act
            Microsoft.AspNetCore.Http.IResult httpResult = result.ToHttpResult();

            // Assert
            NoContent noContentResult = Assert.IsType<NoContent>(httpResult);
            Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        }

        [Fact]
        public void ToHttpResult_Failure_ReturnsProblemHttpResultWithMatchingStatusCode() {
            // Arrange
            Result<int> result = Result.Failure<int>(Error.Conflict("Order.AlreadyExists", "Order is duplicate."));

            // Act
            Microsoft.AspNetCore.Http.IResult httpResult = result.ToHttpResult();

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(httpResult);
            Assert.Equal(StatusCodes.Status409Conflict, problemResult.StatusCode);
            Assert.Equal("Order.AlreadyExists", problemResult.ProblemDetails.Title);
            Assert.Equal("Order is duplicate.", problemResult.ProblemDetails.Detail);
        }
    }

    public sealed class TheAdvancedHttpResponses {
        [Fact]
        public void ToCreatedHttpResult_Success_ReturnsCreatedWithLocationHeader() {
            // Arrange
            Result<int> result = Result.Success(42);

            // Act
            Microsoft.AspNetCore.Http.IResult httpResult = result.ToCreatedHttpResult("/api/users/42");

            // Assert
            Created<int> createdResult = Assert.IsType<Created<int>>(httpResult);
            Assert.Equal(42, createdResult.Value);
            Assert.Equal("/api/users/42", createdResult.Location);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        }

        [Fact]
        public void ToAcceptedHttpResult_Success_ReturnsAcceptedResult() {
            // Arrange
            Result<string> result = Result.Success("job-123");

            // Act
            Microsoft.AspNetCore.Http.IResult httpResult = result.ToAcceptedHttpResult("/api/jobs/job-123");

            // Assert
            Accepted<string> acceptedResult = Assert.IsType<Accepted<string>>(httpResult);
            Assert.Equal("job-123", acceptedResult.Value);
            Assert.Equal(StatusCodes.Status202Accepted, acceptedResult.StatusCode);
        }

        [Fact]
        public void ToHttpResult_WithResponseMapper_TransformsValueOnSuccess() {
            // Arrange
            Result<int> result = Result.Success(10);

            // Act
            Microsoft.AspNetCore.Http.IResult httpResult = result.ToHttpResult(v => new TotalDto(v * 2));

            // Assert
            Ok<TotalDto> okResult = Assert.IsType<Ok<TotalDto>>(httpResult);
            Assert.Equal(20, okResult.Value!.Total);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        }

        private sealed record TotalDto(int Total);
    }
}