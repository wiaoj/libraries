namespace Wiaoj.Mediator;

// ─────────────────────────────────────────────────────────────────────────────
// Request Marker Interfaces
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Marker interface to represent a request with a response.</summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IRequest<out TResponse> { }

/// <summary>Marker interface to represent a command (state-changing operation).</summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse> { }

/// <summary>Marker interface to represent a query (data-retrieval operation).</summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse> { }


// ─────────────────────────────────────────────────────────────────────────────
// Handler Interface
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Defines a handler for a request.</summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse> {
    /// <summary>Handles the request asynchronously.</summary>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}


// ─────────────────────────────────────────────────────────────────────────────
// Pipeline Behavior
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Represents a delegate for the next step in the request pipeline.</summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>Defines a pipeline behavior (middleware) that wraps the request handling process.</summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse> {
    /// <summary>Pipeline behavior handling method.</summary>
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}


// ─────────────────────────────────────────────────────────────────────────────
// Mediator / Sender Interfaces
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Defines the contract for sending requests.</summary>
public interface ISender {
    /// <summary>Asynchronously sends a request to a single handler.</summary>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

/// <summary>Defines a mediator to encapsulate request/response and stream interaction patterns.</summary>
public interface IMediator : ISender {
    /// <summary>Creates an async stream for the specified stream request.</summary>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}


// ─────────────────────────────────────────────────────────────────────────────
// Stream Interfaces
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Marker interface to represent a request that returns a stream of responses.</summary>
/// <typeparam name="TResponse">The type of items in the stream.</typeparam>
public interface IStreamRequest<out TResponse> : IRequest<IAsyncEnumerable<TResponse>> { }

/// <summary>Defines a handler for a stream request.</summary>
/// <typeparam name="TRequest">The type of the stream request.</typeparam>
/// <typeparam name="TResponse">The type of stream items.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse> {
    /// <summary>Handles the stream request.</summary>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}