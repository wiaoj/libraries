namespace Wiaoj.Primitives.Snowflake; 
/// <summary>
/// Defines the contract for a Snowflake ID generator.
/// Implementation can be a single-threaded generator or a striped/sharded one for high contention.
/// </summary>
public interface ISnowflakeGenerator {
    /// <summary>
    /// Generates a new, unique, and time-ordered 64-bit Snowflake ID.
    /// </summary>
    /// <returns>A unique <see cref="SnowflakeId"/>.</returns>
    SnowflakeId NextId();

    /// <summary>
    /// Extracts the <see cref="UnixTimestamp"/> (generation time) from an existing <see cref="SnowflakeId"/> 
    /// based on the generator's internal configuration (Epoch and Bit-shifts).
    /// </summary>
    /// <param name="id">The ID to decode.</param>
    /// <returns>The timestamp representing when the ID was created.</returns>
    UnixTimestamp ExtractUnixTimestamp(SnowflakeId id);

    /// <summary>
    /// Creates a "floor" <see cref="SnowflakeId"/> for a specific timestamp.
    /// The resulting ID has the timestamp part set, while the NodeId and Sequence parts are zero.
    /// Useful for database range queries (e.g., WHERE Id >= FromTimestamp).
    /// </summary>
    /// <param name="timestamp">The target timestamp.</param>
    /// <returns>A placeholder SnowflakeId used for temporal searching.</returns>
    SnowflakeId CreateIdFromTimestamp(UnixTimestamp timestamp);
}