using System.Reflection;

namespace Wiaoj.Mediator.Behaviors;
/// <summary>
/// Pipeline behavior that enforces a maximum execution time for the request.
/// <para>Activated by placing <see cref="TimeoutAttribute"/> on the request class.</para>
/// <para>
/// When the timeout fires, the linked <see cref="CancellationTokenSource"/> signals cancellation
/// to the downstream handler (provided it observes the token). A <see cref="TimeoutException"/>
/// is then thrown to the caller.
/// </para>
/// </summary>
public sealed class TimeoutBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse> {

    private static readonly TimeoutAttribute? _attr =
        typeof(TRequest).GetCustomAttribute<TimeoutAttribute>(inherit: false);

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default) {

        if(_attr is null)
            return await next().ConfigureAwait(false);

        using CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        linkedCts.CancelAfter(_attr.Milliseconds);

        Task<TResponse> handlerTask = next();
        Task timeoutTask = Task.Delay(Timeout.Infinite, linkedCts.Token);

        Task completed = await Task.WhenAny(handlerTask, timeoutTask).ConfigureAwait(false);

        if(completed != handlerTask) {
            // Only throw TimeoutException if the original CT wasn't already cancelled.
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(
                $"Request '{typeof(TRequest).Name}' timed out after {_attr.Milliseconds} ms.");
        }

        // Propagate any exception from the handler (e.g. OperationCanceledException).
        return await handlerTask.ConfigureAwait(false);
    }
}