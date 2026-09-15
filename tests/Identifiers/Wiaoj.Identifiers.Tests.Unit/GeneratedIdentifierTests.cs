using System.ComponentModel;
using System.Text.Json;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers.Tests.Unit;

/// <summary>
/// The members generated for an identifier write and read through the current codec, and plug into parsing, formatting,
/// JSON and type conversion.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "GeneratedIdentifier")]
public sealed class GeneratedIdentifierTests {
    private static IDisposable Plain() => IdCodec.Override(PlainIdCodec.Instance);

    private static IDisposable Aes() => IdCodec.Override(TestCodecs.Aes());

    public sealed class Basics {
        [Fact]
        public void Should_Expose_The_Prefix_Through_The_Type_And_The_Interface() {
            Assert.Equal("usr", UserId.Prefix);
            Assert.Equal("api_key", ApiKeyId.Prefix);
            Assert.Equal("org", PrefixOf<OrgId>());
        }

        private static string PrefixOf<TId>() where TId : struct, IIdentifier<TId> => TId.Prefix;

        [Fact]
        public void Should_Create_From_A_Value_And_New_Snowflakes() {
            SnowflakeId value = new(12345);

            Assert.Equal(value, UserId.From(value).Value);
            Assert.Equal(value, Create<UserId>(value).Value);
            Assert.NotEqual(UserId.New(), UserId.New());
        }

        private static TId Create<TId>(SnowflakeId value) where TId : struct, IIdentifier<TId> => TId.From(value);

        [Fact]
        public void Should_Treat_The_Zero_Value_As_Empty() {
            Assert.True(UserId.Empty.IsEmpty);
            Assert.True(default(UserId).IsEmpty);
            Assert.False(UserId.From(new(1)).IsEmpty);
        }

        [Fact]
        public void Should_Compare_By_Value() {
            UserId small = UserId.From(new(1));
            UserId large = UserId.From(new(2));

            Assert.True(small < large);
            Assert.True(small <= large);
            Assert.True(large > small);
            Assert.True(large >= small);
            Assert.True(small <= UserId.From(new(1)));
            Assert.Equal(-1, small.CompareTo(large));
            Assert.Equal(1, small.CompareTo((object?)null));
            Assert.Equal(0, ((IComparable)small).CompareTo(UserId.From(new(1))));
            Assert.Throws<ArgumentException>(() => small.CompareTo((object)OrgId.From(new(1))));
            Assert.Equal(UserId.From(new(1)), small);
        }
    }

    public sealed class Text {
        [Fact]
        public void Should_Write_And_Read_Through_The_Current_Codec() {
            using(Plain()) {
                UserId id = UserId.From(new(61));

                Assert.Equal("usr_z", id.ToString());
                Assert.Equal("usr_z", $"{id}");
                Assert.Equal(id, UserId.Parse("usr_z"));
            }

            using(Aes()) {
                UserId id = UserId.From(new(61));
                string text = id.ToString();

                Assert.StartsWith("usr_1", text, StringComparison.Ordinal);
                Assert.Equal(id, UserId.Parse(text));
                Assert.False(UserId.TryParse("usr_z", out _));
            }
        }

        [Fact]
        public void Should_Parse_Strings_Spans_And_Utf8() {
            using IDisposable scope = Aes();
            UserId id = UserId.New();
            string text = id.ToString();
            byte[] utf8 = System.Text.Encoding.ASCII.GetBytes(text);

            Assert.Equal(id, UserId.Parse(text));
            Assert.Equal(id, UserId.Parse(text.AsSpan()));
            Assert.Equal(id, UserId.Parse(utf8));
            Assert.True(UserId.TryParse(text, out UserId a) && a == id);
            Assert.True(UserId.TryParse(text.AsSpan(), out UserId b) && b == id);
            Assert.True(UserId.TryParse(utf8, out UserId c) && c == id);
        }

        [Fact]
        public void Should_Work_Through_The_Generic_Parsing_Interfaces() {
            using IDisposable scope = Plain();

            Assert.Equal(UserId.From(new(61)), ParseAs<UserId>("usr_z"));
            Assert.Equal(UserId.From(new(61)), SpanParseAs<UserId>("usr_z"));
            Assert.Equal(UserId.From(new(61)), Utf8ParseAs<UserId>("usr_z"u8));
            Assert.False(TryParseAs<UserId>("org_z"));
        }

        private static T ParseAs<T>(string s) where T : IParsable<T> => T.Parse(s, null);

        private static T SpanParseAs<T>(string s) where T : ISpanParsable<T> => T.Parse(s.AsSpan(), null);

        private static T Utf8ParseAs<T>(ReadOnlySpan<byte> s) where T : IUtf8SpanParsable<T> => T.Parse(s, null);

        private static bool TryParseAs<T>(string s) where T : IParsable<T> => T.TryParse(s, null, out _);

