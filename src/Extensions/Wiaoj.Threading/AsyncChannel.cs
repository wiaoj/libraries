using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Wiaoj.Concurrency;

namespace Wiaoj.Threading.Channels;

/// <summary>
/// Provides a high-level, feature-rich, and safe abstraction over <see cref="System.Threading.Channels.Channel{T}"/>
/// for implementing the producer-consumer pattern. This class separates read and write operations into distinct
/// reader and writer handles to enforce API safety and separation of concerns.
/// </summary>
/// <remarks>
/// This class enhances the base Channel functionality by providing:
/// <list type="bullet">
///   <item>
///     <description>Strongly-typed, separate <see cref="AsyncChannelWriter{T}"/> and <see cref="AsyncChannelReader{T}"/> handles.</description>
///   </item>
///   <item>
///     <description>Full configuration options for bounded channels, including backpressure strategies (e.g., DropOldest).</description>
///   </item>
///   <item>
///     <description>Implementation of <see cref="IAsyncDisposable"/> for deterministic resource management and completion.</description>
///   </item>
///   <item>
///     <description>A complete API surface, exposing methods like <c>TryRead</c> and <c>WaitToReadAsync</c>.</description>
///   </item>
/// </list>
/// </remarks>
/// <typeparam name="T">The type of data in the channel.</typeparam>
[DebuggerDisplay("Count = {Count}, IsCompleted = {IsCompleted}, Mode = {_mode}")]
public sealed class AsyncChannel<T> : IAsyncDisposable {
    private readonly Channel<T> _channel;
    private readonly string _mode;

    /// <summary>
    /// Gets the writer handle for the channel, allowing items to be produced.
    /// </summary>
    public AsyncChannelWriter<T> Writer { get; }

    /// <summary>
    /// Gets the reader handle for the channel, allowing items to be consumed.
    /// </summary>
    public AsyncChannelReader<T> Reader { get; }

    private AsyncChannel(Channel<T> channel, string mode) {
        this._channel = channel;
        this._mode = mode;
        this.Writer = new AsyncChannelWriter<T>(channel.Writer);
        this.Reader = new AsyncChannelReader<T>(channel.Reader);
    }

    #region Factory Methods

    /// <summary>
    /// Creates an unbounded channel, which can store an unlimited number of items.
    /// </summary>
    /// <param name="options">Options for configuring the unbounded channel's behavior.</param>
    /// <returns>A new unbounded <see cref="AsyncChannel{T}"/>.</returns>
    public static AsyncChannel<T> CreateUnbounded(UnboundedChannelOptions? options = null) {
        options ??= new UnboundedChannelOptions();
        Channel<T> channel = Channel.CreateUnbounded<T>(options);
        return new AsyncChannel<T>(channel, "Unbounded");
    }

    /// <summary>
    /// Creates a bounded channel with a specified capacity and behavior.
    /// </summary>
    /// <param name="capacity">The maximum number of items the channel can store.</param>
    /// <param name="fullMode">The behavior to exhibit when writing to a full channel. Defaults to <see cref="BoundedChannelFullMode.Wait"/>.</param>
    /// <returns>A new bounded <see cref="AsyncChannel{T}"/>.</returns>
    public static AsyncChannel<T> CreateBounded(int capacity, BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait) {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        BoundedChannelOptions options = new(capacity) {
            FullMode = fullMode,
            SingleReader = true, // Optimized for the common case of a single dedicated consumer.
            SingleWriter = false // Allows multiple producers by default.
        };

        return CreateBounded(options);
    }

    /// <summary>
    /// Creates a bounded channel with the specified options.
    /// </summary>
    /// <remarks>
    /// Use this factory method for fine-grained control over the channel's behavior, such as
    /// setting <see cref="BoundedChannelOptions"/>
    /// for performance-critical scenarios.
    /// </remarks>
    /// <param name="options">Options for configuring the bounded channel's behavior.</param>
    /// <returns>A new bounded <see cref="AsyncChannel{T}"/>.</returns>
    public static AsyncChannel<T> CreateBounded(BoundedChannelOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        Channel<T> channel = Channel.CreateBounded<T>(options);
        return new AsyncChannel<T>(channel, $"Bounded (Capacity: {options.Capacity}, Mode: {options.FullMode})");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a snapshot of the number of items currently in the channel.
    /// </summary>
    public int Count => this.Reader.Count;

    /// <summary>
    /// Gets a value indicating whether the channel has been marked as complete and is empty.
    /// </summary>
    public bool IsCompleted => this.Reader.Completion.IsCompleted;

    #endregion

    /// <summary>
    /// Marks the channel as complete, preventing further writes, and releases associated resources.
    /// This is typically used with <c>await using</c>.
    /// </summary>
    public async ValueTask DisposeAsync() {
        // Mark the writer as complete. This will unblock any readers
        // after they have drained the channel.
        this.Writer.TryComplete();

        // It's good practice to wait for the channel to be fully processed
        // to catch any potential exceptions from the consumer side upon completion.
        try {
            await this.Reader.Completion.ConfigureAwait(false);
        }
        catch (Exception) {
            // Suppress exceptions during disposal, as the primary path for
            // handling completion errors should be by awaiting Reader.Completion directly.
        }
    }
}

/// <summary>
/// Provides the producer-side (writing) functionality for an <see cref="AsyncChannel{T}"/>.
/// </summary>
/// <typeparam name="T">The type of data in the channel.</typeparam>
public sealed class AsyncChannelWriter<T> {
    private readonly ChannelWriter<T> _writer;

