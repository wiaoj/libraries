namespace Wiaoj.Primitives.Snowflake;
/// <summary>
/// Configuration options for the Snowflake generator.
/// </summary>
public record SnowflakeOptions {
    /// <summary>
    /// Gets or sets the custom epoch (start date) for the ID generation.
    /// Defaults to January 1, 2024.
    /// </summary>
    public DateTimeOffset Epoch { get; set; } = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Gets or sets the unique ID of the node (machine/pod) generating the IDs.
    /// Range depends on <see cref="Snowflake.SequenceBits"/>. 
    /// </summary>
    public ushort NodeId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of bits reserved for the sequence.
    /// Defaults to 12 bits (4096 IDs per millisecond).
    /// </summary>
    public SequenceBits SequenceBits { get; set; } = SequenceBits.Default;

    /// <summary>
    /// Gets or sets the maximum allowed drift in milliseconds.
    /// <para>
    /// If the generator produces IDs faster than the system clock (Burst), 
    /// or if the system clock rolls back, the generator can logically advance its internal time
    /// ahead of the system time up to this limit.
    /// </para>
    /// Defaults to 2000ms (2 seconds).
    /// </summary>
    public long MaxDriftMs { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the time provider used to retrieve the current timestamp.
    /// Defaults to <see cref="TimeProvider.System"/>.
    /// Useful for testing clock drift and sequence overflow deterministically.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}