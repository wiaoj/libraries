using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Wiaoj.Results.AspNetCore.Tests;

/// <summary>
/// Guards the RFC 9457 projection rules in <see cref="ResultProblemDetailsProjection"/> (#210): who owns
/// <c>type</c>, what <c>title</c> is allowed to be, and that the machine-readable code reaches the body in a
/// member a consumer can act on instead of being fished out of a human field.
/// </summary>
/// <remarks>
/// The projection is configured through process-wide statics, so every class here restores the defaults when it
/// finishes — the same shared-state discipline <c>ErrorTypeHttpMapper</c> tests use.
/// </remarks>
[Trait("Category", "ProblemDetailsProjection")]
public sealed class ResultProblemDetailsProjectionTests : IDisposable {

    private const string ProducerNamespace = "https://errors.example.dev";

    public ResultProblemDetailsProjectionTests() {
        Reset();
    }

    public void Dispose() {
        Reset();
    }

    private static void Reset() {
        ResultProblemDetailsProjection.TypeStyle = ProblemTypeStyle.ProducerNamespaceOrAboutBlank;
        ResultProblemDetailsProjection.TypeNamespace = null;
        ResultProblemDetailsProjection.Titles.Clear();
    }

    [Collection("ProblemDetailsProjection")]
    public sealed class TheTypeMember : IDisposable {

        public TheTypeMember() => Reset();

        public void Dispose() => Reset();

        [Fact]
        public void Type_WithNoNamespaceConfigured_IsAboutBlank() {
            // Arrange & Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert - RFC 9457 §3.1.1 blesses about:blank for "no type URI"; a third-party status site is not
            // a producer's answer and leaks the producer's taxonomy into someone else's analytics.
            Assert.Equal("about:blank", problem.Type);
            Assert.DoesNotContain("httpstatuses.io", problem.Type, StringComparison.Ordinal);
        }

        [Fact]
        public void Type_WithNamespaceConfigured_EndsWithTheErrorCode() {
            // Arrange
            ResultProblemDetailsProjection.TypeNamespace = ProducerNamespace;

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert - distinct per problem class, which is the whole point of `type`
            Assert.Equal($"{ProducerNamespace}/User.NotFound", problem.Type);
        }

        [Fact]
        public void Type_TwoErrorsOfTheSameStatus_ButDifferentCodes_GetDifferentTypes() {
            // Arrange - the defect #210 describes: two different NotFound answers shared one `type` because it
            // was keyed on the status code alone.
            ResultProblemDetailsProjection.TypeNamespace = ProducerNamespace;

            // Act
            string userMissing = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails().Type;
            string orderMissing = Error.NotFound("Order.NotFound", "Missing.").ToProblemDetails().Type;

            // Assert
            Assert.NotEqual(userMissing, orderMissing);
        }

        [Fact]
        public void Type_NamespaceWithTrailingSlash_DoesNotDoubleTheSlash() {
            // Arrange
            ResultProblemDetailsProjection.TypeNamespace = $"{ProducerNamespace}/";

            // Act
            string type = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails().Type;

            // Assert
            Assert.Equal($"{ProducerNamespace}/User.NotFound", type);
        }

        [Fact]
        public void Type_WhitespaceNamespace_IsTreatedAsUnconfigured() {
            // Arrange
            ResultProblemDetailsProjection.TypeNamespace = "   ";

            // Act
            string type = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails().Type;

            // Assert
            Assert.Equal("about:blank", type);
        }

        [Fact]
        public void Type_LegacyStyle_ReproducesThePre9457Uri() {
            // Arrange - the escape hatch for a consumer that cannot migrate in the same release.
            ResultProblemDetailsProjection.TypeStyle = ProblemTypeStyle.HttpStatusIo;

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert
            Assert.Equal("https://httpstatuses.io/404", problem.Type);
        }

