namespace Wiaoj.Abstractions;

/// <summary>
/// Defines a contract for a builder that constructs instances of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="IAsyncFactory{T}"/> and its arity overloads, this interface intentionally
/// does not accept construction arguments on <see cref="Build"/>. The builder pattern assumes
/// state is accumulated beforehand via fluent configuration methods on the concrete implementer
/// (e.g. <c>.WithName(...)</c>) and <see cref="Build"/> simply materializes the accumulated state.
/// If construction genuinely needs external arguments at call time rather than pre-configured
/// state, prefer <see cref="IAsyncFactory{T}"/> (or its <c>T1</c>/<c>T2</c>/<c>T3</c> overloads) instead.
/// </remarks>
/// <typeparam name="T">The type of instance produced by <see cref="Build"/>.</typeparam>
public interface IBuilder<out T> {
    /// <summary>
    /// Builds and returns the instance of <typeparamref name="T"/>.
    /// </summary>
    T Build();
}

/// <summary>
/// Defines a contract for an asynchronous builder.
/// </summary>
/// <remarks>
/// The asynchronous counterpart to <see cref="IBuilder{T}"/>. Use this when materializing the
/// accumulated state requires I/O or otherwise cannot complete synchronously (e.g. validating
/// against a remote service before producing the final instance). See <see cref="IBuilder{T}"/>
/// remarks for why this interface, like its synchronous counterpart, does not accept
/// construction arguments.
/// </remarks>
/// <typeparam name="T">The type of instance produced by <see cref="BuildAsync"/>.</typeparam>
public interface IAsyncBuilder<T> {
    /// <summary>
    /// Asynchronously builds and returns the instance of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<T> BuildAsync(CancellationToken cancellationToken = default);
}