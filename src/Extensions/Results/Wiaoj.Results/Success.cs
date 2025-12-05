namespace Wiaoj.Results;   
/// <summary>
/// Represents a successful result of an operation that does not return a value.
/// It serves as a type-safe equivalent of 'void' for use in generic constructs like Result&lt;Success&gt;.
/// </summary>
public readonly record struct Success {
    private static readonly Success _value = new();

    /// <summary>
    /// Gets the singleton instance of the <see cref="Success"/> type.
    /// </summary>
    public static ref readonly Success Default => ref _value;
}