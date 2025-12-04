namespace Wiaoj.Serialization.Compression.Exceptions;
/// <summary>
/// Thrown when a decompression operation fails. This typically indicates that the
/// input data is corrupted, not in the expected compression format (e.g., trying to decompress Gzip data with Brotli),
/// or was compressed using an incompatible algorithm version.
/// </summary>
public class DecompressionFailedException : WiaojCompressionException {
    /// <summary>
    /// Initializes a new instance of the <see cref="DecompressionFailedException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DecompressionFailedException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecompressionFailedException"/> class
    /// with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DecompressionFailedException(string message, Exception innerException) : base(message, innerException) { }
}