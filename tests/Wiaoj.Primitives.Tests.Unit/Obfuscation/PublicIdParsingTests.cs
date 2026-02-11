using System.Text;

namespace Wiaoj.Primitives.Tests.Unit.Obfuscation;
public sealed class PublicIdParsingTests {
    public PublicIdParsingTests() => PublicId.Configure("Parsing-Test-Seed");

    [Fact]
    public void Parse_Utf8_Bytes_Should_Be_Zero_Alloc_And_Correct() {
        PublicId original = new(555666777L);
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(original.ToString());

        PublicId decoded = PublicId.Parse(utf8Bytes);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void TryParse_Zero_String_Should_Return_Empty() {
        Assert.True(PublicId.TryParse("0", out var result));
        Assert.Equal(PublicId.Empty, result);
    }

    [Fact]
    public void TryFormat_Should_Fill_Span_Directly() {
        PublicId pid = new(Guid.NewGuid());
        Span<char> buffer = stackalloc char[64];

        Assert.True(pid.TryFormat(buffer, out int written, default, null));
        Assert.Equal(pid, PublicId.Parse(buffer[..written]));
    }

    [Theory]
    [InlineData("ThisStringIsWayTooLongToBeValid")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("abc!def")]
    public void Parse_Invalid_Format_Should_Throw_FormatException(string invalidInput) {
        Assert.Throws<FormatException>(() => PublicId.Parse(invalidInput));
    }
}