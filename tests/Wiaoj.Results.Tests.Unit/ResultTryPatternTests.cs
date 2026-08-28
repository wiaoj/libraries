namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Try)]
public sealed class ResultTryPatternTests {

    public sealed class TheParseMethod {
        [Fact]
        public void Parse_ValidInteger_ReturnsSuccessResult() {
            // Arrange
            string rawInput = "123";

            // Act
            Result<int> result = Result.Parse<int>(
                rawInput,
                Error.Validation("Number.Invalid", "Input is not a valid number."));

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(123, result.Value);
        }

        [Fact]
        public void Parse_InvalidInteger_ReturnsSpecifiedError() {
            // Arrange
            string rawInput = "invalid";

            // Act
            Result<int> result = Result.Parse<int>(
                rawInput,
                Error.Validation("Number.Invalid", "Input is not a valid number."));

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Number.Invalid", result.FirstError.Code);
        }

        [Fact]
        public void Parse_WithLazyErrorFactory_WhenFailed_ExecutesFactory() {
            // Arrange
            string rawInput = "invalid-guid";
            bool factoryCalled = false;

            // Act
            Result<Guid> result = Result.Parse<Guid>(
                rawInput,
                input => {
                    factoryCalled = true;
                    return Error.Validation("Guid.Invalid", $"'{input}' is not a valid GUID.");
                });

            // Assert
            Assert.True(result.IsFailure);
            Assert.True(factoryCalled);
            Assert.Contains("invalid-guid", result.FirstError.Description);
        }
    }

    public sealed class TheFromTryMethodWithCustomDelegate {
        private static bool TryDecodeAppId(string input, out int decodedId) {
            if(input.StartsWith("app_") && int.TryParse(input[4..], out int id)) {
                decodedId = id;
                return true;
            }

            decodedId = 0;
            return false;
        }

        [Fact]
        public void FromTry_CustomDecoder_WhenValid_ReturnsSuccess() {
            // Arrange
            string rawInput = "app_99";

            // Act
            Result<int> result = Result.FromTry<string, int>(
                rawInput,
                TryDecodeAppId,
                Error.NotFound("App.NotFound", "Application not found."));

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(99, result.Value);
        }

        [Fact]
        public void FromTry_CustomDecoder_WhenInvalid_ReturnsError() {
            // Arrange
            string rawInput = "unknown_format";

            // Act
            Result<int> result = Result.FromTry<string, int>(
                rawInput,
                TryDecodeAppId,
                Error.NotFound("App.NotFound", "Application not found."));

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("App.NotFound", result.FirstError.Code);
        }
    }

    public sealed class TheParameterlessFromTryMethod {
        private static bool TryGetConfigValue(out string value) {
            value = "db-connection-string";
            return true;
        }

        private static bool TryGetMissingConfig(out string value) {
            value = string.Empty;
            return false;
        }

        [Fact]
        public void FromTry_Parameterless_WhenReturnsTrue_ReturnsSuccessResult() {
            // Arrange & Act
            Result<string> result = Result.FromTry<string>(
                TryGetConfigValue,
                Error.NotFound("Config.NotFound", "Missing configuration"));

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("db-connection-string", result.Value);
        }

        [Fact]
        public void FromTry_Parameterless_WhenReturnsFalse_ReturnsSpecifiedError() {
            // Arrange & Act
            Result<string> result = Result.FromTry<string>(
                TryGetMissingConfig,
                Error.NotFound("Config.NotFound", "Missing configuration"));

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Config.NotFound", result.FirstError.Code);
        }
    }
}