namespace Wiaoj.Serialization.Compression.Abstractions;
/// <summary>
/// Defines the contract for a compression and decompression algorithm.
/// </summary>
public interface ICompressor {
    /// <summary>
    /// Compresses the provided raw data.
    /// </summary>
    byte[] Compress(ReadOnlySpan<byte> plainBytes);

    /// <summary>
    /// Decompresses the provided compressed data.
    /// </summary>
    byte[] Decompress(ReadOnlySpan<byte> compressedData); 
    
    Stream CreateCompressionStream(Stream targetStream);
    Stream CreateDecompressionStream(Stream sourceStream);
}