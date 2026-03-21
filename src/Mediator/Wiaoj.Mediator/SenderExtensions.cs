using Wiaoj.Primitives;

namespace Wiaoj.Mediator;

public static class SenderExtensions {
    /// <summary>
    /// Hem Request tipini hem Response tipini belirterek parametresiz gönderim (Query için)
    /// </summary>
    public static Task<TResponse> Send<TRequest, TResponse>(
        this ISender sender,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>, new() {
        return sender.Send(new TRequest(), cancellationToken);
    }

    /// <summary>
    /// Sadece Command tipini belirterek parametresiz gönderim (Unit dönen Command'ler için)
    /// </summary>
    public static Task<Empty> Send<TRequest>(
        this ISender sender,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<Empty>, new() {
        return sender.Send(new TRequest(), cancellationToken);
    }
}