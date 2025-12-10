using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents a high-performance, immutable value type that conforms to the Semantic Versioning 2.0.0 specification.
/// </summary>
/// <remarks>
/// This struct alleviates the performance and feature drawbacks of using <see cref="string"/> or <see cref="System.Version"/>.
/// It implements <see cref="ISpanParsable{TSelf}"/> and <see cref="ISpanFormattable"/> for zero-ish GC pressure
/// and supports the full precedence and comparison rules of the SemVer 2.0.0 standard.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]   
public readonly record struct SemVer :
    IComparable<SemVer>,
    IComparable,
    ISpanParsable<SemVer>,
    ISpanFormattable,
    IComparisonOperators<SemVer, SemVer, bool>,
    IMinMaxValue<SemVer> {

    #region Properties and Constants

    /// <summary>
    /// Gets the major version number, incremented for incompatible API changes.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the minor version number, incremented for new, backward-compatible functionality.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Gets the patch version number, incremented for backward-compatible bug fixes.
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// Gets the pre-release identifier (e.g., "alpha.1").
    /// An empty string indicates that this is a stable release.
    /// </summary>
    public string PreRelease { get; }

    /// <summary>
    /// Gets the build metadata (e.g., "build.20240101").
    /// This information is ignored during precedence comparison.
    /// </summary>
    public string BuildMetadata { get; }

    /// <summary>
    /// Represents the smallest possible version value (0.0.0).
    /// </summary>
    public static SemVer MinVersion { get; } = new(0, 0, 0, string.Empty, string.Empty);

    /// <inheritdoc/>
    static SemVer IMinMaxValue<SemVer>.MinValue => MinVersion;

    /// <inheritdoc/>
    static SemVer IMinMaxValue<SemVer>.MaxValue => throw new NotSupportedException("Semantic versioning has no theoretical upper bound.");

    /// <summary>
    /// Gets a value indicating whether this version is a pre-release.
    /// </summary>
    public bool IsPreRelease => !string.IsNullOrEmpty(this.PreRelease);

    #endregion

    #region Constructors and Factories

    /// <summary>
    /// Initializes a new instance of the <see cref="SemVer"/> struct for a stable release.
    /// </summary>
    public SemVer(int major, int minor, int patch)
        : this(major, minor, patch, string.Empty, string.Empty) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SemVer"/> struct for a pre-release version.
    /// </summary>
    public SemVer(int major, int minor, int patch, string preRelease)
        : this(major, minor, patch, preRelease, string.Empty) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SemVer"/> struct with all components.
    /// </summary>
    public SemVer(int major, int minor, int patch, string preRelease, string buildMetadata) {
        if (major < 0) {
            throw new ArgumentOutOfRangeException(nameof(major), "Major version cannot be negative.");
        }

        if (minor < 0) {
            throw new ArgumentOutOfRangeException(nameof(minor), "Minor version cannot be negative.");
        }

        if (patch < 0) {
            throw new ArgumentOutOfRangeException(nameof(patch), "Patch version cannot be negative.");
        }

        this.Major = major;
        this.Minor = minor;
        this.Patch = patch;
        this.PreRelease = preRelease ?? string.Empty;
        this.BuildMetadata = buildMetadata ?? string.Empty;
    }

    /// <summary>
    /// Creates a <see cref="SemVer"/> instance from a <see cref="System.Version"/> object.
    /// </summary>
    public static SemVer FromVersion(Version version) {
        ArgumentNullException.ThrowIfNull(version);
        string metadata = version.Revision > -1 ? $"rev.{version.Revision}" : string.Empty;
        return new SemVer(version.Major, version.Minor, version.Build, string.Empty, metadata);
    }

    /// <summary>
    /// Explicitly converts a <see cref="System.Version"/> to a <see cref="SemVer"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SemVer(Version version) {
        return FromVersion(version);
    }

    #endregion

    #region Parsing (Public & Explicit Interface Implementation)

    // --- Public API (Clean, No IFormatProvider) ---

    /// <summary>
    /// Parses a string into a <see cref="SemVer"/>.
    /// </summary>
    public static SemVer Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a span of characters into a <see cref="SemVer"/>.
    /// </summary>
    public static SemVer Parse(ReadOnlySpan<char> s) {
        if (TryParseInternal(s, out SemVer result)) {
            return result;
        }
        throw new FormatException($"'{s}' is not a valid semantic version string.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="SemVer"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out SemVer result) {
        if (s is null) {
            result = default;
            return false;
        }
        return TryParseInternal(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a span of characters into a <see cref="SemVer"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out SemVer result) {
        return TryParseInternal(s, out result);
    }

    // --- Explicit Interface Implementation (Hidden from Public API) ---
    // These satisfy the interfaces but don't clutter IntelliSense.
    // We ignore the IFormatProvider because SemVer is culture-invariant.

    static SemVer IParsable<SemVer>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<SemVer>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SemVer result) {
        return TryParse(s, out result);
    }

    static SemVer ISpanParsable<SemVer>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<SemVer>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SemVer result) {
        return TryParse(s, out result);
    }

    // --- Internal Logic ---

    private static bool TryParseInternal(ReadOnlySpan<char> s, out SemVer result) {
        result = default;

        int plusIndex = s.IndexOf('+');
        ReadOnlySpan<char> buildMetadata = default;
        if (plusIndex != -1) {
            buildMetadata = s[(plusIndex + 1)..];
            if (buildMetadata.IsEmpty) {
                return false;
            }

            s = s[..plusIndex];
        }

        int hyphenIndex = s.IndexOf('-');
        ReadOnlySpan<char> preRelease = default;
        if (hyphenIndex != -1) {
            preRelease = s[(hyphenIndex + 1)..];
            if (preRelease.IsEmpty) {
                return false;
            }

            s = s[..hyphenIndex];
        }

        if (!TryParseInt(ref s, '.', out int major) ||
            !TryParseInt(ref s, '.', out int minor) ||
            !TryParseInt(ref s, char.MinValue, out int patch)) {
            return false;
        }

        if (!s.IsEmpty) {
            return false;
        }

        if (!AreIdentifiersValid(preRelease) || !AreIdentifiersValid(buildMetadata)) {
            return false;
        }

        result = new SemVer(major, minor, patch, preRelease.ToString(), buildMetadata.ToString());
        return true;

        static bool TryParseInt(ref ReadOnlySpan<char> span, char separator, out int value) {
            value = 0;
            int separatorIndex = separator == char.MinValue ? span.Length : span.IndexOf(separator);
            if (separatorIndex == -1) {
                return false;
            }

            ReadOnlySpan<char> part = span[..separatorIndex];
            span = separator == char.MinValue ? [] : span[(separatorIndex + 1)..];

            if (part.Length > 1 && part[0] == '0') {
                return false;
            }

            return int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        static bool AreIdentifiersValid(ReadOnlySpan<char> identifiers) {
            if (identifiers.IsEmpty) {
                return true;
            }

            SpanSplitEnumerator enumerator = new(identifiers, '.');
            foreach (ReadOnlySpan<char> identifier in enumerator) {
                if (identifier.IsEmpty) {
                    return false;
                }
            }
            return true;
        }
    }

    #endregion

    #region Comparison (IComparable, IComparisonOperators)

    /// <inheritdoc/>
    public int CompareTo(SemVer other) {
        int comparison = this.Major.CompareTo(other.Major);
        if (comparison != 0) {
            return comparison;
        }

        comparison = this.Minor.CompareTo(other.Minor);
        if (comparison != 0) {
            return comparison;
        }

        comparison = this.Patch.CompareTo(other.Patch);
        if (comparison != 0) {
            return comparison;
        }

        if (this.IsPreRelease && !other.IsPreRelease) {
            return -1;
        }

        if (!this.IsPreRelease && other.IsPreRelease) {
            return 1;
        }

        if (!this.IsPreRelease && !other.IsPreRelease) {
            return 0;
        }

        ReadOnlySpan<char> thisPreReleaseSpan = this.PreRelease.AsSpan();
        ReadOnlySpan<char> otherPreReleaseSpan = other.PreRelease.AsSpan();

        SpanSplitEnumerator thisEnumerator = new(thisPreReleaseSpan, '.');
        SpanSplitEnumerator otherEnumerator = new(otherPreReleaseSpan, '.');

        while (true) {
            bool thisHasNext = thisEnumerator.MoveNext();
            bool otherHasNext = otherEnumerator.MoveNext();

            if (!thisHasNext && !otherHasNext) {
                return 0; // Both finished, equal.
            }

            if (!thisHasNext) {
                return -1; // This one is shorter, so it has lower precedence.
            }

            if (!otherHasNext) {
                return 1;  // The other one is shorter, so this one has higher precedence.
            }

            ReadOnlySpan<char> thisId = thisEnumerator.Current;
            ReadOnlySpan<char> otherId = otherEnumerator.Current;

            bool thisIsNum = int.TryParse(thisId, NumberStyles.None, CultureInfo.InvariantCulture, out int thisNum);
            bool otherIsNum = int.TryParse(otherId, NumberStyles.None, CultureInfo.InvariantCulture, out int otherNum);

            if (thisIsNum && otherIsNum) {
                comparison = thisNum.CompareTo(otherNum);
                if (comparison != 0) {
                    return comparison;
                }
            }
            else {
                if (thisIsNum) {
                    return -1;
                }

                if (otherIsNum) {
                    return 1;
                }

                comparison = thisId.CompareTo(otherId, StringComparison.Ordinal);
                if (comparison != 0) {
                    return comparison;
                }
            }
        }
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if (obj is SemVer other) {
            return CompareTo(other);
        }

        return 1;
    }

    public static bool operator >(SemVer left, SemVer right) {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(SemVer left, SemVer right) {
        return left.CompareTo(right) >= 0;
    }

    public static bool operator <(SemVer left, SemVer right) {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(SemVer left, SemVer right) {
        return left.CompareTo(right) <= 0;
    }

    #endregion

    #region Formatting (ISpanFormattable)

    /// <inheritdoc/>
    /// <remarks>
    /// Supports: G (Default), f (Full), s (Stable), m (MajorMinor), M (Major).
    /// </remarks>
    public override string ToString() {
        return ToString("G");
    }

    /// <summary>
    /// Formats the SemVer instance to a string using the specified format.
    /// </summary>
    public string ToString(string? format) {
        // Mantığı buraya taşıdık.
        // Stackalloc ile allocation yapmadan küçük buffer deniyoruz.
        Span<char> buffer = stackalloc char[128];
        if (TryFormatInternal(buffer, out int charsWritten, format.AsSpan())) {
            return buffer[..charsWritten].ToString();
        }

        // Sığmazsa string.Create ile oluşturuyoruz.
        int requiredLength = GetRequiredLength(format.AsSpan());
        return string.Create(requiredLength, this, (span, state) => {
            state.TryFormatInternal(span, out _, format.AsSpan());
        });
    }

    // Explicit implementation to satisfy IFormattable
    // Bu metot artık yukarıdaki public metoda yönlendiriyor.
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return ToString(format);
    }

    // Explicit implementation to satisfy ISpanFormattable
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        // Provider ignored
        return TryFormatInternal(destination, out charsWritten, format);
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) {
        return TryFormatInternal(destination, out charsWritten, format);
    }

    // --- Internal Formatting Logic ---

    private bool TryFormatInternal(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) {
        if (format.IsEmpty) {
            format = "G";
        }

        if (format.Length != 1) {
            charsWritten = 0;
            return false;
        }

        int requiredLength = GetRequiredLength(format);
        if (destination.Length < requiredLength) {
            charsWritten = 0;
            return false;
        }

        charsWritten = 0;
        char specifier = format[0];

        if (specifier is 'G' or 'f' or 's' or 'm' or 'M') {
            this.Major.TryFormat(destination[charsWritten..], out int written);
            charsWritten += written;
        }
        if (specifier is 'G' or 'f' or 's' or 'm') {
            destination[charsWritten++] = '.';
            this.Minor.TryFormat(destination[charsWritten..], out int written);
            charsWritten += written;
        }
        if (specifier is 'G' or 'f' or 's') {
            destination[charsWritten++] = '.';
            this.Patch.TryFormat(destination[charsWritten..], out int written);
            charsWritten += written;
        }
        if (specifier is 'G' or 'f') {
            if (this.IsPreRelease) {
                destination[charsWritten++] = '-';
                this.PreRelease.AsSpan().CopyTo(destination[charsWritten..]);
                charsWritten += this.PreRelease.Length;
            }
        }
        if (specifier is 'G') {
            if (!string.IsNullOrEmpty(this.BuildMetadata)) {
                destination[charsWritten++] = '+';
                this.BuildMetadata.AsSpan().CopyTo(destination[charsWritten..]);
                charsWritten += this.BuildMetadata.Length;
            }
        }

        return specifier is 'G' or 'f' or 's' or 'm' or 'M';
    }

    private int GetRequiredLength(ReadOnlySpan<char> format) {
        char specifier = format.IsEmpty ? 'G' : format[0];

        int length = 0;
        if (specifier is 'G' or 'f' or 's' or 'm' or 'M') {
            length += GetDigitCount(this.Major);
        }

        if (specifier is 'G' or 'f' or 's' or 'm') {
            length += 1 + GetDigitCount(this.Minor);
        }

        if (specifier is 'G' or 'f' or 's') {
            length += 1 + GetDigitCount(this.Patch);
        }

        if (specifier is 'G' or 'f') {
            if (this.IsPreRelease) {
                length += 1 + this.PreRelease.Length;
            }
        }
        if (specifier is 'G') {
            if (!string.IsNullOrEmpty(this.BuildMetadata)) {
                length += 1 + this.BuildMetadata.Length;
            }
        }

        return length;

        static int GetDigitCount(int n) {
            if (n < 0) {
                return n.ToString().Length;
            }

            return n switch {
                < 10 => 1,
                < 100 => 2,
                < 1000 => 3,
                < 10000 => 4,
                < 100000 => 5,
                < 1000000 => 6,
                < 10000000 => 7,
                < 100000000 => 8,
                < 1000000000 => 9,
                _ => 10
            };
        }
    }

    #endregion

    // A zero-allocation enumerator for splitting a span.
    private ref struct SpanSplitEnumerator {
        private ReadOnlySpan<char> _span;
        private readonly char _separator;

        public SpanSplitEnumerator(ReadOnlySpan<char> span, char separator) {
            this._span = span;
            this._separator = separator;
            this.Current = default;
        }

        public ReadOnlySpan<char> Current { get; private set; }

        public bool MoveNext() {
            if (this._span.IsEmpty) {
                return false;
            }

            int separatorIndex = this._span.IndexOf(this._separator);
            if (separatorIndex == -1) {
                this.Current = this._span;
                this._span = [];
            }
            else {
                this.Current = this._span[..separatorIndex];
                this._span = this._span[(separatorIndex + 1)..];
            }
            return true;
        }

        public readonly SpanSplitEnumerator GetEnumerator() {
            return this;
        }
    }
}