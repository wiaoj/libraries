namespace Wiaoj.Mediator;

// --- Request Marker Interfaces ---

/// <summary>
/// Marker interface to represent a request with a response.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IRequest<out TResponse> { }

/// <summary>
/// Marker interface to represent a command (typically a state-changing operation).
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse> { }

/// <summary>
/// Marker interface to represent a query (typically a data-retrieval operation).
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse> { }


// --- Handler Interface ---

/// <summary>
/// Defines a handler for a request.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse> {
    /// <summary>
    /// Handles the request asynchronously.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

// --- Pipeline Behavior Interface ---

/// <summary>
/// Represents a delegate for the next step in the pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <returns>A task representing the response.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Defines a pipeline behavior (middleware) that wraps the request handling process.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse> {
    /// <summary>
    /// Pipeline behavior handling method.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">The delegate to the next step in the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default);
}

// --- IMediator Interface ---

/// <summary>
/// Defines a mediator to encapsulate request/response interaction patterns.
/// </summary>
public interface IMediator : ISender {
    /// <summary>
    /// Creates a stream for the specified request.
    /// </summary>
    /// <typeparam name="TResponse">The type of the stream items.</typeparam>
    /// <param name="request">The stream request object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An asynchronous stream of responses.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for sending requests.
/// </summary>
public interface ISender {
    /// <summary>
    /// Asynchronously sends a request to a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the response.</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default); 
}

/// <summary>
/// Marker interface to represent a request that returns a stream of responses.
/// </summary>
/// <typeparam name="TResponse">The type of the items in the stream.</typeparam>
public interface IStreamRequest<out TResponse> : IRequest<IAsyncEnumerable<TResponse>> { }

/// <summary>
/// Defines a handler for a stream request.
/// </summary>
/// <typeparam name="TRequest">The type of the stream request.</typeparam>
/// <typeparam name="TResponse">The type of the stream items.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse> {
    /// <summary>
    /// Handles the stream request.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An asynchronous stream of items.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}