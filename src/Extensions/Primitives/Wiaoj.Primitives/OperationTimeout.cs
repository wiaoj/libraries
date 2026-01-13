using System.Diagnostics;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a timeout duration and/or a cancellation token as a single, unified value object.
/// This struct encapsulates the logic of combining time-based and token-based cancellation.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly record struct OperationTimeout {
    #region Static Members
    /// <summary>Represents an infinite timeout. The operation will wait indefinitely unless a token is cancelled.</summary>
    public static OperationTimeout Infinite { get; } = new(System.Threading.Timeout.InfiniteTimeSpan, CancellationToken.None);

    /// <summary>Represents a zero-time timeout. The operation will not wait and will only succeed if it can complete immediately.</summary>
    public static OperationTimeout None { get; } = new(TimeSpan.Zero, CancellationToken.None);

    /// <summary>Represents a pre-cancelled state.</summary>
    public static OperationTimeout Cancelled { get; } = new(TimeSpan.Zero, new CancellationToken(true));
    #endregion

    private readonly TimeSpan _delay;
    public readonly CancellationToken Token { get; }
     

    // Private constructor to enforce creation via factory methods.
    private OperationTimeout(TimeSpan delay, CancellationToken token) {
        Preca.ThrowIf(
            delay < System.Threading.Timeout.InfiniteTimeSpan,
            () => new ArgumentOutOfRangeException(nameof(delay), "OperationTimeout delay must be infinite (-1ms) or non-negative."));

        this._delay = delay;
        this.Token = token;
    }

    #region Factory Methods
    /// <summary>Creates a timeout from a TimeSpan.</summary>
    public static OperationTimeout From(TimeSpan delay) {
        return new(delay, CancellationToken.None);
    }

    /// <summary>Creates a timeout from a CancellationToken.</summary>
    public static OperationTimeout From(CancellationToken token) {
        return new(System.Threading.Timeout.InfiniteTimeSpan, token);
    }

    /// <summary>Creates a timeout that triggers when either the TimeSpan elapses or the CancellationToken is cancelled.</summary>
    public static OperationTimeout From(TimeSpan delay, CancellationToken token) {
        return new(delay, token);
    }

    /// <summary>Creates a timeout from a number of seconds.</summary>
    public static OperationTimeout FromSeconds(double seconds) {
        return From(TimeSpan.FromSeconds(seconds));
    }
    #endregion

    /// <summary>
    /// Gets a value indicating whether a time-based delay (TimeSpan) has been explicitly set 
    /// and is not infinite.
    /// </summary>
    public bool IsTimeoutSet => this._delay != System.Threading.Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Gets a value indicating whether an external CancellationToken is linked to this timeout.
    /// </summary>
    public bool IsCancellable => this.Token.CanBeCanceled;

    /// <summary>
    /// Gets a value indicating whether this timeout represents an infinite wait (no delay and no cancellable token).
    /// </summary>
    public bool IsInfinite =>
        this._delay == System.Threading.Timeout.InfiniteTimeSpan && !this.Token.CanBeCanceled;

    /// <summary>
    /// Gets a value indicating whether this timeout is pre-cancelled (either the token is already 
    /// cancelled or the delay is zero, representing an immediate check).
    /// </summary>
    public bool IsAlreadyCancelled =>
        this.Token.IsCancellationRequested || (this._delay == TimeSpan.Zero && this.Token.CanBeCanceled);

    #region Implicit Operators

    /// <summary>
    /// Defines an implicit conversion from a <see cref="TimeSpan"/> to an <see cref="OperationTimeout"/>.
    /// </summary>
    /// <param name="delay">The time duration to wait before the operation times out.</param>
    /// <remarks>
    /// This allows passing a <see cref="TimeSpan"/> directly into any method that expects an <see cref="OperationTimeout"/>.
    /// <example>
    /// <code>
    /// await repository.GetAsync(id, TimeSpan.FromSeconds(5));
    /// </code>
    /// </example>
    /// </remarks>
    public static implicit operator OperationTimeout(TimeSpan delay) {
        return From(delay);
    }

    /// <summary>
    /// Defines an implicit conversion from a <see cref="CancellationToken"/> to an <see cref="OperationTimeout"/>.
    /// </summary>
    /// <param name="token">The cancellation token to observe.</param>
    /// <remarks>
    /// This allows passing a <see cref="CancellationToken"/> directly into any method that expects an <see cref="OperationTimeout"/>, 
    /// treating it as an infinite timeout that only responds to the provided token.
    /// <example>
    /// <code>
    /// await repository.GetAsync(id, HttpContext.RequestAborted);
    /// </code>
    /// </example>
    /// </remarks>
    public static implicit operator OperationTimeout(CancellationToken token) {
        return From(token);
    }
    #endregion

    /// <summary>
    /// Creates and returns a new <see cref="CancellationTokenSource"/> that represents the combined timeout logic.
    /// </summary>
    /// <remarks>
    /// <para><b>CRITICAL:</b> The returned <see cref="CancellationTokenSource"/> is a disposable resource.
    /// The caller is responsible for ensuring it is disposed, typically by using a <c>using</c> block.</para>
    /// <para>Example: <c>using var cts = myTimeout.CreateCancellationTokenSource();</c></para>
    /// </remarks>
    /// <returns>A new, disposable <see cref="CancellationTokenSource"/>.</returns>
    public CancellationTokenSource CreateCancellationTokenSource() {
        bool hasDelay = this._delay != System.Threading.Timeout.InfiniteTimeSpan;
        bool hasToken = this.Token.CanBeCanceled;

        // Case 1: No timeout, no cancellation token. Nothing can cancel this.
        if(!hasDelay && !hasToken) {
            return new CancellationTokenSource(); // Returns a CTS that will never be cancelled.
        }

        // Case 2: Only a token is provided. Link to it.
        if(!hasDelay && hasToken) {
            return CancellationTokenSource.CreateLinkedTokenSource(this.Token);
        }

        // Case 3: Only a delay is provided.
        CancellationTokenSource cts = new(this._delay);

        // Case 4: Both delay and token are provided. Link them.
        if(hasToken) {
            return CancellationTokenSource.CreateLinkedTokenSource(cts.Token, this.Token);
        }

        // Return the delay-only CTS from Case 3.
        return cts;
    }

    /// <summary>
    /// Creates and immediately checks the combined cancellation signal, throwing an 
    /// <see cref="OperationCanceledException"/> if the timeout has elapsed or the token has been cancelled.
    /// <para>NOTE: This method internally creates and disposes a CancellationTokenSource for immediate status check.</para>
    /// </summary>
    public void ThrowIfExpired() {
        using CancellationTokenSource cts = CreateCancellationTokenSource();
        cts.Token.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Executes an operation using <see cref="ValueTask"/> for better memory efficiency by wrapping the active cancellation signal.
    /// </summary>
    /// <param name="action">The operation to execute, receiving an <see cref="OperationTimeout"/> that encapsulates the current running state.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Recommended for high-throughput operations or scenarios where the operation might complete synchronously, 
    /// reducing Garbage Collector (GC) overhead.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Thrown when a timeout occurs or an external cancellation is requested.</exception>
    public async ValueTask ExecuteAsync(Func<OperationTimeout, ValueTask> action) {
        using CancellationTokenSource cts = CreateCancellationTokenSource();

        try {
            var runningTimeout = OperationTimeout.From(cts.Token);
            await action(runningTimeout).ConfigureAwait(false);
        }
        catch(OperationCanceledException) {
            throw;
        }
    }

    /// <summary>
    /// Executes an operation that returns a value using <see cref="ValueTask{TResult}"/> for optimized memory allocation.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the operation.</typeparam>
    /// <param name="action">The operation to execute, receiving an <see cref="OperationTimeout"/> that encapsulates the current running state.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation and its result.</returns>
    /// <remarks>
    /// Use this method instead of <see cref="Task{T}"/> in performance-critical loops or high-frequency API requests 
    /// to minimize heap allocations.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Thrown when a timeout occurs or an external cancellation is requested.</exception>
    public async ValueTask<TResult> ExecuteAsync<TResult>(Func<OperationTimeout, ValueTask<TResult>> action) {
        using CancellationTokenSource cts = CreateCancellationTokenSource();

        try {
            var runningTimeout = OperationTimeout.From(cts.Token);
            return await action(runningTimeout).ConfigureAwait(false);
        }
        catch(OperationCanceledException) {
            throw;
        }
    }

    /// <inheritdoc/>
    public override string ToString() {
        bool hasDelay = this._delay != System.Threading.Timeout.InfiniteTimeSpan;
        bool hasToken = this.Token.CanBeCanceled;
        if(!hasDelay && !hasToken) {
            return "Infinite";
        }

        if(hasDelay && !hasToken) {
            return $"{this._delay.TotalMilliseconds}ms";
        }

        if(!hasDelay && hasToken) {
            return "Cancellable Token";
        }

        return $"{this._delay.TotalMilliseconds}ms + Cancellable Token";
    }
}