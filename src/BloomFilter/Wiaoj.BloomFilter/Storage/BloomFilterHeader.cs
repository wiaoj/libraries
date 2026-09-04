using System.Buffers.Binary;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.BloomFilter;

/// <summary>
/// Manages the binary header protocol for Bloom Filter storage files.
/// Format: [MagicBytes(4)] [Version(4)] [Checksum(8)]
/// </summary>
public static class BloomFilterHeader {
    /// <summary>
    /// Magic bytes "WBF1" (Wiaoj Bloom Filter v1) to identify the file format.
    /// </summary>
    public static ReadOnlySpan<byte> Magic => "WBF1"u8;

    /// <summary>
    /// The fixed size of the header in bytes.
    /// Magic(4) + Version(4) + Checksum(8) + SizeInBits(8) + HashCount(4) + Fingerprint(8)  = 36 Byte
    /// </summary>
    public const int HeaderSize = 4 + 4 + 8 + 8 + 4 + 8;

    /// <summary>
    /// The current protocol version of the Bloom Filter header.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// Computes a unique 64-bit structural fingerprint for the given configuration using XxHash3.
    /// </summary>
    public static ulong ComputeFingerprint(BloomFilterConfiguration config) {
        Span<byte> buffer = stackalloc byte[28];
        BinaryPrimitives.WriteInt64LittleEndian(buffer[0..8], config.SizeInBits);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..12], config.HashFunctionCount);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[12..16], Version);
        BinaryPrimitives.WriteInt64LittleEndian(buffer[16..24], config.HashSeed);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[24..28], config.ShardCount);

        return XxHash3.Compute(buffer).Value;
    }


    /// <summary>
    /// Writes the standard header to the specified stream using a specific encoding.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="checksum">The data checksum.</param>
    /// <param name="config">The filter configuration.</param>
    public static void WriteHeader(Stream stream, ulong checksum, BloomFilterConfiguration config) {
        Span<byte> header = stackalloc byte[HeaderSize];

        Magic.CopyTo(header[0..4]);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..8], Version);
        BinaryPrimitives.WriteUInt64LittleEndian(header[8..16], checksum);
        BinaryPrimitives.WriteInt64LittleEndian(header[16..24], config.SizeInBits);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..28], config.HashFunctionCount);
        BinaryPrimitives.WriteUInt64LittleEndian(header[28..36], ComputeFingerprint(config));

        stream.Write(header);
    }

    /// <summary>
    /// Attempts to read and validate the header from the stream using a specific encoding.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="checksum">The read checksum.</param>
    /// <param name="sizeInBits">The read bit size.</param>
    /// <param name="hashCount">The read hash function count.</param>
    /// <param name="fingerprint">The read configuration fingerprint.</param>
    /// <returns><c>true</c> if the header was read successfully; otherwise, <c>false</c>.</returns>
    public static bool TryReadHeader(Stream stream,
                                     out ulong checksum,
                                     out long sizeInBits,
                                     out int hashCount,
                                     out ulong fingerprint) {
        checksum = 0;
        sizeInBits = 0;
        hashCount = 0;
        fingerprint = 0;

        Span<byte> header = stackalloc byte[HeaderSize];
        int bytesRead = stream.ReadAtLeast(header, HeaderSize, throwOnEndOfStream: false);

        if(bytesRead < HeaderSize) {
            return false;
        }

        if(!header[0..4].SequenceEqual(Magic)) {
            return false;
        }

        int version = BinaryPrimitives.ReadInt32LittleEndian(header[4..8]);
        if(version != Version) {
            return false;
        }

        checksum = BinaryPrimitives.ReadUInt64LittleEndian(header[8..16]);
        sizeInBits = BinaryPrimitives.ReadInt64LittleEndian(header[16..24]);
        hashCount = BinaryPrimitives.ReadInt32LittleEndian(header[24..28]);
        fingerprint = BinaryPrimitives.ReadUInt64LittleEndian(header[28..36]);

        return true;
    }
}

/// <summary>
/// Data transfer object representing the header information of a Bloom Filter.
/// </summary>
/// <param name="Checksum">The data checksum.</param>
/// <param name="SizeInBits">The total bit size.</param>
/// <param name="HashCount">The hash function count.</param>
/// <param name="Fingerprint">The configuration fingerprint.</param>
/// <param name="CreatedAt">The creation timestamp.</param>
public sealed record BloomFilterHeaderDto(
    ulong Checksum,
    long SizeInBits,
    int HashCount,
    ulong Fingerprint,
    UnixTimestamp CreatedAt
);