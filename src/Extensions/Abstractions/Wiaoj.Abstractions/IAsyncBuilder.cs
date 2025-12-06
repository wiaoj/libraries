using System;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Abstractions;
/// <summary>
/// Defines a contract for a builder that constructs instances of type <typeparamref name="T"/>.
/// </summary>
public interface IBuilder<out T> {
    /// <summary>
    /// Builds and returns the instance of <typeparamref name="T"/>.
    /// </summary>
    T Build();
}

/// <summary>
/// Defines a contract for an asynchronous builder.
/// </summary>
public interface IAsyncBuilder<T> {
    /// <summary>
    /// Asynchronously builds and returns the instance of <typeparamref name="T"/>.
    /// </summary>
    Task<T> BuildAsync(CancellationToken cancellationToken = default);
}