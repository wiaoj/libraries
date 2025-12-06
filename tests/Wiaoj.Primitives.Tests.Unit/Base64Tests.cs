using System;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Primitives.Tests.Unit;
public class Base64Tests {
    [Fact]
    public void Should_Create_From_Text_Helpers() {
        // 1. FromUtf8 Testi
        var b1 = Base64String.FromUtf8("Hello");
        Assert.Equal("SGVsbG8=", b1.Value);

        // 2. From (Encoding) Testi
        var b2 = Base64String.From("Hello", Encoding.ASCII);
        Assert.Equal("SGVsbG8=", b2.Value);

        // 3. Boş string testi
        Assert.Equal(Base64String.Empty, Base64String.FromUtf8(""));
    }
}