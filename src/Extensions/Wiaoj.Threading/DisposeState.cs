using System.Runtime.CompilerServices;

namespace Wiaoj.Concurrency;

[Obsolete]
public readonly struct DisposeState(LifecycleManager<LifecycleState> manager) {

    [Obsolete] public DisposeState() : this(new LifecycleManager<LifecycleState>(LifecycleState.Active)) { }

    [Obsolete]
    public LifecycleState State => manager.State;
    [Obsolete]
    public bool IsDisposed => this.State.IsDisposed;
    [Obsolete]
    public bool IsDisposingOrDisposed => this.State.IsDisposingOrDisposed;

    [Obsolete]
    public bool TryBeginDispose() {
        return manager.TryTransition(LifecycleState.Disposing, LifecycleState.Active);
    }

    [Obsolete]
    public void SetDisposed() {
        manager.Set(LifecycleState.Disposed);
    }

    [Obsolete]
    public void ThrowIfDisposingOrDisposed(string objectName) {
        if (this.IsDisposingOrDisposed) {
            throw new ObjectDisposedException(objectName, "The object is currently being disposed or has already been disposed.");
        }
    }
}

/// <summary>
/// Defines the common lifecycle states for a component or object.
/// </summary>

[Obsolete]
public enum LifecycleState {
    /// <summary>
    /// The component is active and operational.
    /// </summary>

    [Obsolete] Active = 0,

    /// <summary>
    /// The component is in the process of being disposed, but the operation has not yet completed.
    /// This is particularly useful for asynchronous disposal.
    /// </summary>

    [Obsolete] Disposing = 1,

    /// <summary>
    /// The component has been fully disposed and is no longer operational.
    /// </summary>

    [Obsolete] Disposed = 2
}

[Obsolete]
public static class LifecycleStateExtensions {
    extension(LifecycleState state) {
        /// <summary>
        /// Determines if the current state is either Disposing or Disposed.
        /// </summary>

        [Obsolete] public bool IsDisposingOrDisposed => state >= LifecycleState.Disposing;



        [Obsolete] public bool IsDisposed => state == LifecycleState.Disposed;

    }
}


/// <summary>
/// Provides a thread-safe, lock-free, and generic state manager for an object's lifecycle.
/// </summary>
/// <typeparam name="TState">The enum type that defines the lifecycle states.
/// It must have an underlying type of <see cref="int"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the manager with a specific starting state.
/// </remarks>

[Obsolete]
public readonly struct LifecycleManager<TState>(TState initialState) where TState : struct, Enum {
    private readonly int _state = Unsafe.As<TState, int>(ref initialState);

    /// <summary>
    /// Gets the current lifecycle state using a thread-safe (volatile) read.
    /// </summary>

    [Obsolete]
    public TState State {
        get {
            // 1. _state alanına 'readonly' kısıtını atlatarak bir referans al.
            ref int stateAsInt = ref Unsafe.AsRef(in this._state);

            // 2. Bu int referansını kullanarak Atomic.Read'in doğru (int) overload'unu çağır.
            int currentState = Atomic.Read(ref stateAsInt);

            // 3. Okunan int değerini TState enum'una dönüştür.
            return Unsafe.As<int, TState>(ref currentState);
        }
    }

    /// <summary>
    /// Atomically transitions the state from a specified <paramref name="comparand"/> state to a new <paramref name="value"/> state.
    /// </summary>

    [Obsolete]
    public bool TryTransition(TState value, TState comparand) {
        // Bu metot zaten doğruydu. Enum'ları int'e çevirip doğru CompareAndSet'i çağırıyor.
        return Atomic.CompareExchange(
            ref Unsafe.AsRef(in this._state),
            Unsafe.As<TState, int>(ref value),
            Unsafe.As<TState, int>(ref comparand)
        );
    }

    /// <summary>
    /// Sets the state to a new value using a thread-safe (volatile) write.
    /// </summary>

    [Obsolete]
    public void Set(TState value) {
        // 1. _state alanına 'readonly' kısıtını atlatarak bir referans al.
        ref int stateAsInt = ref Unsafe.AsRef(in this._state);

        // 2. Gelen TState değerini int'e dönüştür.
        int valueAsInt = Unsafe.As<TState, int>(ref value);

        // 3. Atomic.Write'ın doğru (int) overload'unu çağır.
        Atomic.Write(ref stateAsInt, valueAsInt);
    }
}