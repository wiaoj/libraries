namespace Wiaoj.Results;

public readonly partial record struct Error {
    /// <summary>
    /// Creates a general Failure error using default code and description.
    /// </summary> 
    public static Error Failure() => Failure("General.Failure", "A failure has occurred.");

    /// <summary>
    /// Creates a general Failure error with specified code and description.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param> 
    public static Error Failure(string code, string description)
        => new(code, description, ErrorType.Failure);

    /// <summary>
    /// Creates a general Failure error with specified code, description, and metadata.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <param name="metadata">Optional metadata.</param> 
    public static Error Failure(string code,
                                string description,
                                IReadOnlyDictionary<string, object> metadata)
        => new(code, description, ErrorType.Failure, metadata);
}