namespace Wiaoj.Serialization.Security;
/// <summary>
/// The base exception for all security-related operations within the serialization pipeline,
/// such as encryption or decryption.
/// </summary>
public class WiaojSecurityException(string message, Exception innerException) : WiaojSerializationException(message, innerException);

/// <summary>
/// Thrown when a decryption operation fails. This can be due to an incorrect key,
/// corrupted data, or a failed integrity check (tampering).
/// </summary>
public class DecryptionFailedException(string message, Exception innerException) : WiaojSecurityException(message, innerException);

public class WiaojSecurityConfigurationException(string? message) : Exception(message) {
    public string? Path { get; }

    public WiaojSecurityConfigurationException(string? message, string path) : this(message) {
        this.Path = path;
    }

}