using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Wiaoj.Identifiers.Generators;

/// <summary>What the generator needs about one identifier — plain, equatable data, so unchanged inputs are not regenerated.</summary>
internal sealed record IdentifierTarget(
    string Namespace,
    string Name,
    string Accessibility,
    string Prefix,
    LocationInfo Location,
    EquatableArray<DiagnosticInfo> Diagnostics) {
    public string FullName => this.Namespace.Length == 0 ? this.Name : $"{this.Namespace}.{this.Name}";
}

internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan) {
    public static LocationInfo From(Location location) {
        return new LocationInfo(location.SourceTree?.FilePath ?? "", location.SourceSpan, location.GetLineSpan().Span);
    }

    public Location ToLocation() => Location.Create(this.FilePath, this.Span, this.LineSpan);
}

internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo Location, EquatableArray<string> Arguments) {
    public DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo location, params string[] arguments)
        : this(descriptor, location, new EquatableArray<string>(arguments)) {
    }

    public Diagnostic ToDiagnostic() => Diagnostic.Create(this.Descriptor, this.Location.ToLocation(), [.. this.Arguments]);
}

/// <summary>An immutable array compared by its elements.</summary>
internal readonly struct EquatableArray<T>(T[] items) : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T> {
    private readonly T[]? _items = items;

    public int Count => this._items?.Length ?? 0;

    public bool Equals(EquatableArray<T> other) => ((ReadOnlySpan<T>)(this._items ?? [])).SequenceEqual(other._items ?? []);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && this.Equals(other);

    public override int GetHashCode() {
        int hash = 17;
        foreach(T item in this._items ?? []) {
            hash = (hash * 31) + item.GetHashCode();
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(this._items ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}

internal static class Diagnostics {
    private const string Category = "Wiaoj.Identifiers";

    public static readonly DiagnosticDescriptor InvalidPrefix = new(
        "WIAOJID001",
        "Invalid identifier prefix",
        "The prefix of '{0}' is '{1}'; a prefix is 1-32 lowercase ASCII letters and digits, starting with a letter, optionally separated by single underscores",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WrongShape = new(
        "WIAOJID002",
        "Identifier must be a readonly partial record struct",
        "'{0}' must be declared as 'readonly partial record struct {0};' with no parameter list; the generator adds the value",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicatePrefix = new(
        "WIAOJID003",
        "Identifier prefix used more than once",
        "The prefix '{0}' is used by {1}; each identifier needs its own prefix, or text written for one would be read as the other",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NestedOrGeneric = new(
        "WIAOJID004",
        "Identifier must not be nested or generic",
        "'{0}' must be a non-generic type declared directly in a namespace",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
