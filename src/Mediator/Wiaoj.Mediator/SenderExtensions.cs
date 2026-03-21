using Wiaoj.Primitives;

namespace Wiaoj.Mediator;

public static class SenderExtensions { 
    /// <summary>
    /// Sends a parameterless request by specifying both request and response types explicitly.
    /// Useful for query objects that carry no data.
    /// </summary>
    public static Task<TResponse> Send<TRequest, TResponse>(
        this ISender sender,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>, new() {
        return sender.Send(new TRequest(), cancellationToken);
    }

    /// <summary>
    /// Sends a parameterless command that returns <see cref="Empty"/>.
    /// </summary>
    public static Task<Empty> Send<TRequest>(
        this ISender sender,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<Empty>, new() {
        return sender.Send(new TRequest(), cancellationToken);
    }
}