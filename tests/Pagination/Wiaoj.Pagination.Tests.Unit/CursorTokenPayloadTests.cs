using System.Text;
using Xunit;

namespace Wiaoj.Pagination.Tests.Unit;

/// <summary>
/// Reading a cursor's payload back out.
/// </summary>
/// <remarks>
/// <para>
/// <c>FromUtf8</c> and <c>FromBytes</c> had no counterpart, so the only way back was
/// <c>GetDecodedLength</c> plus a rented buffer plus <c>TryDecode</c> — eight lines at every call site that
/// decodes a cursor, which is every keyset endpoint with a strongly-typed key.
/// </para>
/// <para>
/// The shortcut that presented itself was <c>token.Value</c>, which is a string, and is the wrong one: it is
/// the Base64Url wire form. A decoder written against it compiles and reads plausibly. It also fails on the
/// second page, never the first — the first page carries no cursor — so it survives a casual test.
/// Both package READMEs documented it that way.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Feature", "Pagination")]
[Trait("Component", "CursorToken")]
public sealed class CursorTokenPayloadTests {

    public sealed class TheTextPayload {
        [Theory]
        [InlineData("ast_3Cs5HYmWY9UaCQRv65KApx")]
        [InlineData("order_9901")]
        [InlineData("10920")]
        [InlineData("a")]
        [InlineData("ürün-42 ☕")]
        public void Should_Round_Trip(string payload) {
            Assert.Equal(payload, CursorToken.FromUtf8(payload).ToUtf8String());
        }

        [Fact]
        public void Should_Round_Trip_A_Payload_Past_The_Stack_Threshold() {
            string payload = new('x', 4096);

            Assert.Equal(payload, CursorToken.FromUtf8(payload).ToUtf8String());
        }

        [Fact]
        public void Should_Be_Empty_For_An_Empty_Token() {
            Assert.Equal(string.Empty, CursorToken.Empty.ToUtf8String());
        }

        [Fact]
        public void Should_Survive_A_Trip_Through_The_Wire_Form() {
            // What actually happens between two requests: the token is written into a URL and parsed back.
            CursorToken issued = CursorToken.FromUtf8("ast_3Cs5HYmWY9UaCQRv65KApx");

            CursorToken returned = CursorToken.Parse(issued.Value);

            Assert.Equal("ast_3Cs5HYmWY9UaCQRv65KApx", returned.ToUtf8String());
        }
    }

    public sealed class TheBinaryPayload {
        [Fact]
        public void Should_Round_Trip() {
            byte[] payload = BitConverter.GetBytes(9_007_199_254_740_993L);

            Assert.Equal(payload, CursorToken.FromBytes(payload).ToBytes());
        }

        [Fact]
        public void Should_Be_Empty_For_An_Empty_Token() {
            Assert.Empty(CursorToken.Empty.ToBytes());
        }
    }

    /// <summary>
    /// Pins the distinction the old documentation got wrong, so it cannot quietly become true again.
    /// </summary>
    public sealed class TheWireForm {
        [Fact]
        public void Should_Not_Be_The_Payload() {
            CursorToken token = CursorToken.FromUtf8("ACC-4471");

            Assert.Equal("QUNDLTQ0NzE", token.Value);
            Assert.NotEqual(token.Value, token.ToUtf8String());
        }

        [Fact]
        public void Should_Not_Parse_As_The_Number_That_Was_Encoded() {
            // The example in src/Pagination/README.md, before this change: long.Parse(token.Value).
            CursorToken token = CursorToken.FromUtf8(10920L.ToString());

            Assert.False(long.TryParse(token.Value, out _));
            Assert.Equal(10920L, long.Parse(token.ToUtf8String()));
        }

        [Fact]
        public void Should_Be_What_The_Implicit_String_Conversion_Yields() {
            // Which is why `AssetId.Decode(token)` compiles and is wrong too.
            CursorToken token = CursorToken.FromUtf8("order_42");
            string implicitly = token;

            Assert.Equal(token.Value, implicitly);
            Assert.NotEqual("order_42", implicitly);
        }
    }

    /// <summary>
    /// The shape a keyset endpoint with a strongly-typed key actually writes.
    /// </summary>
    public sealed class TheDecoderItReplaces {
        private readonly record struct AssetId(long Value) {
            public string Encode() => $"ast_{this.Value}";
            public static AssetId Decode(string s) => new(long.Parse(s.AsSpan(4)));
        }

        [Fact]
        public void Should_Round_Trip_In_One_Call() {
            AssetId id = new(42);

            CursorToken token = CursorToken.FromUtf8(id.Encode());
            AssetId decoded = AssetId.Decode(token.ToUtf8String());

            Assert.Equal(id, decoded);
        }

        [Fact]
        public void Should_Agree_With_The_Allocation_Free_Path() {
            CursorToken token = CursorToken.FromUtf8("ast_42");

            Span<byte> buffer = stackalloc byte[token.GetDecodedLength()];
            token.TryDecode(buffer, out int written);

            Assert.Equal(Encoding.UTF8.GetString(buffer[..written]), token.ToUtf8String());
        }
    }
}
