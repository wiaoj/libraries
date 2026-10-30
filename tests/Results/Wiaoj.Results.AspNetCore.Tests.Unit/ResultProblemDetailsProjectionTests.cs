using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Wiaoj.Results.AspNetCore.Tests;

/// <summary>
/// Guards the RFC 9457 projection rules in <see cref="ProblemDetailsProjection"/> (#210): who owns <c>type</c>,
/// what <c>title</c> is allowed to be, and that the machine-readable code reaches the body in a member a consumer
/// can act on instead of being fished out of a human field.
/// </summary>
/// <remarks>
/// Every class here passes its own <see cref="ProblemDetailsProjection"/> except the last one, which is the only
/// test allowed to touch <see cref="ProblemDetailsProjection.Default"/>. The classes that read the default —
/// <c>TheProblemDetailsCreation</c>, <c>TheToHttpResultExtension</c> and the endpoint filter tests — are tagged into
/// the same xUnit collection so nothing mutates it while they are asserting.
/// </remarks>
[Trait("Category", "ProblemDetailsProjection")]
public sealed class ResultProblemDetailsProjectionTests {

    private const string ProducerNamespace = "https://errors.example.dev";

    private static readonly ProblemDetailsProjection Bare = new();

    private static readonly ProblemDetailsProjection Named = new() { TypeNamespace = ProducerNamespace };

    [Collection("ProblemDetailsProjection")]
    public sealed class TheTypeMember {

        [Fact]
        public void Type_WithNoNamespaceConfigured_IsAboutBlank() {
            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.")
                .ToProblemDetails(null, Bare);

            // Assert - RFC 9457 §3.1.1 names about:blank for "no type URI"; a third-party status site is not a
            // producer's answer and leaks the producer's taxonomy into someone else's analytics.
            Assert.Equal("about:blank", problem.Type);
            Assert.DoesNotContain("httpstatuses.io", problem.Type, StringComparison.Ordinal);
        }

        [Fact]
        public void Type_WithNamespaceConfigured_EndsWithTheErrorCode() {
            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.")
                .ToProblemDetails(null, Named);

            // Assert - distinct per problem class, which is the whole point of `type`
            Assert.Equal($"{ProducerNamespace}/User.NotFound", problem.Type);
        }

        [Fact]
        public void Type_TwoErrorsOfTheSameStatus_ButDifferentCodes_GetDifferentTypes() {
            // Arrange - the defect #210 describes: two different NotFound answers shared one `type`, because it
            // was keyed on the status code alone.
            // Act
            string userMissing = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, Named).Type;
            string orderMissing = Error.NotFound("Order.NotFound", "Missing.").ToProblemDetails(null, Named).Type;

            // Assert
            Assert.NotEqual(userMissing, orderMissing);
        }

        [Fact]
        public void Type_NamespaceWithTrailingSlash_DoesNotDoubleTheSlash() {
            // Arrange
            ProblemDetailsProjection projection = new() { TypeNamespace = $"{ProducerNamespace}/" };

            // Act
            string type = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, projection).Type;

