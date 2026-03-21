using Wiaoj.Results;
using Xunit;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Error)]
public sealed class ErrorTests {

    // ── Factory methods ───────────────────────────────────────────────────────

    [Fact]
    public void Failure_DefaultParameters_UsesDefaultCodeAndDescription() {
        Error error = Error.Failure();
        Assert.Equal("General.Failure", error.Code);
        Assert.Equal(ErrorType.Failure, error.Type);
        Assert.False(string.IsNullOrEmpty(error.Description));
    }

    [Fact]
    public void Failure_CustomParameters_StoresCorrectly() {
        Error error = Error.Failure("Order.Failed", "Order processing failed.");
        Assert.Equal("Order.Failed", error.Code);
        Assert.Equal("Order processing failed.", error.Description);
        Assert.Equal(ErrorType.Failure, error.Type);
    }

    [Fact]
    public void Unexpected_DefaultParameters_UsesDefaultCode() {
        Error error = Error.Unexpected();
        Assert.Equal("General.Unexpected", error.Code);
        Assert.Equal(ErrorType.Unexpected, error.Type);
    }

    [Fact]
    public void Validation_StoresCodeAndDescription() {
        Error error = Error.Validation("User.Email.Invalid", "Email format is invalid.");
        Assert.Equal("User.Email.Invalid", error.Code);
        Assert.Equal("Email format is invalid.", error.Description);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void NotFound_DefaultParameters_UsesDefaultCode() {
        Error error = Error.NotFound();
        Assert.Equal("Resource.NotFound", error.Code);
        Assert.Equal(ErrorType.NotFound, error.Type);
    }

    [Fact]
    public void NotFound_WithResourceAndId_FormatsCodeAndDescription() {
        Error error = Error.NotFound("User", 42);
        Assert.Equal("User.NotFound", error.Code);
        Assert.Contains("User", error.Description);
        Assert.Contains("42", error.Description);
        Assert.Equal(ErrorType.NotFound, error.Type);
    }

    [Fact]
    public void NotFound_WithResourceAndGuid_FormatsCodeAndDescription() {
        Guid id = Guid.NewGuid();
        Error error = Error.NotFound("Order", id);
        Assert.Contains(id.ToString(), error.Description);
    }

    [Fact]
    public void Conflict_DefaultParameters_UsesDefaultCode() {
        Error error = Error.Conflict();
        Assert.Equal("Resource.Conflict", error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    [Fact]
    public void Unauthorized_DefaultParameters_UsesDefaultCode() {
        Error error = Error.Unauthorized();
        Assert.Equal("Auth.Unauthorized", error.Code);
        Assert.Equal(ErrorType.Unauthorized, error.Type);
    }

    [Fact]
    public void Forbidden_DefaultParameters_UsesDefaultCode() {
        Error error = Error.Forbidden();
        Assert.Equal("Auth.Forbidden", error.Code);
        Assert.Equal(ErrorType.Forbidden, error.Type);
    }

    // ── WithMetadata ──────────────────────────────────────────────────────────

    [Fact]
    public void WithMetadata_AddsKeyValuePair() {
        Error error = Error.Failure().WithMetadata("RequestId", "abc-123");
        Assert.NotNull(error.Metadata);
        Assert.True(error.Metadata.ContainsKey("RequestId"));
        Assert.Equal("abc-123", error.Metadata["RequestId"]);
    }

    [Fact]
    public void WithMetadata_DoesNotMutateOriginal() {
        Error original = Error.Failure();
        Error withMeta = original.WithMetadata("Key", "Value");
        Assert.Null(original.Metadata);
        Assert.NotNull(withMeta.Metadata);
    }

    [Fact]
    public void WithMetadata_ChainedCalls_AccumulatesAllKeys() {
        Error error = Error.Failure()
            .WithMetadata("Key1", "Val1")
            .WithMetadata("Key2", 42)
            .WithMetadata("Key3", true);

        Assert.Equal(3, error.Metadata!.Count);
        Assert.Equal("Val1", error.Metadata["Key1"]);
        Assert.Equal(42, error.Metadata["Key2"]);
        Assert.Equal(true, error.Metadata["Key3"]);
    }

    [Fact]
    public void WithMetadata_OverwritesExistingKey() {
        Error error = Error.Failure()
            .WithMetadata("Key", "first")
            .WithMetadata("Key", "second");

        Assert.Equal("second", error.Metadata!["Key"]);
    }

    [Fact]
    public void WithMetadata_PreservesCodeDescriptionType() {
        Error original = Error.Validation("V.Code", "V.Desc");
        Error withMeta = original.WithMetadata("K", "V");

        Assert.Equal(original.Code, withMeta.Code);
        Assert.Equal(original.Description, withMeta.Description);
        Assert.Equal(original.Type, withMeta.Type);
    }

    // ── Value semantics ───────────────────────────────────────────────────────

    [Fact]
    public void TwoErrors_SameValues_AreEqual() {
        Error a = Error.Failure("Code", "Desc");
        Error b = Error.Failure("Code", "Desc");
        Assert.Equal(a, b);
    }

    [Fact]
    public void TwoErrors_DifferentCodes_AreNotEqual() {
        Error a = Error.Failure("Code.A", "Desc");
        Error b = Error.Failure("Code.B", "Desc");
        Assert.NotEqual(a, b);
    }

    // ── Metadata is null by default ───────────────────────────────────────────

    [Fact]
    public void Metadata_ByDefault_IsNull() {
        Error error = Error.Failure();
        Assert.Null(error.Metadata);
    }

    // ── Error.None ────────────────────────────────────────────────────────────

    [Fact]
    public void None_HasNoneCode() {
        Assert.Equal("None", Error.None.Code);
    }

    [Fact]
    public void None_IsNotEqualToAnyRealError() {
        Assert.NotEqual(Error.None, Error.Failure());
        Assert.NotEqual(Error.None, Error.NotFound());
    }

    [Fact]
    public void None_TwoReferences_AreEqual() {
        Assert.Equal(Error.None, Error.None);
    }

    // ── Error.Multiple ────────────────────────────────────────────────────────

    [Fact]
    public void Multiple_WithErrors_CreatesFailedResult() {
        Result<Success> result = Error.Multiple([SomeError, AnotherError]);
        Assert.True(result.IsError);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Multiple_Generic_CreatesFailedResultOfThatType() {
        Result<int> result = Error.Multiple<int>([SomeError, AnotherError]);
        Assert.True(result.IsError);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Multiple_WithSingleError_CreatesFailedResult() {
        Result<Success> result = Error.Multiple([SomeError]);
        Assert.True(result.IsError);
        Assert.Equal(SomeError, result.FirstError);
    }

    [Fact]
    public void Multiple_WithEmptyList_Throws() {
        Assert.Throws<ArgumentException>(() => Error.Multiple([]));
    }

    [Fact]
    public void Multiple_Generic_WithEmptyList_Throws() {
        Assert.Throws<ArgumentException>(() => Error.Multiple<int>([]));
    }

    [Fact]
    public void Multiple_AcceptsIEnumerable_NotOnlyList() {
        IEnumerable<Error> errors = [SomeError, AnotherError];
        Result<Success> result = Error.Multiple(errors);
        Assert.Equal(2, result.Errors.Count);
    }

    // ── Error.Custom ──────────────────────────────────────────────────────────

    [Fact]
    public void Custom_StoresTypeCodeDescription() {
        ErrorType rateLimit = new("RateLimit");
        Error error = Error.Custom(rateLimit, "RateLimit.Exceeded", "Too many requests.");

        Assert.Equal(rateLimit, error.Type);
        Assert.Equal("RateLimit.Exceeded", error.Code);
        Assert.Equal("Too many requests.", error.Description);
    }

    [Fact]
    public void Custom_TypeIsRetainedOnWithMetadata() {
        ErrorType custom = new("Custom");
        Error error = Error.Custom(custom, "C.Code", "desc").WithMetadata("k", "v");
        Assert.Equal(custom, error.Type);
    }

    // ── ErrorType via factory methods ─────────────────────────────────────────

    [Fact]
    public void Failure_HasFailureType() {
        Assert.Equal(ErrorType.Failure, Error.Failure().Type);
    }

    [Fact]
    public void Validation_HasValidationType() {
        Assert.Equal(ErrorType.Validation, Error.Validation("c", "d").Type);
    }

    [Fact]
    public void NotFound_HasNotFoundType() {
        Assert.Equal(ErrorType.NotFound, Error.NotFound().Type);
    }

    [Fact]
    public void Conflict_HasConflictType() {
        Assert.Equal(ErrorType.Conflict, Error.Conflict().Type);
    }

    [Fact]
    public void Unauthorized_HasUnauthorizedType() {
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized().Type);
    }

    [Fact]
    public void Forbidden_HasForbiddenType() {
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden().Type);
    }

    [Fact]
    public void Unexpected_HasUnexpectedType() {
        Assert.Equal(ErrorType.Unexpected, Error.Unexpected().Type);
    }

    [Fact]
    public void ErrorType_BuiltIn_Failure_HasExpectedName() {
        Assert.Equal("Failure", ErrorType.Failure.Name);
    }

    [Fact]
    public void ErrorType_BuiltIn_Validation_HasExpectedName() {
        Assert.Equal("Validation", ErrorType.Validation.Name);
    }

    [Fact]
    public void ErrorType_BuiltIn_NotFound_HasExpectedName() {
        Assert.Equal("NotFound", ErrorType.NotFound.Name);
    }

    [Fact]
    public void ErrorType_BuiltIn_Conflict_HasExpectedName() {
        Assert.Equal("Conflict", ErrorType.Conflict.Name);
    }

    [Fact]
    public void ErrorType_BuiltIn_Unauthorized_HasExpectedName() {
        Assert.Equal("Unauthorized", ErrorType.Unauthorized.Name);
    }

    [Fact]
    public void ErrorType_BuiltIn_Forbidden_HasExpectedName() {
        Assert.Equal("Forbidden", ErrorType.Forbidden.Name);
    }

    [Fact]
    public void ErrorType_BuiltIn_Unexpected_HasExpectedName() {
        Assert.Equal("Unexpected", ErrorType.Unexpected.Name);
    }

    [Fact]
    public void ErrorType_Custom_StoresProvidedName() {
        ErrorType rateLimit = new("RateLimit");
        Assert.Equal("RateLimit", rateLimit.Name);
    }

    [Fact]
    public void ErrorType_Custom_WithNullName_Throws() {
        Assert.ThrowsAny<ArgumentException>(() => new ErrorType(null!));
    }

    [Fact]
    public void ErrorType_Custom_WithWhitespaceName_Throws() {
        Assert.ThrowsAny<ArgumentException>(() => new ErrorType("   "));
    }

    [Fact]
    public void ErrorType_TwoInstancesWithSameName_AreEqual() {
        ErrorType a = new("MyType");
        ErrorType b = new("MyType");
        Assert.Equal(a, b);
    }

    [Fact]
    public void ErrorType_TwoInstancesWithDifferentNames_AreNotEqual() {
        Assert.NotEqual(new ErrorType("A"), new ErrorType("B"));
    }

    [Fact]
    public void ErrorType_ToString_ReturnsName() {
        Assert.Equal("RateLimit", new ErrorType("RateLimit").ToString());
    }
}