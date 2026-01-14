using Microsoft.IO;
using System.IO.Compression;
using Wiaoj.Serialization.Compression.Abstractions;

namespace Wiaoj.Serialization.Compression.Compressors;
/// <summary>
/// An implementation of <see cref="ICompressor"/> using the GZIP algorithm.
/// Provides good compression with universal compatibility.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GzipCompressor"/>.
/// </remarks>
/// <param name="compressionLevel">The desired trade-off between compression speed and size.</param>
/// <param name="streamManager"></param>
internal sealed class GzipCompressor(CompressionLevel compressionLevel, RecyclableMemoryStreamManager streamManager) : ICompressor {
    /// <inheritdoc />
    public byte[] Compress(ReadOnlySpan<byte> plainBytes) {
        using MemoryStream outputStream = streamManager.GetStream();
        using(GZipStream gzipStream = new(outputStream, compressionLevel, leaveOpen: true)) {
            gzipStream.Write(plainBytes);
        }
        return outputStream.ToArray();
    }

    /// <inheritdoc />
    public byte[] Decompress(ReadOnlySpan<byte> compressedData) {
        using MemoryStream inputStream = streamManager.GetStream(compressedData);
        using MemoryStream outputStream = streamManager.GetStream();

        using(GZipStream gzipStream = new(inputStream, CompressionMode.Decompress)) {
            gzipStream.CopyTo(outputStream);
        }

        return outputStream.ToArray();
    }

    /// <inheritdoc />
    public Stream CreateCompressionStream(Stream targetStream) {
        return new GZipStream(targetStream, compressionLevel, leaveOpen: true);
    }

    /// <inheritdoc />
    public Stream CreateDecompressionStream(Stream sourceStream) {
        return new GZipStream(sourceStream, CompressionMode.Decompress, leaveOpen: true);
    }
}