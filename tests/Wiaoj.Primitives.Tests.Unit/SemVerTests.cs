using Wiaoj.Primitives;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit;

public sealed class SemVerTests {
    [Fact]
    public void Precedence_Rules_Compliance() {
        // SemVer 2.0.0 Spec sıralama kuralları
        var versions = new[] {
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11", // Numeric sort (2 < 11)
            "1.0.0-rc.1",
            "1.0.0",
            "2.0.0",
            "2.1.0",
            "2.1.1"
        };

        for (int i = 0; i < versions.Length - 1; i++) {
            var v1 = SemVer.Parse(versions[i]);
            var v2 = SemVer.Parse(versions[i + 1]);

            Assert.True(v1 < v2, $"{v1} should be less than {v2}");
        }
    }

    [Fact]
    public void Build_Metadata_Should_Ignored_In_Comparison() {
        var v1 = SemVer.Parse("1.0.0+build.1");
        var v2 = SemVer.Parse("1.0.0+build.2");

        // 1. Nesne olarak farklılar (Doğru olan bu, metadata farklı)
        Assert.NotEqual(v1, v2);

        // 2. Ama SemVer kurallarına göre sıralamada eşdeğerler (Update gerekmez)
        Assert.Equal(0, v1.CompareTo(v2));

        // 3. Operatörler de sıralamaya bakar (İsteğe bağlı)
        Assert.False(v1 > v2);
        Assert.False(v1 < v2);
    }
}