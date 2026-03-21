namespace Wiaoj.Mediator.Internal;
internal sealed class Mediator(IServiceProvider serviceProvider, HandlerRegistry registry) : IMediator {
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) {
        Func<IServiceProvider, object, CancellationToken, Task<TResponse>> handler 
            = registry.GetRequestHandler<TResponse>(request.GetType());
        return handler(serviceProvider, request, cancellationToken);
    } 

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) {
        Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<TResponse>> handler 
            = registry.GetStreamHandler<TResponse>(request.GetType());
        return handler(serviceProvider, request, cancellationToken);
    }
}