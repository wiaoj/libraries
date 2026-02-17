namespace Wiaoj.Primitives.Tests.Unit.UrnTests; 
public sealed class UrnPerformanceTests {
    [Fact]
    public void TryFormat_ShouldWriteToSpan() {
        var urn = Urn.Create("test", "123");
        Span<char> buffer = stackalloc char[32];

        bool success = urn.TryFormat(buffer, out int charsWritten, default, null);

        Assert.True(success);
        Assert.Equal(urn.Value.Length, charsWritten);
        Assert.Equal("urn:test:123", buffer.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TryFormat_BufferTooSmall_ShouldReturnFalse() {
        var urn = Urn.Create("test", "verylongidentifier");
        Span<char> buffer = stackalloc char[5]; // Çok küçük buffer

        bool success = urn.TryFormat(buffer, out int charsWritten, default, null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void Equality_And_HashCode_ShouldBeConsistent() {
        var u1 = Urn.Parse("urn:user:1");
        var u2 = Urn.Create("user", "1");
        var u3 = Urn.Create("user", "2");

        Assert.True(u1 == u2);
        Assert.True(u1.Equals(u2));
        Assert.Equal(u1.GetHashCode(), u2.GetHashCode());

        Assert.False(u1 == u3);
        Assert.NotEqual(u1.GetHashCode(), u3.GetHashCode());
    }
}