#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Handles exceptions thrown during request processing.
/// <para>
/// Call <see cref="ExceptionContext{TResponse}.SetHandled"/> on the <paramref name="context"/> to swallow
/// the exception and return a fallback value. If not called, the exception is re-thrown after this handler returns.
/// </para>
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <typeparam name="TException">The exception type to handle.</typeparam>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : IRequest<TResponse>
    where TException : Exception {

    /// <summary>
    /// Invoked when <typeparamref name="TException"/> is thrown during the handling of <typeparamref name="TRequest"/>.
    /// </summary>
    /// <param name="request">The original request.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="context">
    /// Recovery context. Call <see cref="ExceptionContext{TResponse}.SetHandled"/> to swallow the exception.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Handle(
        TRequest request,
        TException exception,
        ExceptionContext<TResponse> context,
        CancellationToken cancellationToken = default);
}