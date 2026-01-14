using Microsoft.IO;
using System.IO.Compression;
using Wiaoj.Serialization.Compression.Abstractions;

namespace Wiaoj.Serialization.Compression.Compressors;
/// <summary>
/// An implementation of <see cref="ICompressor"/> using the Brotli algorithm.
/// Offers superior compression ratios, especially for text-based payloads.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BrotliCompressor"/>.
/// </remarks>
/// <param name="compressionLevel">The desired trade-off between compression speed and size.</param>
/// <param name="streamManager"></param>
internal sealed class BrotliCompressor(CompressionLevel compressionLevel, RecyclableMemoryStreamManager streamManager) : ICompressor {
    /// <inheritdoc />
    public byte[] Compress(ReadOnlySpan<byte> plainBytes) {
        using MemoryStream outputStream = streamManager.GetStream();
        using(BrotliStream brotliStream = new(outputStream, compressionLevel, leaveOpen: true)) {
            brotliStream.Write(plainBytes);
        }
        return outputStream.ToArray();
    }

    /// <inheritdoc />
    public byte[] Decompress(ReadOnlySpan<byte> compressedData) {
        using MemoryStream inputStream = streamManager.GetStream(compressedData);
        using MemoryStream outputStream = streamManager.GetStream();
        using(BrotliStream brotliStream = new(inputStream, CompressionMode.Decompress)) {
            brotliStream.CopyTo(outputStream);
        }
        return outputStream.ToArray();
    }


    /// <inheritdoc />
    public Stream CreateCompressionStream(Stream targetStream) {
        return new BrotliStream(targetStream, compressionLevel, leaveOpen: true);
    }

    /// <inheritdoc />
    public Stream CreateDecompressionStream(Stream sourceStream) {
        return new BrotliStream(sourceStream, CompressionMode.Decompress, leaveOpen: true);
    }
}