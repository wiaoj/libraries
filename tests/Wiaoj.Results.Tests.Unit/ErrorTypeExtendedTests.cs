namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Error)]
public sealed class ErrorTypeExtendedTests {

    // ── New built-in ErrorTypes ───────────────────────────────────────────────

    [Fact]
    public void ErrorType_RateLimit_HasExpectedName() {
        Assert.Equal("RateLimit", ErrorType.RateLimit.Name);
    }

    [Fact]
    public void ErrorType_Timeout_HasExpectedName() {
        Assert.Equal("Timeout", ErrorType.Timeout.Name);
    }

    [Fact]
    public void ErrorType_Unavailable_HasExpectedName() {
        Assert.Equal("Unavailable", ErrorType.Unavailable.Name);
    }

    [Fact]
    public void ErrorType_Gone_HasExpectedName() {
        Assert.Equal("Gone", ErrorType.Gone.Name);
    }

    [Fact]
    public void ErrorType_UnprocessableEntity_HasExpectedName() {
        Assert.Equal("UnprocessableEntity", ErrorType.UnprocessableEntity.Name);
    }

    [Fact]
    public void ErrorType_AllBuiltIns_AreDistinct() {
        ErrorType[] all = [
            ErrorType.Failure,
            ErrorType.Validation,
            ErrorType.NotFound,
            ErrorType.Conflict,
            ErrorType.Unauthorized,
            ErrorType.Forbidden,
            ErrorType.Unexpected,
            ErrorType.RateLimit,
            ErrorType.Timeout,
            ErrorType.Unavailable,
            ErrorType.Gone,
            ErrorType.UnprocessableEntity,
        ];
        Assert.Equal(all.Length, all.Distinct().Count());
    }

    // ── New Error factory methods ─────────────────────────────────────────────

    [Fact]
    public void RateLimitExceeded_HasRateLimitType() {
        Assert.Equal(ErrorType.RateLimit, Error.RateLimitExceeded().Type);
    }

    [Fact]
    public void RateLimitExceeded_DefaultCode() {
        Assert.Equal("RateLimit.Exceeded", Error.RateLimitExceeded().Code);
    }

    [Fact]
    public void Timeout_HasTimeoutType() {
        Assert.Equal(ErrorType.Timeout, Error.Timeout().Type);
    }

    [Fact]
    public void Timeout_DefaultCode() {
        Assert.Equal("Request.Timeout", Error.Timeout().Code);
    }

    [Fact]
    public void ServiceUnavailable_HasUnavailableType() {
        Assert.Equal(ErrorType.Unavailable, Error.ServiceUnavailable().Type);
    }

    [Fact]
    public void Gone_HasGoneType() {
        Assert.Equal(ErrorType.Gone, Error.Gone().Type);
    }

    [Fact]
    public void UnprocessableEntity_HasUnprocessableEntityType() {
        Error error = Error.UnprocessableEntity("Rule.Violated", "desc");
        Assert.Equal(ErrorType.UnprocessableEntity, error.Type);
    }

    // ── Error.FromException ───────────────────────────────────────────────────

    [Fact]
    public void FromException_TimeoutException_MapsToTimeout() {
        Error error = Error.FromException(new TimeoutException("slow"));
        Assert.Equal(ErrorType.Timeout, error.Type);
        Assert.Contains("slow", error.Description);
    }

    [Fact]
    public void FromException_UnauthorizedAccessException_MapsToUnauthorized() {
        Error error = Error.FromException(new UnauthorizedAccessException("denied"));
        Assert.Equal(ErrorType.Unauthorized, error.Type);
    }

    [Fact]
    public void FromException_ArgumentException_MapsToValidation() {
        Error error = Error.FromException(new ArgumentException("bad arg"));
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void FromException_ArgumentNullException_MapsToValidation() {
        // ArgumentNullException derives from ArgumentException
        Error error = Error.FromException(new ArgumentNullException("param"));
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void FromException_UnknownException_MapsToUnexpected() {
        Error error = Error.FromException(new InvalidOperationException("ioe"));
        Assert.Equal(ErrorType.Unexpected, error.Type);
    }

    [Fact]
    public void FromException_SetsDescriptionFromMessage() {
        string msg = "something went wrong";
        Error error = Error.FromException(new Exception(msg));
        Assert.Equal(msg, error.Description);
    }

    [Fact]
    public void FromException_CodeContainsExceptionTypeName() {
        Error error = Error.FromException(new InvalidOperationException());
        Assert.Contains("InvalidOperationException", error.Code);
    }

    [Fact]
    public void FromException_WithIncludeType_AttachesMetadata() {
        Error error = Error.FromException(new Exception("x"), includeType: true);
        Assert.NotNull(error.Metadata);
        Assert.True(error.Metadata!.ContainsKey("ExceptionType"));
        Assert.Contains("Exception", error.Metadata["ExceptionType"].ToString());
    }

    [Fact]
    public void FromException_WithIncludeTypeFalse_NoMetadata() {
        Error error = Error.FromException(new Exception("x"), includeType: false);
        Assert.Null(error.Metadata);
    }

    [Fact]
    public void FromException_NullException_Throws() {
        Assert.Throws<ArgumentNullException>(() => Error.FromException(null!));
    }

    // ── Gone vs NotFound distinction ──────────────────────────────────────────

    [Fact]
    public void Gone_AndNotFound_HaveDifferentTypes() {
        Assert.NotEqual(ErrorType.Gone, ErrorType.NotFound);
    }

    [Fact]
    public void Gone_DefaultCode_DifferentFromNotFound() {
        Assert.NotEqual(Error.Gone().Code, Error.NotFound().Code);
    }

    // ── UnprocessableEntity vs Validation distinction ─────────────────────────

    [Fact]
    public void UnprocessableEntity_AndValidation_HaveDifferentTypes() {
        Assert.NotEqual(ErrorType.UnprocessableEntity, ErrorType.Validation);
    }
}