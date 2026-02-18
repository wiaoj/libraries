namespace Wiaoj.Primitives;
/// <summary>
/// Provides an entry point for fluent extension methods designed to generate sensitive data 
/// according to specific cryptographic standards (e.g., AES, RSA, Secure Salts).
/// </summary>
/// <remarks>
/// The primary purpose of this class is to provide a clean and discoverable syntax, 
/// such as <c>Secret.Factory.Aes256Key()</c>, without cluttering the main static API 
/// of the <see cref="Secret"/> class with too many standard-specific methods.
/// </remarks>
public sealed class SecretFactory {
    /// <summary>
    /// Prevents external instantiation. 
    /// Access should be performed through <see cref="Secret{any}.Factory"/>.
    /// </summary>
    internal SecretFactory() { }
}