using System.Text;
using Wiaoj.Primitives.Obfuscation;

namespace Wiaoj.Primitives.Tests.Unit.Obfuscation;
public sealed class OpaqueIdParsingTests {
    public OpaqueIdParsingTests() {
        OpaqueId.Configure(new FeistelBase62Obfuscator(new() { Seed = "1234567890123456"u8.ToArray() }));
    }
     

    [Fact]
    public void Parse_Utf8_Bytes_Should_Be_Zero_Alloc_And_Correct() {
        OpaqueId original = new(555666777L);
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(original.ToString());

        OpaqueId decoded = OpaqueId.Parse(utf8Bytes);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void TryParse_Zero_String_Should_Return_Empty() {
        Assert.True(OpaqueId.TryParse("0", out var result));
        Assert.Equal(OpaqueId.Empty, result);
    }

    [Fact]
    public void TryFormat_Should_Fill_Span_Directly() {
        OpaqueId pid = new(Guid.NewGuid());
        Span<char> buffer = stackalloc char[64];

        Assert.True(pid.TryFormat(buffer, out int written, default, null));
        Assert.Equal(pid, OpaqueId.Parse(buffer[..written]));
    }

    [Theory]
    [InlineData("ThisStringIsWayTooLongToBeValid")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("abc!def")]
    public void Parse_Invalid_Format_Should_Throw_FormatException(string invalidInput) {
        Assert.Throws<FormatException>(() => OpaqueId.Parse(invalidInput));
    }
}