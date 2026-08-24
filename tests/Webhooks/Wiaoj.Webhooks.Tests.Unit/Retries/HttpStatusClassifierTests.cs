using Wiaoj.Webhooks.Retries;

namespace Wiaoj.Webhooks.Tests.Unit.Retries;

public sealed class HttpStatusClassifierTests {
    [Theory]
    [InlineData(null)] // Network failure / timeout
    [InlineData(408)]  // Request Timeout
    [InlineData(429)]  // Too Many Requests (Rate Limit)
    [InlineData(500)]  // Internal Server Error
    [InlineData(502)]  // Bad Gateway
    [InlineData(503)]  // Service Unavailable
    [InlineData(504)]  // Gateway Timeout
    [InlineData(599)]  // Network Connect Timeout
    public void IsTransient_ReturnsTrue_ForTransientErrors(int? statusCode) {
        Assert.True(HttpStatusClassifier.IsTransient(statusCode));
        Assert.False(HttpStatusClassifier.IsPermanentFailure(statusCode));
    }

    [Theory]
    [InlineData(200)] // OK
    [InlineData(201)] // Created
    [InlineData(204)] // No Content
    [InlineData(400)] // Bad Request
    [InlineData(401)] // Unauthorized
    [InlineData(403)] // Forbidden
    [InlineData(404)] // Not Found
    [InlineData(405)] // Method Not Allowed
    [InlineData(410)] // Gone
    [InlineData(413)] // Payload Too Large
    [InlineData(415)] // Unsupported Media Type
    [InlineData(422)] // Unprocessable Entity
    public void IsTransient_ReturnsFalse_ForPermanentOutcomes(int? statusCode) {
        Assert.False(HttpStatusClassifier.IsTransient(statusCode));
        Assert.True(HttpStatusClassifier.IsPermanentFailure(statusCode));
    }
}
