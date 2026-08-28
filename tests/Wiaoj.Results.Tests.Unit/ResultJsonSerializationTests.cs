using System.Text.Json;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Serialization)]
public sealed class ResultJsonSerializationTests {

    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public sealed class TheResultSerialization {
        [Fact]
        public void Serialize_SuccessResult_ProducesCleanJsonWithValue() {
            // Arrange
            Result<int> result = Result.Success(42);

            // Act
            string json = JsonSerializer.Serialize(result, SerializerOptions);

            // Assert
            Assert.Contains("\"isSuccess\":true", json);
            Assert.Contains("\"value\":42", json);
            Assert.DoesNotContain("_singleError", json);
            Assert.DoesNotContain("_multipleErrors", json);
        }

        [Fact]
        public void Serialize_FailureResult_ProducesCleanJsonWithErrors() {
            // Arrange
            Result<int> result = Result.Failure<int>(SomeError);

            // Act
            string json = JsonSerializer.Serialize(result, SerializerOptions);

            // Assert
            Assert.Contains("\"isSuccess\":false", json);
            Assert.Contains("\"errors\":", json);
            Assert.Contains("\"code\":\"Test.Failure\"", json);
            Assert.DoesNotContain("\"value\":", json);
        }

        [Fact]
        public void Serialize_ResultOfSuccess_ProducesCleanJson() {
            // Arrange
            Result<Success> result = Result.Success();

            // Act
            string json = JsonSerializer.Serialize(result, SerializerOptions);

            // Assert
            Assert.Contains("\"isSuccess\":true", json);
            Assert.DoesNotContain("\"errors\":", json);
        }
    }

    public sealed class TheResultDeserialization {
        [Fact]
        public void Deserialize_SuccessJson_ReconstructsSuccessfulResult() {
            // Arrange
            string json = "{\"isSuccess\":true,\"value\":100}";

            // Act
            Result<int> result = JsonSerializer.Deserialize<Result<int>>(json, SerializerOptions);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(100, result.Value);
        }

        [Fact]
        public void Deserialize_FailureJson_ReconstructsFailedResult() {
            // Arrange
            string json = "{\"isSuccess\":false,\"errors\":[{\"code\":\"Auth.Invalid\",\"description\":\"Invalid credentials\",\"type\":\"Unauthorized\"}]}";

            // Act
            Result<string> result = JsonSerializer.Deserialize<Result<string>>(json, SerializerOptions);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Auth.Invalid", result.FirstError.Code);
            Assert.Equal("Invalid credentials", result.FirstError.Description);
            Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
        }

        [Fact]
        public void RoundTrip_ComplexObjectResult_PreservesIntegrity() {
            // Arrange
            TestUser user = new("John Doe", "john@example.com");
            Result<TestUser> original = Result.Success(user);

            // Act
            string json = JsonSerializer.Serialize(original, SerializerOptions);
            Result<TestUser> deserialized = JsonSerializer.Deserialize<Result<TestUser>>(json, SerializerOptions);

            // Assert
            Assert.True(deserialized.IsSuccess);
            Assert.Equal("John Doe", deserialized.Value.Name);
            Assert.Equal("john@example.com", deserialized.Value.Email);
        }
    }

    public sealed class TheErrorSerialization {
        [Fact]
        public void SerializeAndDeserialize_ErrorWithMetadata_PreservesAllProperties() {
            // Arrange
            Error original = Error.Validation("User.Age.Invalid", "Age must be at least 18.")
                .WithMetadata("MinimumAge", 18)
                .WithMetadata("ProvidedAge", 15);

            // Act
            string json = JsonSerializer.Serialize(original, SerializerOptions);
            Error deserialized = JsonSerializer.Deserialize<Error>(json, SerializerOptions);

            // Assert
            Assert.Equal(original.Code, deserialized.Code);
            Assert.Equal(original.Description, deserialized.Description);
            Assert.Equal(original.Type, deserialized.Type);
            Assert.NotNull(deserialized.Metadata);
            Assert.Equal(2, deserialized.Metadata.Count);
        }

        [Fact]
        public void SerializeAndDeserialize_CustomErrorType_PreservesCustomTypeName() {
            // Arrange
            ErrorType customType = new("RateLimitExceeded");
            Error original = Error.Custom(customType, "Rate.Limit", "Too many requests.");

            // Act
            string json = JsonSerializer.Serialize(original, SerializerOptions);
            Error deserialized = JsonSerializer.Deserialize<Error>(json, SerializerOptions);

            // Assert
            Assert.Equal("RateLimitExceeded", deserialized.Type.Name);
            Assert.Equal(customType, deserialized.Type);
        }
    }

    private sealed record TestUser(string Name, string Email);
}