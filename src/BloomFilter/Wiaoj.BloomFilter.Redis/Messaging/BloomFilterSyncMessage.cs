using System.Buffers.Binary;

namespace Wiaoj.BloomFilter.Redis.Messaging;

/// <summary>
/// Binary message published over Redis Pub/Sub to synchronize Bloom Filter additions across distributed peer nodes.
/// Fixed wire size: 32 bytes (16 bytes OriginNodeId + 8 bytes Hash1 + 8 bytes Hash2).
/// </summary>
public readonly record struct BloomFilterSyncMessage(Guid OriginNodeId, ulong Hash1, ulong Hash2) {
    /// <summary>
    /// The fixed binary wire size of the sync message in bytes.
    /// </summary>
    public const int WireSize = 32;

    /// <summary>
    /// Serializes this message into a 32-byte array.
    /// </summary>
    /// <returns>A 32-byte binary representation.</returns>
    public byte[] ToByteArray() {
        byte[] buffer = new byte[WireSize];
        WriteTo(buffer);
        return buffer;
    }

    /// <summary>
    /// Serializes this message into the destination byte span.
    /// </summary>
    /// <param name="destination">The destination span of at least 32 bytes.</param>
    public void WriteTo(Span<byte> destination) {
        if (destination.Length < WireSize) {
            throw new ArgumentException($"Destination buffer must be at least {WireSize} bytes.", nameof(destination));
        }

        OriginNodeId.TryWriteBytes(destination[..16]);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16, 8), Hash1);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(24, 8), Hash2);
    }

    /// <summary>
    /// Attempts to parse a 32-byte binary payload into a <see cref="BloomFilterSyncMessage"/>.
    /// </summary>
    /// <param name="source">The source binary data span.</param>
    /// <param name="message">The parsed sync message if successful; otherwise, default.</param>
    /// <returns><see langword="true"/> if successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> source, out BloomFilterSyncMessage message) {
        if (source.Length != WireSize) {
            message = default;
            return false;
        }

        Guid originNodeId = new(source[..16]);
        ulong h1 = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(16, 8));
        ulong h2 = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(24, 8));

        message = new BloomFilterSyncMessage(originNodeId, h1, h2);
        return true;
    }
}
