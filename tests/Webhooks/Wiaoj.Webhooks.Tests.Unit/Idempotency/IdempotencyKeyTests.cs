using System.Text;
using System.Text.Json;
using Wiaoj.Primitives.Hashing;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Idempotency;

[Trait("Category", "Unit")]
[Trait("Feature", "Idempotency")]
[Trait("Component", "ValueObject")]
public sealed class IdempotencyKeyTests {

    public sealed class TheCreateMethod {
        [Fact]
        public void Create_GeneratesExpectedDeterministicFormat() {
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
            const string eventName = "invoice.paid";
            XxHash128 hash = XxHash128.Compute("{\"id\":100}");

            IdempotencyKey key = IdempotencyKey.Create(endpointId, eventName, hash);

            string expected = $"idemp:customer-1:invoice.paid:{hash}";
            Assert.Equal(expected, key.Value);
            Assert.Equal(expected, key.ToString());
        }

        [Fact]
        public void Create_Throws_WhenEventNameIsNullOrWhiteSpace() {
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
            XxHash128 hash = XxHash128.Compute("test");

            Assert.ThrowsAny<ArgumentException>(() =>
                IdempotencyKey.Create(endpointId, "", hash));
        }
    }

    public sealed class ParsingAndFormatting {
        [Fact]
        public void Parse_And_TryParse_WorkAcrossStringAndSpans() {
            const string raw = "idemp:ep1:order.created:0123456789abcdef0123456789abcdef";

            IdempotencyKey parsed1 = IdempotencyKey.Parse(raw);
            IdempotencyKey parsed2 = IdempotencyKey.Parse(raw.AsSpan());

            Assert.Equal(raw, parsed1.Value);
            Assert.Equal(parsed1, parsed2);

            Assert.True(IdempotencyKey.TryParse(raw, out IdempotencyKey try1));
            Assert.True(IdempotencyKey.TryParse(raw.AsSpan(), out IdempotencyKey try2));
            Assert.Equal(raw, try1.Value);
            Assert.Equal(raw, try2.Value);

            Assert.False(IdempotencyKey.TryParse((string?)null, out _));
            Assert.False(IdempotencyKey.TryParse("   ", out _));
        }

        [Fact]
        public void TryFormat_WritesCorrectlyToCharAndUtf8Span() {
            IdempotencyKey key = new("idemp:test-key-123");

            Span<char> charBuf = stackalloc char[32];
            Assert.True(key.TryFormat(charBuf, out int charsWritten));
            Assert.Equal("idemp:test-key-123", charBuf[..charsWritten].ToString());

            Span<byte> utf8Buf = stackalloc byte[32];
            Assert.True(key.TryFormat(utf8Buf, out int bytesWritten));
            Assert.Equal("idemp:test-key-123", Encoding.UTF8.GetString(utf8Buf[..bytesWritten]));
        }
    }

    public sealed class EqualityAndSerialization {
        [Fact]
        public void Equality_ComparesByValue() {
            IdempotencyKey key1 = new("idemp:a:b:1");
            IdempotencyKey key2 = new("idemp:a:b:1");
            IdempotencyKey key3 = new("idemp:a:b:2");

            Assert.Equal(key1, key2);
            Assert.NotEqual(key1, key3);
            Assert.True(key1 == key2);
            Assert.False(key1 != key2);
            Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
        }

        [Fact]
        public void JsonSerialization_SerializesAsFlatString() {
            IdempotencyKey key = new("idemp:tenant-9:event:abc");

            string json = JsonSerializer.Serialize(key);
            Assert.Equal("\"idemp:tenant-9:event:abc\"", json);

            IdempotencyKey deserialized = JsonSerializer.Deserialize<IdempotencyKey>(json);
            Assert.Equal(key, deserialized);
        }
    }
}