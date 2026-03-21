using System.Diagnostics; 
using Wiaoj.Preconditions;

namespace Wiaoj.Mediator.Internal;
internal sealed class TracingMediator(
    IServiceProvider serviceProvider,
    HandlerRegistry registry
    ) : IMediator {

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(request);
        string requestName = request.GetType().Name;
        // Trace Başlat
        using Activity? activity = WiaojDiagnostics.Source.StartActivity($"Mediator Send {requestName}", ActivityKind.Internal);
        activity?.AddTag("mediator.request_type", requestName);

        try {
            Func<IServiceProvider, object, CancellationToken, Task<TResponse>> handler
                = registry.GetRequestHandler<TResponse>(request.GetType());
            return await handler(serviceProvider, request, cancellationToken);
        }
        catch(Exception ex) {
            // Hata durumunda Trace'e işaret koy
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag("error.type", ex.GetType().Name);
            throw;
        }
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(request);
        // Stream Trace
        using Activity? activity = WiaojDiagnostics.Source.StartActivity($"Mediator Stream {request.GetType().Name}", ActivityKind.Internal);

        Func<IServiceProvider, object, CancellationToken, IAsyncEnumerable<TResponse>> handler 
            = registry.GetStreamHandler<TResponse>(request.GetType());
        return handler(serviceProvider, request, cancellationToken);
    }
}