        [Fact]
        public void Type_LegacyStyle_StillCarriesTheCodeExtension() {
            // Arrange - pinning the old `type` must not cost the consumer the machine key; that is the pair of
            // fields that made the old shape work at all.
            ResultProblemDetailsProjection.TypeStyle = ProblemTypeStyle.HttpStatusIo;

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert
            Assert.Equal("User.NotFound", problem.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey]);
        }
    }

    [Collection("ProblemDetailsProjection")]
    public sealed class TheTitleMember : IDisposable {

        public TheTitleMember() => Reset();

        public void Dispose() => Reset();


        [Fact]
        public void Title_IsTheErrorTypeName_NotTheErrorCode() {
            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert - §3.2 wants a short human summary that repeats per problem type. The code is no longer
            // allowed to squat here: it changes per instance.
            Assert.Equal(nameof(ErrorType.NotFound), problem.Title);
            Assert.DoesNotContain("User.NotFound", problem.Title, StringComparison.Ordinal);
        }

        [Fact]
        public void Title_IsTheSameForEveryErrorOfTheSameType() {
            // Arrange
            ProblemDetails first = Error.Validation("Email.Invalid", "Bad email.").ToProblemDetails();
            ProblemDetails second = Error.Validation("Password.Weak", "Weak password.").ToProblemDetails();

            // Assert
            Assert.Equal(first.Title, second.Title);
            Assert.NotEqual(first.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey],
                second.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey]);
        }

        [Fact]
        public void Title_ConfiguredOverride_Wins() {
            // Arrange
            ResultProblemDetailsProjection.Titles[nameof(ErrorType.NotFound)] = "Resource Not Found";

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert
            Assert.Equal("Resource Not Found", problem.Title);
        }

        [Fact]
        public void Title_BlankOverride_FallsBackToTheTypeName() {
            // Arrange - a configured blank must not put an empty title on the wire.
            ResultProblemDetailsProjection.Titles[nameof(ErrorType.NotFound)] = "  ";

            // Act
            ProblemDetails problem = Error.NotFound("User.NotFound", "Missing.").ToProblemDetails();

            // Assert
            Assert.Equal(nameof(ErrorType.NotFound), problem.Title);
        }
    }

    [Collection("ProblemDetailsProjection")]
    public sealed class TheCodeExtension : IDisposable {

        public TheCodeExtension() => Reset();

        public void Dispose() => Reset();


        [Fact]
        public void Code_IsPresentEvenWithoutMetadata() {
            // Act
            ProblemDetails problem = Error.Conflict("Order.AlreadyExists", "Duplicate.").ToProblemDetails();

            // Assert - this is the member a deployed client reads instead of `title`.
            Assert.Equal("Order.AlreadyExists", problem.Extensions[ResultProblemDetailsExtensions.CodeExtensionKey]);
        }

        [Fact]
        public void Code_WinsOverAMetadataEntryOfTheSameName() {
            // Arrange - a producer that puts its own "code" in metadata would otherwise make the member mean two
            // things, which is the consumer trap #210 renamed `errors` for.
            Error error = Error.Validation("Form.Invalid", "Bad input.").WithMetadata("code", "SOMETHING_ELSE");

            // Act
            ProblemDetails problem = error.ToProblemDetails();

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
            string fromError = failureResult.FirstError.ToProblemDetails()
                .Extensions[ResultProblemDetailsExtensions.CodeExtensionKey] as string;
            string fromResult = failureResult.ToProblemDetails()
                .Extensions[ResultProblemDetailsExtensions.CodeExtensionKey] as string;

            // Assert
            Assert.Equal("Email.Invalid", fromError);
            Assert.Equal(fromError, fromResult);
        }
    }

    [Collection("ProblemDetailsProjection")]
    public sealed class TheViolationsExtension : IDisposable {

        public TheViolationsExtension() => Reset();

        public void Dispose() => Reset();


        [Fact]
        public void Violations_SingleError_IsNotAdded() {
            // Act
            ProblemDetails problem = Result.Failure<int>(Error.NotFound("User.NotFound", "Missing."))
                .ToProblemDetails();

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
            ProblemDetails problem = failureResult.ToProblemDetails();

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
            ProblemDetails problem = ((Result<string>)errors).ToProblemDetails();

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
    public sealed class TheSerializedBody : IDisposable {

        public TheSerializedBody() => Reset();

        public void Dispose() => Reset();


        [Fact]
        public void Body_DefaultProjection_HasThe9457MemberRoles() {
            // Arrange
            Error error = Error.NotFound("User.NotFound", "User with id '42' was not found.")
                .WithMetadata("UserId", 42);

            // Act
            string json = JsonSerializer.Serialize(error.ToProblemDetails(instance: "/api/users/42"));

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
            // Arrange
            ResultProblemDetailsProjection.TypeNamespace = ProducerNamespace;

            // Act
            string userJson = JsonSerializer.Serialize(Error.NotFound("User.NotFound", "Missing.").ToProblemDetails());
            string orderJson = JsonSerializer.Serialize(Error.NotFound("Order.NotFound", "Missing.").ToProblemDetails());

            // Assert
            Assert.Contains($"\"type\":\"{ProducerNamespace}/User.NotFound\"", userJson, StringComparison.Ordinal);
            Assert.Contains($"\"type\":\"{ProducerNamespace}/Order.NotFound\"", orderJson, StringComparison.Ordinal);
        }

        [Fact]
        public void Body_StatusAndDetail_NeverMoved() {
            // Arrange & Act
            ProblemDetails problem = Error.Conflict("Order.AlreadyExists", "Order is duplicate.").ToProblemDetails();
            string json = JsonSerializer.Serialize(problem);

            // Assert - the two members consumers already read correctly are untouched by #210.
            Assert.Equal(409, problem.Status);
            Assert.Equal("Order is duplicate.", problem.Detail);
            Assert.Contains("\"status\":409", json, StringComparison.Ordinal);
            Assert.Contains("\"detail\":\"Order is duplicate.\"", json, StringComparison.Ordinal);
        }
    }
}
