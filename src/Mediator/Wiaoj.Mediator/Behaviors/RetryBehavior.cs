using System.Reflection;

namespace Wiaoj.Mediator.Behaviors;
/// <summary>
/// Pipeline behavior that automatically retries the request on failure.
/// <para>Activated by placing <see cref="RetryAttribute"/> on the request class.</para>
/// </summary>
public sealed class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse> {

    // Static per closed-generic — initialised once, never per-request.
    private static readonly RetryAttribute? _attr =
        typeof(TRequest).GetCustomAttribute<RetryAttribute>(inherit: false);

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default) {

        if(_attr is null)
            return await next().ConfigureAwait(false);

        int attempt = 0;
        while(true) {
            try {
                return await next().ConfigureAwait(false);
            }
            catch(Exception) when(!cancellationToken.IsCancellationRequested && attempt < _attr.Count) {
                attempt++;
                int delay = _attr.ExponentialBackoff
                    ? (int)(_attr.DelayMs * Math.Pow(2, attempt - 1))
                    : _attr.DelayMs;

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}