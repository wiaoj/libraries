using System.Text;
using Wiaoj.BloomFilter.Extensions;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Internal;

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
    /// Writes the standard header to the specified stream.
    /// </summary>
    public static void WriteHeader(Stream stream, ulong checksum, BloomFilterConfiguration config) {
        WriteHeader(stream, checksum, config, Encoding.UTF8);
    }

    /// <summary>
    /// Writes the standard header to the specified stream using a specific encoding.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="checksum">The data checksum.</param>
    /// <param name="config">The filter configuration.</param>
    /// <param name="encoding">The encoding for the header (optional).</param>
    public static void WriteHeader(
        Stream stream,
        ulong checksum,
        BloomFilterConfiguration config,
        Encoding encoding) {
        encoding ??= Encoding.UTF8;

        // BinaryWriter'a encoding'i veriyoruz
        using BinaryWriter writer = new(stream, encoding, leaveOpen: true);

        writer.Write(Magic); // 4 bytes
        writer.Write(Version);     // Version: 1 (4 bytes)
        writer.Write(checksum); // 8 bytes
        writer.Write(config.SizeInBits); // Dosyanın kaç bit olduğunu içine yaz
        writer.Write(config.HashFunctionCount);  // Kaç hash fonksiyonu ile yazıldığını yaz 
        writer.Write(config.GetFingerprint());
    }

    /// <summary>
    /// Attempts to read and validate the header from the stream.
    /// </summary>
    public static bool TryReadHeader(Stream stream,
                                     out ulong checksum,
                                     out long sizeInBits,
                                     out int hashCount,
                                     out ulong fingerprint) {
        return TryReadHeader(stream, out checksum, out sizeInBits, out hashCount, out fingerprint, Encoding.UTF8);
    }

    /// <summary>
    /// Attempts to read and validate the header from the stream using a specific encoding.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="checksum">The read checksum.</param>
    /// <param name="sizeInBits">The read bit size.</param>
    /// <param name="hashCount">The read hash function count.</param>
    /// <param name="fingerprint">The read configuration fingerprint.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <returns><c>true</c> if the header was read successfully; otherwise, <c>false</c>.</returns>
    public static bool TryReadHeader(Stream stream,
                                     out ulong checksum,
                                     out long sizeInBits,
                                     out int hashCount,
                                     out ulong fingerprint,
                                     Encoding encoding) {
        encoding ??= Encoding.UTF8;
        checksum = 0; sizeInBits = 0; hashCount = 0; fingerprint = 0;

        if(stream.Length < HeaderSize) return false;

        using BinaryReader reader = new(stream, encoding, leaveOpen: true);
        byte[] magic = reader.ReadBytes(4);

        if(!magic.AsSpan().SequenceEqual(Magic)) return false;

        int version = reader.ReadInt32();
        if(version != 1) return false;

        checksum = reader.ReadUInt64();
        sizeInBits = reader.ReadInt64();
        hashCount = reader.ReadInt32();
        fingerprint = reader.ReadUInt64();
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