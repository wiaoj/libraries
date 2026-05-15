namespace Wiaoj.BloomFilter;

/// <summary>
/// Exception thrown when data integrity issues are detected during Bloom Filter operations, 
/// such as checksum mismatches or invalid header data.
/// </summary>
public class DataIntegrityException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="DataIntegrityException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DataIntegrityException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataIntegrityException"/> class with a specified error message 
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public DataIntegrityException(string message, Exception inner) : base(message, inner) { }
}