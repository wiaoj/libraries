namespace Wiaoj.Abstractions;
/// <summary>
/// Defines a contract for objects that can be merged with another object of the same type using a specific strategy.
/// </summary>
/// <remarks>
/// This interface is typically used to blend default configurations or settings with runtime-specific values.
/// The implementation determines how properties are combined (e.g., source values overwriting defaults if not null).
/// </remarks>
/// <typeparam name="T">The type of the objects to be merged.</typeparam>
public interface IMergeable<T> {
    /// <summary>
    /// Merges the current instance with the specified object and returns the combined result.
    /// </summary>
    /// <param name="other">The other object to be merged into the current instance. Can be <see langword="null"/>.</param>
    /// <returns>A new object or the updated current instance representing the merged result.</returns>
    T Merge(T? other);
}

/// <summary>
/// Provides extension methods for types implementing the <see cref="IMergeable{T}"/> interface.
/// </summary>
public static class MergeableExtensions {
    /// <summary>
    /// A helper method to merge the source object with another instance. 
    /// Ensures that the source is not null before attempting the merge.
    /// </summary>
    /// <typeparam name="T">The type implementing <see cref="IMergeable{T}"/>.</typeparam>
    /// <param name="source">The primary object that performs the merge.</param>
    /// <param name="other">The secondary object to merge into the source.</param>
    /// <returns>The result of the merge operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public static T MergeWith<T>(this IMergeable<T> source, T? other) {
        Preca.ThrowIfNull(source);
        return source.Merge(other);
    }
}