using Wiaoj.Pagination.AspNetCore.Caching;
using Xunit;

namespace Wiaoj.Pagination.AspNetCore.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "AspNetCore.Caching")]
public sealed class ETagGeneratorTests {

    public sealed class GenerateWeakETagMethod {
        [Fact]
        public void Should_Generate_Valid_Weak_ETag_Format() {
            // Arrange
            byte[] payload = "{\"items\":[1,2,3],\"metadata\":{}}"u8.ToArray();

            // Act
            string etag = ETagGenerator.GenerateWeakETag(payload);

            // Assert: Must start with W/" and end with "
            Assert.StartsWith("W/\"", etag);
            Assert.EndsWith("\"", etag);
            Assert.Equal(20, etag.Length); // W/" + 16 hex + "
        }

        [Fact]
        public void Should_Produce_Identical_ETags_For_Identical_Payloads() {
            // Arrange
            byte[] payload1 = "sample_paginated_json_payload"u8.ToArray();
            byte[] payload2 = "sample_paginated_json_payload"u8.ToArray();

            // Act
            string etag1 = ETagGenerator.GenerateWeakETag(payload1);
            string etag2 = ETagGenerator.GenerateWeakETag(payload2);

            // Assert
            Assert.Equal(etag1, etag2);
        }

        [Fact]
        public void Should_Handle_Empty_Payload_Safely() {
            // Act
            string etag = ETagGenerator.GenerateWeakETag(ReadOnlySpan<byte>.Empty);

            // Assert
            Assert.StartsWith("W/\"", etag);
            Assert.EndsWith("\"", etag);
            Assert.Equal(20, etag.Length);
        }
    }

    public sealed class TryGenerateWeakETagMethod {
        [Fact]
        public void Should_Format_Directly_Into_Span_Without_Allocations() {
            // Arrange
            byte[] payload = "test_payload"u8.ToArray();
            Span<char> destination = stackalloc char[32];

            // Act
            bool success = ETagGenerator.TryGenerateWeakETag(payload, destination, out int charsWritten);

            // Assert
            Assert.True(success);
            string formatted = destination[..charsWritten].ToString();
            Assert.StartsWith("W/\"", formatted);
            Assert.EndsWith("\"", formatted);
            Assert.Equal(20, charsWritten);
        }

        [Fact]
        public void Should_Return_False_When_Destination_Buffer_Is_Too_Small() {
            // Arrange
            byte[] payload = "test_payload"u8.ToArray();
            Span<char> smallBuffer = stackalloc char[5];

            // Act
            bool success = ETagGenerator.TryGenerateWeakETag(payload, smallBuffer, out int charsWritten);

            // Assert
            Assert.False(success);
            Assert.Equal(0, charsWritten);
        }
    }

    public sealed class GenerateStrongETagMethod {
        [Fact]
        public void Should_Generate_Valid_Strong_ETag_Format() {
            // Arrange
            byte[] payload = "cryptographic_integrity_payload"u8.ToArray();

            // Act
            string etag = ETagGenerator.GenerateStrongETag(payload);

            // Assert: 64 hex + 2 quotes
            Assert.StartsWith("\"", etag);
            Assert.EndsWith("\"", etag);
            Assert.Equal(66, etag.Length);
        }
    }

    public sealed class IsNotModifiedMethod {
        [Fact]
        public void Should_Return_True_When_ETags_Match_Exactly() {
            // Arrange
            string currentETag = "W/\"0123456789abcdef\"";
            string ifNoneMatch = "W/\"0123456789abcdef\"";

            // Act
            bool isNotModified = ETagGenerator.IsNotModified(ifNoneMatch, currentETag);

            // Assert
            Assert.True(isNotModified);
        }

        [Fact]
        public void Should_Return_True_When_IfNoneMatch_Contains_Wildcard() {
            // Arrange: RFC 9110 wildcard '*'
            string currentETag = "W/\"abcdef123456\"";
            string ifNoneMatch = "*";

            // Act
            bool isNotModified = ETagGenerator.IsNotModified(ifNoneMatch, currentETag);

            // Assert
            Assert.True(isNotModified);
        }

        [Fact]
        public void Should_Return_True_When_Matching_ETag_Is_Inside_Comma_Separated_List() {
            // Arrange
            string currentETag = "W/\"target_etag\"";
            string ifNoneMatch = "W/\"old_etag_1\", W/\"target_etag\", \"another_etag\"";

            // Act
            bool isNotModified = ETagGenerator.IsNotModified(ifNoneMatch, currentETag);

            // Assert
            Assert.True(isNotModified);
        }

        [Fact]
        public void Should_Return_False_When_ETags_Do_Not_Match() {
            // Arrange
            string currentETag = "W/\"new_content_etag\"";
            string ifNoneMatch = "W/\"old_cached_etag\"";

            // Act
            bool isNotModified = ETagGenerator.IsNotModified(ifNoneMatch, currentETag);

            // Assert
            Assert.False(isNotModified);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_Return_False_When_IfNoneMatch_Header_Is_Missing_Or_Empty(string? emptyHeader) {
            // Arrange
            string currentETag = "W/\"some_etag\"";

            // Act
            bool isNotModified = ETagGenerator.IsNotModified(emptyHeader, currentETag);

            // Assert
            Assert.False(isNotModified);
        }
    }
}