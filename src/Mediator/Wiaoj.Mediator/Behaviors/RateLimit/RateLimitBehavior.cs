using System.Reflection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator.Behaviors;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Pipeline behavior that enforces a sliding-window rate limit per request type.
/// <para>Activated by placing <see cref="RateLimitAttribute"/> on the request class.</para>
/// <para>The counter is shared across all requests of the same type (process-level, not per-user).</para>
/// </summary>
public sealed class RateLimitBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse> {

    private static readonly RateLimitAttribute? _attr =
        typeof(TRequest).GetCustomAttribute<RateLimitAttribute>(inherit: false);

    // Lazily initialised, shared across all instances of this closed generic.
    private static readonly SlidingWindowCounter? _counter = _attr is not null
        ? SlidingWindowRegistry.GetOrCreate(typeof(TRequest), _attr.MaxRequests, _attr.Window)
        : null;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default) {

        if(_counter is null)
            return await next().ConfigureAwait(false);

        bool acquired = await _counter.TryAcquireAsync(cancellationToken).ConfigureAwait(false);

        if(!acquired)
            throw new RateLimitExceededException(typeof(TRequest), _attr!.MaxRequests, _attr.Per);

        return await next().ConfigureAwait(false);
    }
}