    internal AsyncChannelWriter(ChannelWriter<T> writer) {
        this._writer = writer;
    }

    /// <summary>
    /// Asynchronously writes an item to the channel. If the channel is bounded and full,
    /// this method will wait until space is available.
    /// </summary>
    /// <param name="item">The item to write.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the item has been written.</returns>
    public ValueTask WriteAsync(T item, CancellationToken cancellationToken = default) {
        return this._writer.WriteAsync(item, cancellationToken);
    }

    /// <summary>
    /// Attempts to write an item to the channel synchronously.
    /// </summary>
    /// <param name="item">The item to write.</param>
    /// <returns><c>true</c> if the item was written successfully; otherwise, <c>false</c>.
    /// For a bounded channel, this returns <c>false</c> if the channel is full.</returns>
    public bool TryWrite(T item) {
        return this._writer.TryWrite(item);
    }

    /// <summary>
    /// Returns a <see cref="ValueTask{Boolean}"/> that completes when space is available to write an item.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ValueTask{Boolean}"/> that completes with <c>true</c> when space is available,
    /// or with <c>false</c> if the channel is marked as complete.
    /// </returns>
    public ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default) {
        return this._writer.WaitToWriteAsync(cancellationToken);
    }

    /// <summary>
    /// Marks the channel as being complete, meaning no more items will be written to it.
    /// </summary>
    /// <remarks>
    /// After completion, any attempt to write will throw a <see cref="ChannelClosedException"/>.
    /// This will unblock any waiting readers once the channel is empty.
    /// </remarks>
    /// <param name="error">An optional exception to complete the channel in a faulted state.</param>
    /// <returns><c>true</c> if this call successfully marked the channel as complete; <c>false</c> if it was already complete.</returns>
    public bool TryComplete(Exception? error = null) {
        return this._writer.TryComplete(error);
    }
}

/// <summary>
/// Provides the consumer-side (reading) functionality for an <see cref="AsyncChannel{T}"/>.
/// </summary>
/// <typeparam name="T">The type of data in the channel.</typeparam>
public sealed class AsyncChannelReader<T> {
    private readonly ChannelReader<T> _reader;

    internal AsyncChannelReader(ChannelReader<T> reader) {
        this._reader = reader;
    }

    /// <summary>
    /// Gets a <see cref="Task"/> that completes when the channel is marked as complete and all data has been read.
    /// The task will be in a faulted state if the channel was completed with an exception.
    /// </summary>
    public Task Completion => this._reader.Completion;

    /// <summary>
    /// Gets a snapshot of the number of items currently in the channel.
    /// </summary>
    public int Count => this._reader.Count;

    /// <summary>
    /// Asynchronously reads an item from the channel. If the channel is empty, this method will wait
    /// until an item becomes available.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask{T}"/> that completes with the read item.</returns>
    /// <exception cref="ChannelClosedException">Thrown if the channel is completed while waiting for an item.</exception>
    public ValueTask<T> ReadAsync(CancellationToken cancellationToken = default) {
        return this._reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Attempts to read an item from the channel synchronously.
    /// </summary>
    /// <param name="item">The read item, or the default value of <typeparamref name="T"/> if reading failed.</param>
    /// <returns><c>true</c> if an item was read successfully; otherwise, <c>false</c> if the channel is empty.</returns>
    public bool TryRead([MaybeNullWhen(false)] out T? item) {
        return this._reader.TryRead(out item);
    }

    /// <summary>
    /// Returns a <see cref="ValueTask{Boolean}"/> that completes when an item is available to be read.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ValueTask{Boolean}"/> that completes with <c>true</c> when an item is available,
    /// or with <c>false</c> if the channel is marked as complete and empty.
    /// </returns>
    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default) {
        return this._reader.WaitToReadAsync(cancellationToken);
    }

    /// <summary>
    /// Creates an <see cref="IAsyncEnumerable{T}"/> that allows reading all items from the channel
    /// until it is marked as complete.
    /// </summary>
    /// <example>
    /// <code>
    /// await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken)) {
    ///     // Process item
    /// }
    /// </code>
    /// </example>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An async enumerable that yields items as they become available.</returns>
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default) {
        return this._reader.ReadAllAsync(cancellationToken);
    }
}