        [Theory]
        [InlineData("org_z")]
        [InlineData("usr_")]
        [InlineData("usr_01")]
        [InlineData("")]
        public void Should_Refuse_Invalid_Text_Without_Echoing_It(string text) {
            using IDisposable scope = Plain();

            Assert.False(UserId.TryParse(text, out UserId result));
            Assert.Equal(default, result);

            FormatException error = Assert.Throws<FormatException>(() => UserId.Parse(text));
            Assert.Equal("The text is not a valid UserId.", error.Message);
        }

        [Fact]
        public void Should_Refuse_Null() {
            using IDisposable scope = Plain();

            Assert.False(UserId.TryParse((string?)null, out _));
            Assert.Throws<ArgumentNullException>(() => UserId.Parse((string)null!));
        }

        [Fact]
        public void Should_Not_Read_One_Identifier_Type_As_Another() {
            using IDisposable scope = Aes();
            string user = UserId.New().ToString();

            Assert.False(OrgId.TryParse(user, out _));
            Assert.False(OrgId.TryParse("org" + user["usr".Length..], out _));
        }

        [Fact]
        public void Should_Format_Into_Char_And_Utf8_Buffers() {
            using IDisposable scope = Plain();
            UserId id = UserId.From(new(3843));

            Span<char> chars = stackalloc char[16];
            Assert.True(id.TryFormat(chars, out int charsWritten, default, null));
            Assert.Equal("usr_zz", chars[..charsWritten].ToString());

            Span<byte> bytes = stackalloc byte[16];
            Assert.True(id.TryFormat(bytes, out int bytesWritten, default, null));
            Assert.True(bytes[..bytesWritten].SequenceEqual("usr_zz"u8));

            Assert.False(id.TryFormat(stackalloc char[5], out charsWritten, default, null));
            Assert.Equal(0, charsWritten);
            Assert.False(id.TryFormat(stackalloc byte[5], out bytesWritten, default, null));
            Assert.Equal(0, bytesWritten);

            Assert.Equal("usr_zz", id.ToString("anything", null));
        }
    }

    public sealed class Json {
        private sealed record Order(UserId Customer, OrgId? Seller, Dictionary<UserId, int> Quantities);

        [Fact]
        public void Should_Write_A_Json_String_And_Read_It_Back() {
            using IDisposable scope = Aes();
            UserId customer = UserId.New();
            OrgId seller = OrgId.New();
            Order order = new(customer, seller, new() { [customer] = 3 });

            string json = JsonSerializer.Serialize(order);
            Order back = JsonSerializer.Deserialize<Order>(json)!;

            Assert.Contains($"\"Customer\":\"{customer}\"", json, StringComparison.Ordinal);
            Assert.Contains($"\"{customer}\":3", json, StringComparison.Ordinal);
            Assert.Equal(customer, back.Customer);
            Assert.Equal(seller, back.Seller);
            Assert.Equal(3, back.Quantities[customer]);
        }

        [Fact]
        public void Should_Read_A_Null_Optional_Identifier() {
            using IDisposable scope = Plain();

            Order back = JsonSerializer.Deserialize<Order>("""{"Customer":"usr_z","Seller":null,"Quantities":{}}""")!;

            Assert.Null(back.Seller);
        }

        [Fact]
        public void Should_Read_An_Escaped_String() {
            using IDisposable scope = Plain();

            Assert.Equal(UserId.From(new(61)), JsonSerializer.Deserialize<UserId>("\"\\u0075sr_z\""));
        }

        [Theory]
        [InlineData("61")]
        [InlineData("true")]
        [InlineData("{}")]
        [InlineData("\"org_z\"")]
        [InlineData("\"usr_01\"")]
        public void Should_Refuse_Anything_But_A_Valid_String_With_A_Json_Exception(string json) {
            using IDisposable scope = Plain();

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UserId>(json));
        }

        [Fact]
        public void Should_Say_That_A_Json_String_Is_Expected() {
            using IDisposable scope = Plain();

            JsonException error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UserId>("61"));
            Assert.Contains("must be a JSON string", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Refuse_An_Invalid_Dictionary_Key() {
            using IDisposable scope = Plain();

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<UserId, int>>("""{"org_z":1}"""));
        }
    }

    public sealed class TypeConversion {
        [Fact]
        public void Should_Convert_To_And_From_Its_Text() {
            using IDisposable scope = Plain();
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(UserId));

            Assert.True(converter.CanConvertFrom(typeof(string)));
            Assert.True(converter.CanConvertTo(typeof(string)));
            Assert.Equal(UserId.From(new(61)), converter.ConvertFromInvariantString("usr_z"));
            Assert.Equal("usr_z", converter.ConvertToInvariantString(UserId.From(new(61))));
            Assert.False(converter.CanConvertFrom(typeof(int)));
            Assert.Throws<FormatException>(() => converter.ConvertFromInvariantString("org_z"));
        }
    }
}
