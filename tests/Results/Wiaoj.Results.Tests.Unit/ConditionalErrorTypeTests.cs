using System.Reflection;

namespace Wiaoj.Results.Tests.Unit;

/// <summary>
/// Guards the conditional-request / request-shape built-ins (<c>PreconditionFailed</c>,
/// <c>PreconditionRequired</c>, <c>UnsupportedMediaType</c>, <c>MethodNotAllowed</c>, <c>NotImplemented</c>):
/// their <see cref="ErrorType"/> names and their <see cref="Error"/> factories.
/// Their HTTP mappings are guarded by the AspNetCore suite, which is the project that owns the table.
/// </summary>
[Trait("Category", Category.Error)]
public sealed class ConditionalErrorTypeTests {

    public sealed class TheBuiltInErrorTypeFields {

        [Fact]
        public void EveryBuiltInField_NameMatchesTheMemberItIsDeclaredOn() {
            // Arrange - a built-in declared as new("PreconditionFaild") would key the HTTP table under a
            // name no producer can spell, so the nameof() convention is worth pinning for every field.
            FieldInfo[] fields = typeof(ErrorType)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(ErrorType))
                .ToArray();

            Assert.NotEmpty(fields);

            foreach(FieldInfo field in fields) {
                // Act
                ErrorType builtIn = (ErrorType)field.GetValue(null)!;

                // Assert
                Assert.Equal(field.Name, builtIn.Name);
            }
        }

        [Theory]
        [InlineData(nameof(ErrorType.PreconditionFailed))]
        [InlineData(nameof(ErrorType.PreconditionRequired))]
        [InlineData(nameof(ErrorType.UnsupportedMediaType))]
        [InlineData(nameof(ErrorType.MethodNotAllowed))]
        [InlineData(nameof(ErrorType.NotImplemented))]
        public void ConditionalErrorType_IsDeclaredAsABuiltIn(string expectedName) {
            // Arrange
            FieldInfo field = typeof(ErrorType).GetField(expectedName, BindingFlags.Public | BindingFlags.Static)
                ?? throw new Xunit.Sdk.XunitException($"ErrorType.{expectedName} is not declared.");

            // Act
            ErrorType errorType = (ErrorType)field.GetValue(null)!;

            // Assert
            Assert.Equal(expectedName, errorType.Name);
            Assert.Equal(expectedName, errorType.ToString());
        }

        [Fact]
        public void PreconditionFailed_And_PreconditionRequired_AreDifferentTypes() {
            // Arrange & Act - 412 is a precondition that failed, 428 is a precondition that was never sent.
            // Folding them into one type would blame the client for the wrong omission.
            ErrorType failed = ErrorType.PreconditionFailed;
            ErrorType required = ErrorType.PreconditionRequired;

            // Assert
            Assert.NotEqual(failed, required);
        }

