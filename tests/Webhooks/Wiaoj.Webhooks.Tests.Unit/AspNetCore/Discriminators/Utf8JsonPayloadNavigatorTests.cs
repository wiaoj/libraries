using System.Text;
using Wiaoj.Webhooks.AspNetCore;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore.Discriminators;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "PayloadNavigator")]
public sealed class Utf8JsonPayloadNavigatorTests {

    public sealed class TheTryExtractSubtreeMethod {

        [Fact]
        public void TryExtractSubtree_WhenPathIsRootOrEmpty_ReturnsOriginalPayload() {
            ReadOnlySpan<byte> rawJson = """{"id":"evt_1","amount":100}"""u8;

            bool resultEmpty = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, string.Empty, out ReadOnlySpan<byte> sliceEmpty);
            bool resultNull = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, (string)null!, out ReadOnlySpan<byte> sliceNull);

            Assert.True(resultEmpty);
            Assert.True(rawJson.SequenceEqual(sliceEmpty));

            Assert.True(resultNull);
            Assert.True(rawJson.SequenceEqual(sliceNull));
        }

        [Fact]
        public void TryExtractSubtree_WhenSingleLevelPathExists_ExtractsNestedObjectSuccessfully() {
            ReadOnlySpan<byte> rawJson = """{"id":"evt_100","data":{"order_id":"ORD-99","total":49.90}}"""u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, "data", out ReadOnlySpan<byte> subtree);

            Assert.True(result);
            string jsonString = Encoding.UTF8.GetString(subtree);
            Assert.Equal("""{"order_id":"ORD-99","total":49.90}""", jsonString);
        }

        [Fact]
        public void TryExtractSubtree_WhenTwoLevelDotDelimitedPathExists_ExtractsDeepNestedObject() {
            // Typical Stripe envelope: data.object
            ReadOnlySpan<byte> rawJson = """{"id":"evt_stripe_1","type":"payment_intent.succeeded","data":{"object":{"id":"pi_123","amount":5000,"currency":"usd"}}}"""u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, "data.object", out ReadOnlySpan<byte> subtree);

            Assert.True(result);
            string jsonString = Encoding.UTF8.GetString(subtree);
            Assert.Equal("""{"id":"pi_123","amount":5000,"currency":"usd"}""", jsonString);
        }

        [Fact]
        public void TryExtractSubtree_WhenThreeLevelDotDelimitedPathExists_ExtractsDeepNestedObject() {
            ReadOnlySpan<byte> rawJson = """{"a":{"b":{"c":{"target_value":"success"}}}}"""u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, "a.b.c", out ReadOnlySpan<byte> subtree);

            Assert.True(result);
            string jsonString = Encoding.UTF8.GetString(subtree);
            Assert.Equal("""{"target_value":"success"}""", jsonString);
        }

        [Fact]
        public void TryExtractSubtree_WhenTargetIsNestedArray_ExtractsRawArraySlice() {
            ReadOnlySpan<byte> rawJson = """{"event":"batch","data":{"items":[{"id":1},{"id":2}]}}"""u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, "data.items", out ReadOnlySpan<byte> subtree);

            Assert.True(result);
            string jsonString = Encoding.UTF8.GetString(subtree);
            Assert.Equal("""[{"id":1},{"id":2}]""", jsonString);
        }

        [Fact]
        public void TryExtractSubtree_WhenTargetIsPrimitiveString_ExtractsStringLiteralSlice() {
            ReadOnlySpan<byte> rawJson = """{"data":{"status":"active"}}"""u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, "data.status", out ReadOnlySpan<byte> subtree);

            Assert.True(result);
            string jsonString = Encoding.UTF8.GetString(subtree);
            Assert.Equal("\"active\"", jsonString);
        }

        [Fact]
        public void TryExtractSubtree_WhenTargetIsPrimitiveNumber_ExtractsNumberLiteralSlice() {
            ReadOnlySpan<byte> rawJson = """{"data":{"count":42}}"""u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, "data.count", out ReadOnlySpan<byte> subtree);

            Assert.True(result);
            string jsonString = Encoding.UTF8.GetString(subtree);
            Assert.Equal("42", jsonString);
        }

        [Fact]
        public void TryExtractSubtree_WhenPathDoesNotExist_ReturnsFalse() {
            ReadOnlySpan<byte> rawJson = """{"id":"evt_1","data":{"object":{"amount":100}}}"""u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, "data.non_existent_key", out ReadOnlySpan<byte> subtree);

            Assert.False(result);
            Assert.True(subtree.IsEmpty);
        }

        [Fact]
        public void TryExtractSubtree_WhenIntermediatePathDoesNotExist_ReturnsFalse() {
            ReadOnlySpan<byte> rawJson = """{"id":"evt_1","payload":{"amount":100}}"""u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(rawJson, "data.object", out ReadOnlySpan<byte> subtree);

            Assert.False(result);
            Assert.True(subtree.IsEmpty);
        }

        [Fact]
        public void TryExtractSubtree_WhenJsonIsMalformed_ReturnsFalse_WithoutThrowing() {
            ReadOnlySpan<byte> malformedJson = "{ invalid json payload "u8;

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(malformedJson, "data.object", out ReadOnlySpan<byte> subtree);

            Assert.False(result);
            Assert.True(subtree.IsEmpty);
        }

        [Fact]
        public void TryExtractSubtree_WhenJsonIsEmpty_ReturnsFalse() {
            ReadOnlySpan<byte> emptyJson = [];

            bool result = Utf8JsonPayloadNavigator.TryExtractSubtree(emptyJson, "data", out ReadOnlySpan<byte> subtree);

            Assert.False(result);
            Assert.True(subtree.IsEmpty);
        }
    }
}