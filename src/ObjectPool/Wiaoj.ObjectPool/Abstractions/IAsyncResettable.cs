namespace Wiaoj.ObjectPool.Abstractions;
/// <summary>
/// Defines a contract for an object that requires asynchronous operations to reset its state.
/// Essential for objects involving I/O during cleanup (e.g., DB connections, Sockets).
/// </summary>
public interface IAsyncResettable {
    /// <summary>
    /// Asynchronously resets the object to its default state.
    /// </summary>
    /// <returns>True if successful; otherwise false (object will be discarded).</returns>
    ValueTask<bool> TryResetAsync();
}