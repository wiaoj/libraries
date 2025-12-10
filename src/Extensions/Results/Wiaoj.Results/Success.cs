using System.Runtime.InteropServices;

namespace Wiaoj.Results;
/// <summary>
/// Represents a successful result of an operation that returns no value (void).
/// Optimized for high-performance scenarios as a substitute for System.Void in generics.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly record struct Success : IComparable<Success>, IComparable {
    /// <summary>
    /// Gets the default value of <see cref="Success"/>.
    /// Since this is a value type, this property returns a copy, equivalent to <c>new Success()</c>.
    /// </summary>
    public static Success Default { get; } = new();

    /// <inheritdoc/>
    public override string ToString() {
        return nameof(Success);
    }

    /// <inheritdoc/>
    public int CompareTo(Success other) {
        return 0;
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if (obj is null) {
            return 1;
        }

        if (obj is Success) {
            return 0;
        }

        throw new ArgumentException($"Object must be of type {nameof(Success)}");
    }
}