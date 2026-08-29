using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wiaoj.Results.AspNetCore.Internal;
using Xunit;

namespace Wiaoj.Results.AspNetCore.Tests;

[Trait("Category", "EndpointFilter")]
public sealed class ResultEndpointFilterTests {

    public sealed class TheResultEndpointFilter {
        [Fact]
        public async Task InvokeAsync_WhenEndpointReturnsSuccessfulResult_ConvertsToOkResult() {
            // Arrange
            ResultEndpointFilter filter = new();
            EndpointFilterInvocationContext context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
            EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Result.Success(100));

            // Act
            object? result = await filter.InvokeAsync(context, next);

            // Assert
            Ok<int> okResult = Assert.IsType<Ok<int>>(result);
            Assert.Equal(100, okResult.Value);
        }

        [Fact]
        public async Task InvokeAsync_WhenEndpointReturnsFailedResult_ConvertsToProblemResult() {
            // Arrange
            ResultEndpointFilter filter = new();
            EndpointFilterInvocationContext context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
            EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Result.Failure<int>(Error.NotFound("User.NotFound", "User not found.")));

            // Act
            object? result = await filter.InvokeAsync(context, next);

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
            Assert.Equal("User.NotFound", problemResult.ProblemDetails.Title);
        }

        [Fact]
        public async Task InvokeAsync_WhenEndpointReturnsNonResultObject_PassesThroughUnmodified() {
            // Arrange
            ResultEndpointFilter filter = new();
            EndpointFilterInvocationContext context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
            EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("plain-string-response");

            // Act
            object? result = await filter.InvokeAsync(context, next);

            // Assert
            Assert.Equal("plain-string-response", result);
        }
    }
}