            // Assert
            Assert.Equal($"{ProducerNamespace}/User.NotFound", type);
        }

        [Fact]
        public void Type_WhitespaceNamespace_IsTreatedAsUnconfigured() {
            // Arrange
            ProblemDetailsProjection projection = new() { TypeNamespace = "   " };

            // Act
            string type = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, projection).Type;

            // Assert
            Assert.Equal("about:blank", type);
        }

        [Fact]
        public void Type_LegacyStyle_ReproducesThePre9457Uri() {
            // Arrange - the escape hatch for a consumer that cannot migrate in the same release.
            ProblemDetailsProjection legacy = new() { TypeStyle = ProblemTypeStyle.HttpStatusIo };

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, legacy);

            // Assert
            Assert.Equal("https://httpstatuses.io/404", problem.Type);
        }

        [Fact]
        public void Type_LegacyStyle_StillCarriesTheCodeExtension() {
            // Arrange - pinning the old `type` must not cost the consumer the machine key; that pair of fields is
            // what made the old shape usable at all.
            ProblemDetailsProjection legacy = new() { TypeStyle = ProblemTypeStyle.HttpStatusIo };

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, legacy);

            // Assert
            Assert.Equal("User.NotFound", problem.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey]);
        }

        [Fact]
        public void Projection_WithNoInstanceArgument_IsRejected() {
            // Assert - the explicit overload is a system boundary: a null projection is a caller bug, not a
            // request for the default.
            Assert.Throws<ArgumentNullException>(
                () => Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, null!));
        }
    }

    [Collection("ProblemDetailsProjection")]
    public sealed class TheTitleMember {

        [Fact]
        public void Title_IsTheErrorTypeName_NotTheErrorCode() {
            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, Bare);

            // Assert - §3.2 wants a short human summary that repeats per problem type. The code is no longer
            // allowed to squat here: it changes per instance.
            Assert.Equal(nameof(ErrorType.NotFound), problem.Title);
            Assert.DoesNotContain("User.NotFound", problem.Title, StringComparison.Ordinal);
        }

        [Fact]
        public void Title_IsTheSameForEveryErrorOfTheSameType() {
            // Act
            ProblemDetails first = Error.Validation("Email.Invalid", "Bad email.").ToProblemDetails(null, Bare);
            ProblemDetails second = Error.Validation("Password.Weak", "Weak password.").ToProblemDetails(null, Bare);

            // Assert
            Assert.Equal(first.Title, second.Title);
            Assert.NotEqual(
                first.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey],
                second.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey]);
        }

        [Fact]
        public void Title_ConfiguredOverride_Wins() {
            // Arrange
            ProblemDetailsProjection projection = new() {
                Titles = new Dictionary<string, string> { [nameof(ErrorType.NotFound)] = "Resource Not Found" }
            };

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, projection);

            // Assert
            Assert.Equal("Resource Not Found", problem.Title);
        }

        [Fact]
        public void Title_BlankOverride_FallsBackToTheTypeName() {
            // Arrange - a configured blank must not put an empty title on the wire.
            ProblemDetailsProjection projection = new() {
                Titles = new Dictionary<string, string> { [nameof(ErrorType.NotFound)] = "  " }
            };

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, projection);

            // Assert
            Assert.Equal(nameof(ErrorType.NotFound), problem.Title);
        }

        [Fact]
        public void Title_OfAnUninitializedType_IsTheUninitializedName() {
            // Act - #226 named the default ErrorType, so the title never comes out empty.
            ProblemDetails problem = Error.Uninitialized.ToProblemDetails(null, Bare);

            // Assert
            Assert.Equal(nameof(ErrorType.Uninitialized), problem.Title);
        }
    }

    [Collection("ProblemDetailsProjection")]
    public sealed class TheCodeExtension {

        [Fact]
        public void Code_IsPresentEvenWithoutMetadata() {
            // Act
            ProblemDetails problem = Error.Conflict("Order.AlreadyExists", "Duplicate.").ToProblemDetails(null, Bare);

            // Assert - this is the member a deployed client reads instead of `title`.
            Assert.Equal(
                "Order.AlreadyExists",
                problem.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey]);
        }

        [Fact]
        public void Code_WinsOverAMetadataEntryOfTheSameName() {
            // Arrange - a producer that puts its own "code" in metadata would otherwise make the member mean two
            // things, which is the consumer trap that `violations` was named for.
            Error error = Error.Validation("Form.Invalid", "Bad input.").WithMetadata("code", "SOMETHING_ELSE");

            // Act
            ProblemDetails problem = error.ToProblemDetails(null, Bare);

            // Assert
            Assert.Equal("Form.Invalid", problem.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey]);
        }

        [Fact]
        public void Code_OnEveryProjectionPath_MatchesTheProjectedError() {
            // Arrange
            List<Error> errors = [
                Error.Validation("Email.Invalid", "Bad email."),
                Error.Validation("Password.Weak", "Weak password.")
            ];
            Result<string> failureResult = errors;

            // Act - the IResult path goes through the Error path; both must agree.
            string? fromError = failureResult.FirstError.ToProblemDetails(null, Bare)
                .Extensions[ResultProblemDetailsExtensions.CodeExtensionKey] as string;
            string? fromResult = failureResult.ToProblemDetails(null, Bare)
                .Extensions[ResultProblemDetailsExtensions.CodeExtensionKey] as string;

            // Assert
            Assert.Equal("Email.Invalid", fromError);
            Assert.Equal(fromError, fromResult);
        }

        [Theory]
        [InlineData(nameof(ErrorType.NotFound))]
        [InlineData(nameof(ErrorType.Validation))]
        [InlineData(nameof(ErrorType.PreconditionFailed))]
        [InlineData(nameof(ErrorType.PreconditionRequired))]
        [InlineData(nameof(ErrorType.Uninitialized))]
        public void Code_And_Title_AreBothWritten_ForEveryBuiltInType(string typeName) {
            // Arrange
            ErrorType type = new(typeName);
            Error error = Error.Custom(type, $"{typeName}.Probe", "Probe description.");

            // Act
            ProblemDetails problem = error.ToProblemDetails(null, Bare);

            // Assert - no built-in type projects to a body missing either member.
            Assert.Equal(typeName, problem.Title);
            Assert.Equal($"{typeName}.Probe", problem.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey]);
            Assert.Equal("about:blank", problem.Type);
        }
    }

    [Collection("ProblemDetailsProjection")]
    public sealed class TheViolationsExtension {

        [Fact]
        public void Violations_SingleError_IsNotAdded() {
            // Act
            ProblemDetails problem = Result.Failure<int>(Error.NotFound("User.NotFound", "Missing."))
                .ToProblemDetails(null, Bare);

            // Assert
            Assert.False(problem.Extensions.ContainsKey(ResultProblemDetailsExtensions.ViolationsExtensionKey));
        }

        [Fact]
        public void Violations_MultipleErrors_KeepsEveryEntryIncludingTheProjectedFirst() {
            // Arrange
            List<Error> errors = [
                Error.Validation("Email.Invalid", "Bad email."),
                Error.Validation("Password.Weak", "Weak password.")
            ];

            // Act
            Result<string> failureResult = errors;
            ProblemDetails problem = failureResult.ToProblemDetails(null, Bare);

            // Assert - the rename is the change; the content rule (all errors, first one included) is untouched.
            Assert.True(problem.Extensions.TryGetValue(
                ResultProblemDetailsExtensions.ViolationsExtensionKey,
                out object? violations));
            Assert.Equal(2, ((Array)violations!).Length);
        }

        [Fact]
        public void Violations_RoundTripsThroughJson_WithTheDocumentedMemberNames() {
            // Arrange
            List<Error> errors = [
                Error.Validation("Email.Invalid", "Bad email."),
                Error.Validation("Password.Weak", "Weak password.")
            ];
            Result<string> failureResult = errors;
            ProblemDetails problem = failureResult.ToProblemDetails(null, Bare);

            // Act
            string json = JsonSerializer.Serialize(problem);

            // Assert - a consumer reads bytes, not objects.
            Assert.Contains("\"violations\":[", json, StringComparison.Ordinal);
            Assert.Contains("\"code\":\"Email.Invalid\"", json, StringComparison.Ordinal);
            Assert.Contains("\"description\":\"Weak password.\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"errors\":[", json, StringComparison.Ordinal);
        }
    }

    [Collection("ProblemDetailsProjection")]
    public sealed class TheSerializedBody {

        [Fact]
        public void Body_DefaultProjection_HasThe9457MemberRoles() {
            // Arrange
            Error error = Error.NotFound("User.NotFound", "User with id '42' was not found.")
                .WithMetadata("UserId", 42);

            // Act
            string json = JsonSerializer.Serialize(error.ToProblemDetails("/api/users/42", Bare));

            // Assert
            Assert.Contains("\"type\":\"about:blank\"", json, StringComparison.Ordinal);
            Assert.Contains("\"title\":\"NotFound\"", json, StringComparison.Ordinal);
            Assert.Contains("\"status\":404", json, StringComparison.Ordinal);
            Assert.Contains("\"code\":\"User.NotFound\"", json, StringComparison.Ordinal);
            Assert.Contains("\"instance\":\"/api/users/42\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"title\":\"User.NotFound\"", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Body_NamespaceConfigured_TypeIsProducerOwnedAndDistinct() {
            // Act
            string userJson = JsonSerializer.Serialize(
                Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, Named));
            string orderJson = JsonSerializer.Serialize(
                Error.NotFound("Order.NotFound", "Missing.").ToProblemDetails(null, Named));

            // Assert
            Assert.Contains($"\"type\":\"{ProducerNamespace}/User.NotFound\"", userJson, StringComparison.Ordinal);
            Assert.Contains($"\"type\":\"{ProducerNamespace}/Order.NotFound\"", orderJson, StringComparison.Ordinal);
        }

        [Fact]
        public void Body_StatusAndDetail_NeverMoved() {
            // Act
            ProblemDetails problem = Error.Conflict("Order.AlreadyExists", "Order is duplicate.")
                .ToProblemDetails(null, Bare);
            string json = JsonSerializer.Serialize(problem);

            // Assert - the two members consumers already read correctly are untouched by #210.
            Assert.Equal(409, problem.Status);
            Assert.Equal("Order is duplicate.", problem.Detail);
            Assert.Contains("\"status\":409", json, StringComparison.Ordinal);
            Assert.Contains("\"detail\":\"Order is duplicate.\"", json, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The ambient path. This is the only class that assigns <see cref="ProblemDetailsProjection.Default"/>, and it
    /// restores it on the way out; the classes that assert the default projection are in the same collection so
    /// nothing reads it while it is changed.
    /// </summary>
    [Collection("ProblemDetailsProjection")]
    public sealed class TheAmbientDefault : IDisposable {

        private readonly ProblemDetailsProjection original = ProblemDetailsProjection.Default;

        public void Dispose() {
            ProblemDetailsProjection.Default = original;
        }

        [Fact]
        public void ToProblemDetails_WithoutAnArgument_ReadsTheAmbientDefault() {
            // Arrange - a producer configures once at startup, exactly as an endpoint filter would see it.
            ProblemDetailsProjection.Default = new() { TypeNamespace = ProducerNamespace };

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert
            Assert.Equal($"{ProducerNamespace}/User.NotFound", problem.Type);
        }

        [Fact]
        public void ToProblemDetails_WithoutAnArgument_UsesTheBuiltInDefault_WhenNothingIsConfigured() {
            // Arrange
            ProblemDetailsProjection.Default = new();

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert
            Assert.Equal("about:blank", problem.Type);
            Assert.Equal(nameof(ErrorType.NotFound), problem.Title);
        }

        [Fact]
        public void ExplicitProjection_LeavesTheAmbientDefaultAlone() {
            // Arrange
            ProblemDetailsProjection fresh = new();
            ProblemDetailsProjection.Default = fresh;

            // Act
            Error.NotFound("User.NotFound", "Missing.").ToProblemDetails(null, Named);

            // Assert - the per-call path must not be a way to reconfigure the process.
            Assert.Same(fresh, ProblemDetailsProjection.Default);
            Assert.Null(ProblemDetailsProjection.Default.TypeNamespace);
        }
    }
}
