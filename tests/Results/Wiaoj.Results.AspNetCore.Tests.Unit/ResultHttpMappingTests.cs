using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Wiaoj.Results.AspNetCore.Tests;

[Trait("Category", "HttpMapping")]
public sealed class ResultHttpMappingTests {

    public sealed class TheErrorTypeToStatusCodeMapping {
        [Theory]
        [InlineData("Validation", StatusCodes.Status400BadRequest)]
        [InlineData("Unauthorized", StatusCodes.Status401Unauthorized)]
        [InlineData("Forbidden", StatusCodes.Status403Forbidden)]
        [InlineData("NotFound", StatusCodes.Status404NotFound)]
        [InlineData("Conflict", StatusCodes.Status409Conflict)]
        [InlineData("Gone", StatusCodes.Status410Gone)]
        [InlineData("UnprocessableEntity", StatusCodes.Status422UnprocessableEntity)]
        [InlineData("RateLimit", StatusCodes.Status429TooManyRequests)]
        [InlineData("Timeout", StatusCodes.Status504GatewayTimeout)]
        [InlineData("Unavailable", StatusCodes.Status503ServiceUnavailable)]
        [InlineData("Unexpected", StatusCodes.Status500InternalServerError)]
        [InlineData("Failure", StatusCodes.Status500InternalServerError)]
        public void ToStatusCode_BuiltInErrorTypes_ReturnsExpectedHttpStatus(string typeName, int expectedStatusCode) {
            // Arrange
            ErrorType errorType = new(typeName);

            // Act
            int actualStatusCode = errorType.ToStatusCode();

            // Assert
            Assert.Equal(expectedStatusCode, actualStatusCode);
        }

        [Fact]
        public void ToStatusCode_CustomUnknownErrorType_DefaultsTo500InternalServerError() {
            // Arrange
            ErrorType customType = new("CustomDomainFailure");

            // Act
            int statusCode = customType.ToStatusCode();

            // Assert
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
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
            Microsoft.AspNetCore.Http.IResult httpResult = result.ToHttpResult(v => new { Total = v * 2 });

            // Assert
            Ok<object> okResult = Assert.IsType<Ok<object>>(httpResult);
            Assert.NotNull(okResult.Value);
        }
    }
}