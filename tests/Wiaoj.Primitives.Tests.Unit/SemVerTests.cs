using System.Text;
using Wiaoj.Primitives;
using Xunit;

namespace Wiaoj.Primitives.Tests.Unit;

public sealed class SemVerTests {
    #region SemVer 2.0.0 Precedence Rules (Section 11)

    [Fact]
    public void Precedence_Rules_Compliance() {
        var versions = new[] {
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11", // Numeric comparison (2 < 11)
            "1.0.0-rc.1",
            "1.0.0",
            "2.0.0",
            "2.1.0",
            "2.1.1"
        };

        for(int i = 0; i < versions.Length - 1; i++) {
            var v1 = SemVer.Parse(versions[i]);
            var v2 = SemVer.Parse(versions[i + 1]);

            Assert.True(v1 < v2, $"{v1} should be less than {v2}");
            Assert.True(v2 > v1, $"{v2} should be greater than {v1}");
            Assert.True(v1.CompareTo(v2) < 0);
            Assert.True(v2.CompareTo(v1) > 0);
        }
    }

    [Fact]
    public void Numeric_PreRelease_Identifier_Has_Lower_Precedence_Than_Lexical() {
        // Spec Section 11: Numeric identifiers always have lower precedence than non-numeric identifiers
        var numeric = SemVer.Parse("1.0.0-1");
        var lexical = SemVer.Parse("1.0.0-alpha");

        Assert.True(numeric < lexical);
        Assert.True(numeric.CompareTo(lexical) < 0);
    }

    [Fact]
    public void Build_Metadata_Ignored_In_Precedence_Comparison() {
        // Spec Section 10: Build metadata does not figure in precedence
        var v1 = SemVer.Parse("1.0.0+build.1");
        var v2 = SemVer.Parse("1.0.0+build.2");

        // Object equality compares all fields including BuildMetadata
        Assert.NotEqual(v1, v2);

        // Precedence comparison treats them as equal
        Assert.Equal(0, v1.CompareTo(v2));
        Assert.False(v1 > v2);
        Assert.False(v1 < v2);
        Assert.True(v1 >= v2);
        Assert.True(v1 <= v2);
    }

    #endregion

    #region Spec Section 9 & 10 Validation (Leading Zeroes & Valid Characters)

    [Theory]
    [InlineData("1.0.0-01")]         // Leading zero in numeric pre-release identifier
    [InlineData("1.0.0-alpha.01")]   // Leading zero in dotted numeric pre-release
    [InlineData("01.1.0")]           // Leading zero in Major
    [InlineData("1.01.0")]           // Leading zero in Minor
    [InlineData("1.1.01")]           // Leading zero in Patch
    [InlineData("1.0.0-alpha_beta")] // Underscore in pre-release is invalid in SemVer 2.0.0
    [InlineData("1.0.0-alpha@1")]    // At symbol is invalid
    [InlineData("1.0.0-alpha..1")]   // Empty identifier in pre-release
    [InlineData("1.0.0+build..1")]   // Empty identifier in build metadata
    [InlineData("1.0.0+build_meta")] // Underscore in build metadata is invalid
    public void Parse_InvalidSemVerSpec_ThrowsFormatException(string invalidVersion) {
        Assert.Throws<FormatException>(() => SemVer.Parse(invalidVersion));
        Assert.False(SemVer.TryParse(invalidVersion, out _));
    }

    [Theory]
    [InlineData("1.0.0+01")]         // Leading zero in build metadata is allowed
    [InlineData("1.0.0+build.01")]   // Leading zero in dotted build metadata is allowed
    [InlineData("0.0.4")]
    [InlineData("1.2.3")]
    [InlineData("10.20.30")]
    [InlineData("1.1.2-prerelease+meta")]
    [InlineData("1.1.2+meta")]
    [InlineData("1.1.2+meta-valid")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-beta.11")]
    [InlineData("1.0.0-rc.1+build.1")]
    public void Parse_ValidSemVerSpec_Succeeds(string validVersion) {
        var parsed = SemVer.Parse(validVersion);
        Assert.Equal(validVersion, parsed.ToString());
    }

    #endregion

    #region Formatting & Exception Handling

    [Fact]
    public void ToString_FormatSpecifiers_ProduceExpectedOutput() {
        var semVer = SemVer.Parse("1.2.3-beta.1+sha.123");

        Assert.Equal("1.2.3-beta.1+sha.123", semVer.ToString("G"));
        Assert.Equal("1.2.3-beta.1", semVer.ToString("f"));
        Assert.Equal("1.2.3", semVer.ToString("s"));
        Assert.Equal("1.2", semVer.ToString("m"));
        Assert.Equal("1", semVer.ToString("M"));
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("X")]
    [InlineData("z")]
    [InlineData("123")]
    public void ToString_InvalidFormatSpecifier_ThrowsFormatException(string invalidFormat) {
        var semVer = SemVer.Parse("1.2.3");
        Assert.ThrowsAny<FormatException>(() => semVer.ToString(invalidFormat));
    }

    [Fact]
    public void TryFormat_DestinationTooSmall_ReturnsFalse() {
        var semVer = SemVer.Parse("10.20.30-beta.1+build.1");
        Span<char> smallBuffer = stackalloc char[5];

        bool result = semVer.TryFormat(smallBuffer, out int written);

        Assert.False(result);
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryFormat_Utf8_RoundTrip_Succeeds() {
        var semVer = SemVer.Parse("1.2.3-rc.1+build.99");
        Span<byte> utf8Buffer = stackalloc byte[64];

        bool formatted = semVer.TryFormat(utf8Buffer, out int bytesWritten);

        Assert.True(formatted);
        Assert.Equal("1.2.3-rc.1+build.99", Encoding.UTF8.GetString(utf8Buffer[..bytesWritten]));

        bool parsed = SemVer.TryParse(utf8Buffer[..bytesWritten], out SemVer parsedVersion);
        Assert.True(parsed);
        Assert.Equal(semVer, parsedVersion);
    }

    #endregion

    #region Alternate Comparer (.NET 10 Lookup)

    [Fact]
    public void AlternateLookup_AllowsSpanDictionaryLookupWithoutStringAllocation() {
        var dictionary = new Dictionary<SemVer, string>(SemVer.OrdinalComparer) {
            [SemVer.Parse("1.0.0")] = "Release 1.0",
            [SemVer.Parse("2.0.0-beta")] = "Release 2.0 Beta"
        };

        var lookup = dictionary.GetAlternateLookup<ReadOnlySpan<char>>();

        ReadOnlySpan<char> spanKey = "1.0.0".AsSpan();
        bool found = lookup.TryGetValue(spanKey, out string? value);

        Assert.True(found);
        Assert.Equal("Release 1.0", value);
    }

    #endregion
}