        [Fact]
        public void NotImplemented_And_Unavailable_AreDifferentTypes() {
            // Arrange & Act - 501 says "never, as deployed", 503 says "supported, but retry later".
            ErrorType notImplemented = ErrorType.NotImplemented;
            ErrorType unavailable = ErrorType.Unavailable;

            // Assert
            Assert.NotEqual(notImplemented, unavailable);
        }
    }

    public sealed class TheBuiltInFactoryMethods {

        [Fact]
        public void PreconditionFailed_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.PreconditionFailed();

            // Assert
            Assert.Equal("Request.PreconditionFailed", error.Code);
            Assert.Equal(ErrorType.PreconditionFailed, error.Type);
            Assert.False(string.IsNullOrEmpty(error.Description));
            Assert.Null(error.Metadata);
            Assert.Null(error.Cause);
        }

        [Fact]
        public void PreconditionFailed_CustomParameters_StoresValuesCorrectly() {
            // Arrange & Act
            Error error = Error.PreconditionFailed("Order.ETagStale", "The ETag you sent is no longer current.");

            // Assert
            Assert.Equal("Order.ETagStale", error.Code);
            Assert.Equal("The ETag you sent is no longer current.", error.Description);
            Assert.Equal(ErrorType.PreconditionFailed, error.Type);
        }

        [Fact]
        public void PreconditionRequired_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.PreconditionRequired();

            // Assert
            Assert.Equal("Request.PreconditionRequired", error.Code);
            Assert.Equal(ErrorType.PreconditionRequired, error.Type);
            Assert.False(string.IsNullOrEmpty(error.Description));
        }

        [Fact]
        public void PreconditionRequired_CustomParameters_StoresValuesCorrectly() {
            // Arrange & Act
            Error error = Error.PreconditionRequired("Cart.BlindWrite", "Update the cart with If-Match.");

            // Assert
            Assert.Equal("Cart.BlindWrite", error.Code);
            Assert.Equal("Update the cart with If-Match.", error.Description);
            Assert.Equal(ErrorType.PreconditionRequired, error.Type);
        }

        [Fact]
        public void UnsupportedMediaType_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.UnsupportedMediaType();

            // Assert
            Assert.Equal("Request.UnsupportedMediaType", error.Code);
            Assert.Equal(ErrorType.UnsupportedMediaType, error.Type);
            Assert.False(string.IsNullOrEmpty(error.Description));
        }

        [Fact]
        public void UnsupportedMediaType_CustomParameters_StoresValuesCorrectly() {
            // Arrange & Act
            Error error = Error.UnsupportedMediaType("Import.NotXml", "This endpoint only reads application/xml.");

            // Assert
            Assert.Equal("Import.NotXml", error.Code);
            Assert.Equal("This endpoint only reads application/xml.", error.Description);
            Assert.Equal(ErrorType.UnsupportedMediaType, error.Type);
        }

        [Fact]
        public void MethodNotAllowed_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.MethodNotAllowed();

            // Assert
            Assert.Equal("Request.MethodNotAllowed", error.Code);
            Assert.Equal(ErrorType.MethodNotAllowed, error.Type);
            Assert.False(string.IsNullOrEmpty(error.Description));
        }

        [Fact]
        public void MethodNotAllowed_CustomParameters_StoresValuesCorrectly() {
            // Arrange & Act
            Error error = Error.MethodNotAllowed("Report.DeleteForbidden", "Reports support GET only.");

            // Assert
            Assert.Equal("Report.DeleteForbidden", error.Code);
            Assert.Equal("Reports support GET only.", error.Description);
            Assert.Equal(ErrorType.MethodNotAllowed, error.Type);
        }

        [Fact]
        public void NotImplemented_DefaultParameters_SetsDefaultValues() {
            // Arrange & Act
            Error error = Error.NotImplemented();

            // Assert
            Assert.Equal("Service.NotImplemented", error.Code);
            Assert.Equal(ErrorType.NotImplemented, error.Type);
            Assert.False(string.IsNullOrEmpty(error.Description));
        }

        [Fact]
        public void NotImplemented_CustomParameters_StoresValuesCorrectly() {
            // Arrange & Act
            Error error = Error.NotImplemented("Webhook.PatchUnsupported", "PATCH is not implemented yet.");

            // Assert
            Assert.Equal("Webhook.PatchUnsupported", error.Code);
            Assert.Equal("PATCH is not implemented yet.", error.Description);
            Assert.Equal(ErrorType.NotImplemented, error.Type);
        }

        [Theory]
        [InlineData("PreconditionFailed")]
        [InlineData("PreconditionRequired")]
        [InlineData("UnsupportedMediaType")]
        [InlineData("MethodNotAllowed")]
        [InlineData("NotImplemented")]
        public void ConditionalFactories_DefaultErrors_AllCarryTheirOwnType(string factoryName) {
            // Arrange & Act - each factory must tag its error with the same-named built-in, never with Failure.
            Error error = factoryName switch {
                "PreconditionFailed" => Error.PreconditionFailed(),
                "PreconditionRequired" => Error.PreconditionRequired(),
                "UnsupportedMediaType" => Error.UnsupportedMediaType(),
                "MethodNotAllowed" => Error.MethodNotAllowed(),
                "NotImplemented" => Error.NotImplemented(),
                _ => throw new Xunit.Sdk.XunitException($"Unknown factory {factoryName}.")
            };

            ErrorType declaredType = (ErrorType)typeof(ErrorType)
                .GetField(factoryName, BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;

            // Assert
            Assert.Equal(declaredType, error.Type);
            Assert.NotEqual(ErrorType.Failure, error.Type);
            Assert.False(string.IsNullOrWhiteSpace(error.Description));
        }
    }
}
