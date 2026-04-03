using Wiaoj.Preconditions.Exceptions;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.Extensions;
/// <summary>
/// Provides input/output (I/O) stream and file extension methods for cryptographic hashes.
/// </summary>
public static class HashExtensions {
    /// <summary>
    /// Asynchronously computes the SHA256 hash of a file on the disk without loading the entire file into memory.
    /// </summary>
    /// <param name="fileInfo">The file to be hashed.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask{Sha256Hash}"/> representing the asynchronous operation, yielding the computed hash.</returns>
    /// <exception cref="PrecaArgumentNullException">Thrown if <paramref name="fileInfo"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    public static async ValueTask<Sha256Hash> ComputeSha256Async(this FileInfo fileInfo, CancellationToken ct = default) {
        Preca.ThrowIfNull(fileInfo);
        Preca.ThrowIfFalse(fileInfo.Exists, static (fullName) => new FileNotFoundException("File not found.", fullName), fileInfo.FullName);

        await using FileStream fs = fileInfo.OpenRead();
        return await Sha256HashExtensions.ComputeAsync(fs, ct);
    }

    /// <summary>
    /// Asynchronously verifies if the computed SHA256 hash of the provided stream matches the expected hash.
    /// </summary>
    /// <param name="stream">The data stream to compute the hash from.</param>
    /// <param name="expectedHash">The previously known <see cref="Sha256Hash"/> to verify against.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask{Boolean}"/> yielding <see langword="true"/> if the hashes match; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> VerifySha256Async(this Stream stream, Sha256Hash expectedHash, CancellationToken ct = default) {
        Sha256Hash computed = await Sha256HashExtensions.ComputeAsync(stream, ct);
        return computed == expectedHash;
    }
}