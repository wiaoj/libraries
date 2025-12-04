namespace Wiaoj.ObjectPool;
/// <summary>
/// Defines a contract for an object that can be reset to its initial state.
/// This is essential for objects that are managed by an object pool.
/// </summary>
public interface IResettable {
    /// <summary>
    /// Resets the object to its default state, preparing it for reuse.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the object was successfully reset; otherwise, <see langword="false"/>.
    /// </returns> 
    bool TryReset();
}