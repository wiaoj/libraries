using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Wiaoj.Abstractions;

namespace Wiaoj.Concurrency;
/// <summary>
/// Provides support for lazy, asynchronous initialization of a value.
/// This implementation is aligned with the design patterns of System.Lazy&lt;T&gt;.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is fully thread-safe, lock-free, ensures the value-producing factory is executed only once,
/// and correctly handles exceptions and cancellation. It prevents thread starvation by avoiding synchronous locks.
/// It is a foundational primitive for highly concurrent and asynchronous programming.
/// </para>
/// <para>
/// It supports initialization from a pre-computed value, a parameterless constructor, 
/// synchronous factories (<see cref="Func{TResult}"/>), and asynchronous factories (<see cref="Func{TResult}"/> returning a <see cref="Task{TResult}"/>).
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the object that is being asynchronously initialized.</typeparam>
[DebuggerDisplay("Status = {Status,nq}")]
public sealed class AsyncLazy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : IAsyncDisposable {
    private Func<CancellationToken, Task<T>>? _factory;

    // We use Volatile/Interlocked operations on this field to achieve lock-free thread safety.
    private Task<T>? _initializationTask;

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class with a pre-computed value.
    /// The instance will be considered successfully initialized from the start.
    /// </summary>
    /// <param name="value">The pre-computed value to wrap.</param>
    public AsyncLazy(T value) {
        // The value is already available, so we store it in a pre-completed task.
        this._initializationTask = Task.FromResult(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class that will create the value
    /// using the public parameterless constructor of <typeparamref name="T"/>.
    /// </summary>
    public AsyncLazy() : this(cancellationToken => Task.Run(() => Activator.CreateInstance<T>(), cancellationToken)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class using a synchronous factory.
    /// The factory will be executed asynchronously on a thread pool thread.
    /// </summary>
    public AsyncLazy(Func<T> factory) : this(WrapSyncFactory(factory)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class using an asynchronous factory delegate.
    /// </summary>
    public AsyncLazy(Func<Task<T>> factory) : this(WrapFactory(factory)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class using a pre-existing task.
    /// </summary>
    /// <param name="task">The task representing the asynchronous initialization.</param>
    public AsyncLazy(Task<T> task) {
        Preca.ThrowIfNull(task);
        this._initializationTask = task;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class using a standardized async factory.
    /// </summary>
    public AsyncLazy(IAsyncFactory<T> asyncFactory) : this(asyncFactory.CreateAsync) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class using a cancellable asynchronous factory delegate.
    /// This is the core factory-based constructor.
    /// </summary>
    public AsyncLazy(Func<CancellationToken, Task<T>> factory) {
        Preca.ThrowIfNull(factory);
        this._factory = factory;
    }

    #endregion

    #region Properties & Public Methods

    /// <summary>
    /// Gets a value indicating whether the asynchronous value has been created and completed successfully.
    /// </summary>
    public bool IsValueCreated => Volatile.Read(ref this._initializationTask)?.IsCompletedSuccessfully ?? false;

    /// <summary>
    /// Asynchronously starts the initialization (if not already started) and retrieves the value.
    /// </summary>
    public ValueTask<T> GetValueAsync(CancellationToken cancellationToken = default) {
        // Fast path: Volatile read to check if the task is already created.
        Task<T>? task = Volatile.Read(ref this._initializationTask);

        if(task is not null) {
            // Optimization: If the task is already successfully completed, return the result synchronously.
            return task.IsCompletedSuccessfully
                ? new ValueTask<T>(task.Result)
                : new ValueTask<T>(task);
        }

        // Slow path: The task hasn't been created yet. We need to initialize it lock-free.
        return new ValueTask<T>(InitializeAsync(cancellationToken));
    }

    /// <summary>
    /// Provides an awaiter for this instance, allowing it to be used directly with the 'await' keyword.
    /// </summary>
#pragma warning disable CA2012 // ValueTask is consumed correctly by the awaiter pattern.
    public ValueTaskAwaiter<T> GetAwaiter() {
        return GetValueAsync().GetAwaiter();
    }
#pragma warning restore CA2012

    #endregion

    #region Private Implementation (Lock-Free Initialization)

    private Task<T> InitializeAsync(CancellationToken cancellationToken) {
        // Prepare a placeholder task (TaskCompletionSource) that we will try to publish.
        TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Atomically attempt to set our placeholder task as the official initialization task.
        // If _initializationTask is currently null, it becomes tcs.Task.
        Task<T>? winningTask = Interlocked.CompareExchange(ref this._initializationTask, tcs.Task, null);

        if(winningTask is not null) {
            // Another thread beat us to the initialization! 
            // We simply discard our 'tcs' and return the winner's task.
            return winningTask;
        }

        // If we reach here, we successfully published our placeholder task.
        // We are now exclusively responsible for executing the factory.
        // We fire and forget the execution method; it will resolve the 'tcs' when done.
        _ = ExecuteFactoryAsync(tcs, cancellationToken);

        return tcs.Task;
    }

    private async Task ExecuteFactoryAsync(TaskCompletionSource<T> tcs, CancellationToken cancellationToken) {
        // Capture the factory and clear the field so it can be garbage collected.
        Func<CancellationToken, Task<T>>? factory = Interlocked.Exchange(ref this._factory, null);

        // This should theoretically never happen if the class is used correctly, but we guard against it.
        if(factory is null) {
            tcs.SetException(new InvalidOperationException("The factory has already been executed or cleared unexpectedly."));
            return;
        }

        try {
            cancellationToken.ThrowIfCancellationRequested();

            // Execute the user-provided factory.
            T result = await factory(cancellationToken).ConfigureAwait(false);

            // Fulfill the promise, waking up all awaiting threads.
            tcs.SetResult(result);
        }
        catch(OperationCanceledException ex) {
            tcs.SetCanceled(ex.CancellationToken);

            // If initialization was canceled, reset the state so it can be retried later.
            Interlocked.Exchange(ref this._initializationTask, null);
            Volatile.Write(ref this._factory, factory); // Restore the factory for retry
        }
        catch(Exception ex) {
            tcs.SetException(ex);

            // If initialization failed, reset the state so it can be retried later.
            Interlocked.Exchange(ref this._initializationTask, null);
            Volatile.Write(ref this._factory, factory); // Restore the factory for retry
        }
    }

    private static Func<CancellationToken, Task<T>> WrapSyncFactory(Func<T> factory) {
        Preca.ThrowIfNull(factory);
        return cancellationToken => Task.Run(factory, cancellationToken);
    }

    private static Func<CancellationToken, Task<T>> WrapFactory(Func<Task<T>> factory) {
        Preca.ThrowIfNull(factory);
        return _ => factory();
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Asynchronously releases the resources used by the <see cref="AsyncLazy{T}"/> instance.
    /// If the underlying value implements <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/>, it is disposed.
    /// </summary>
    public async ValueTask DisposeAsync() {
        // Read the task safely.
        Task<T>? task = Volatile.Read(ref this._initializationTask);

        if(task is not null) {
            try {
                // Ensure the value is fully created before attempting to dispose it.
                T value = await task.ConfigureAwait(false);

                if(value is IAsyncDisposable asyncDisp) {
                    await asyncDisp.DisposeAsync().ConfigureAwait(false);
                }
                else if(value is IDisposable disp) {
                    disp.Dispose();
                }
            }
            catch(Exception ex) {
                // Disposal errors are generally swallowed or logged, 
                // but should not break the main application flow.
                Debug.WriteLine($"AsyncLazy disposal failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Debugger Support

    /// <summary>
    /// Gets the current initialization status of this instance.
    /// </summary>
    /// <remarks>
    /// This property is primarily intended for debugging and monitoring purposes.
    /// </remarks>
    private DebuggerStatus Status {
        get {
            Task<T>? task = Volatile.Read(ref this._initializationTask);
            if(task is null) return DebuggerStatus.NotStarted;

            if(task.IsCompletedSuccessfully) return DebuggerStatus.Succeeded;
            if(task.IsCanceled) return DebuggerStatus.Canceled;
            if(task.IsFaulted) return DebuggerStatus.Faulted;

            return DebuggerStatus.Executing;
        }
    }

    /// <summary>
    /// Represents the initialization status of an asynchronous lazy-loading operation, primarily for debugging and monitoring.
    /// </summary>
    private readonly record struct DebuggerStatus {
        private readonly string _name;

        private DebuggerStatus(string name) {
            this._name = name;
        }

        /// <summary>The asynchronous operation has not yet been started.</summary>
        public static DebuggerStatus NotStarted { get; } = new("NotStarted");

        /// <summary>The asynchronous operation has been started but has not yet completed.</summary>
        public static DebuggerStatus Executing { get; } = new("Executing");

        /// <summary>The asynchronous operation completed successfully and a value is available.</summary>
        public static DebuggerStatus Succeeded { get; } = new("Succeeded");

        /// <summary>The asynchronous operation was canceled.</summary>
        public static DebuggerStatus Canceled { get; } = new("Canceled");

        /// <summary>The asynchronous operation completed with an exception.</summary>
        public static DebuggerStatus Faulted { get; } = new("Faulted");

        /// <summary>
        /// Returns the string representation of the status.
        /// </summary>
        public override string ToString() {
            return this._name;
        }

        /// <summary>
        /// Implicitly converts a <see cref="DebuggerStatus"/> to a <see cref="string"/>.
        /// </summary>
        public static implicit operator string(DebuggerStatus status) {
            return status._name;
        }
    }
    #endregion
}