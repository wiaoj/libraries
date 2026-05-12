#pragma warning disable IDE0130 // Namespace does not match folder structure
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Wiaoj.Primitives;
#pragma warning restore IDE0130 // Namespace does not match folder structure
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



/// <summary>
/// Provides high-level asynchronous and transformation extensions for <see cref="Secret{T}"/>.
/// </summary>
public static partial class SecretExtensions {
    extension<T>(Secret<T> secret) where T : unmanaged {
        /// <summary>
        /// Asynchronously executes an operation on the secret data.
        /// </summary>
        /// <remarks>
        /// The derived key material passes through a managed byte array that is
        /// securely zeroed immediately after use. During derivation it is briefly
        /// visible to the GC, which is an accepted trade-off of the extension-based API.
        /// </remarks>
        public async ValueTask ExposeAsync(Func<ReadOnlyMemory<T>, ValueTask> action) {
            await secret.ExposeAsync((buffer, ct) => action(buffer), default);
        }

        /// <summary>
        /// Asynchronously executes an operation on the secret data with a cancellation token.
        /// </summary>
        /// <remarks>
        /// The derived key material passes through a managed byte array that is
        /// securely zeroed immediately after use. During derivation it is briefly
        /// visible to the GC, which is an accepted trade-off of the extension-based API.
        /// </remarks>
        public async ValueTask ExposeAsync(Func<ReadOnlyMemory<T>, CancellationToken, ValueTask> action,
                                           CancellationToken cancellationToken) {
            T[] buffer = secret.Expose(static span => span.ToArray());
            try {
                await action(buffer, cancellationToken);
            }
            finally {
                CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(buffer.AsSpan()));
            }
        }

        /// <summary>
        /// Asynchronously executes a function that returns a result based on the secret data.
        /// </summary>
        /// <remarks>
        /// The derived key material passes through a managed byte array that is
        /// securely zeroed immediately after use. During derivation it is briefly
        /// visible to the GC, which is an accepted trade-off of the extension-based API.
        /// </remarks>
        public async ValueTask<TResult> ExposeAsync<TResult>(Func<ReadOnlyMemory<T>, ValueTask<TResult>> func) {
            return await secret.ExposeAsync((buffer, ct) => func(buffer), default);
        }

        /// <summary>
        /// Asynchronously executes a function that returns a result based on the secret data with a cancellation token.
        /// </summary>
        /// <remarks>
        /// The derived key material passes through a managed byte array that is
        /// securely zeroed immediately after use. During derivation it is briefly
        /// visible to the GC, which is an accepted trade-off of the extension-based API.
        /// </remarks>
        public async ValueTask<TResult> ExposeAsync<TResult>(Func<ReadOnlyMemory<T>, CancellationToken, ValueTask<TResult>> func,
                                                             CancellationToken cancellationToken) {
            T[] buffer = secret.Expose(static span => span.ToArray());
            try {
                return await func(buffer, cancellationToken);
            }
            finally {
                CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(buffer.AsSpan()));
            }
        }

        // ── Pipeline / Fluent extensions ──────────────────────────────────────

        /// <summary>
        /// Pipes the secret into a transformation function. 
        /// Useful for chaining cryptographic operations like DeriveKey.
        /// </summary>
        public TResult Pipe<TResult>(Func<Secret<T>, TResult> pipe) {
            return pipe(secret);
        }
    }

    extension(Secret<byte> secret) {
        /// <summary>
        /// Derives a new key from this secret using HKDF-SHA256.
        /// This is a convenience overload that accepts a <see cref="Secret{T}"/> as the salt.
        /// </summary>
        public Secret<byte> DeriveKey(in Secret<byte> salt, int outputByteCount) {
            return salt.Expose(
                (secret, outputByteCount),
                static (state, saltSpan) => state.secret.DeriveKeyFromSpan(saltSpan, state.outputByteCount));
        }

        /// <summary>
        /// Derives a new key from this secret using HKDF-SHA256 and a span-based salt.
        /// </summary>
        public Secret<byte> DeriveKeyFromSpan(ReadOnlySpan<byte> salt, int outputByteCount) {
            Preca.ThrowIfNegativeOrZero(outputByteCount);

            byte[] derived = new byte[outputByteCount];
            try {
                secret.Expose(salt, (saltSpan, ikmSpan) =>
                    HKDF.DeriveKey(HashAlgorithmName.SHA256, ikmSpan, derived.AsSpan(), saltSpan, null));

                return Secret<byte>.From(derived);
            }
            finally {
                CryptographicOperations.ZeroMemory(derived);
            }
        }
    }
}