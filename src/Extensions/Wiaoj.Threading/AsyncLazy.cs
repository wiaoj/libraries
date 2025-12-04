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
/// This implementation is fully thread-safe, ensures the value-producing factory is executed only once,
/// and correctly handles exceptions and cancellation. It is a foundational primitive for concurrent and asynchronous programming.
/// </para>
/// <para>
/// It supports initialization from a pre-computed value, a parameterless constructor, 
/// synchronous factories (<see cref="Func{TResult}"/>), and asynchronous factories (<see cref="Func{TResult}"/> returning a <see cref="Task{TResult}"/>).
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the object that is being asynchronously initialized.</typeparam>
[DebuggerDisplay("Status = {Status,nq}")]
public sealed class AsyncLazy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> {
    private Func<CancellationToken, Task<T>>? _factory;
    private volatile Task<T>? _initializationTask;

    // Using 'object' is the simplest and most efficient solution for this ultra-low-contention lock.
    // The IDE0330 warning is suppressed as its general recommendation is not optimal for this specific algorithm.
#pragma warning disable IDE0330
    private readonly object _lock = new();
#pragma warning restore IDE0330

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
    public AsyncLazy()
        : this(cancellationToken => Task.Run(() => Activator.CreateInstance<T>(), cancellationToken)) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class using a synchronous factory.
    /// The factory will be executed asynchronously on a thread pool thread.
    /// </summary>
    public AsyncLazy(Func<T> factory)
        : this(WrapSyncFactory(factory)) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class using an asynchronous factory delegate.
    /// </summary>
    public AsyncLazy(Func<Task<T>> factory)
        : this(WrapFactory(factory)) {
    }

    public AsyncLazy(Task<T> task) {
        this._initializationTask = task;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class using a standardized async factory.
    /// </summary>
    public AsyncLazy(IAsyncFactory<T> asyncFactory)
        : this(asyncFactory.CreateAsync) {
    }

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
    public bool IsValueCreated => this._initializationTask?.IsCompletedSuccessfully ?? false;

    /// <summary>
    /// Asynchronously starts the initialization and retrieves the value.
    /// </summary>
    public ValueTask<T> GetValueAsync(CancellationToken cancellationToken = default) {
        Task<T>? task = this._initializationTask;
        if (task is not null) {
            return task.IsCompletedSuccessfully
                ? new ValueTask<T>(task.Result)
                : new ValueTask<T>(task);
        }

        return new ValueTask<T>(InitializeAndGetTaskAsync(cancellationToken));
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

    #region Private Implementation

    private Task<T> InitializeAndGetTaskAsync(CancellationToken cancellationToken) {
        lock (this._lock) {
            Task<T>? task = this._initializationTask;
            if (task is not null) return task;

            task = InitializeCoreAsync(cancellationToken);
            this._initializationTask = task;
            return task;
        }
    }

    private async Task<T> InitializeCoreAsync(CancellationToken cancellationToken) {
        Func<CancellationToken, Task<T>>? factory = this._factory;
        this._factory = null;

        Preca.ThrowIfNull(factory, static () => new InvalidOperationException("The factory has already been executed or cleared."));

        try {
            cancellationToken.ThrowIfCancellationRequested();
            return await factory(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) {
            throw;
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

    #region Debugger Support
    /// <summary>
    /// Gets the current initialization status of this instance.
    /// </summary>
    /// <remarks>
    /// This property is primarily intended for debugging and monitoring purposes.
    /// </remarks>
    private DebuggerStatus Status {
        get {
            Task<T>? task = this._initializationTask;
            if (task is null) return DebuggerStatus.NotStarted;

            if (task.IsCompletedSuccessfully) return DebuggerStatus.Succeeded;
            if (task.IsCanceled) return DebuggerStatus.Canceled;
            if (task.IsFaulted) return DebuggerStatus.Faulted;

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