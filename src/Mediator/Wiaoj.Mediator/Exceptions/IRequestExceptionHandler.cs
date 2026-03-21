using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 
namespace Wiaoj.Mediator;
#pragma warning restore IDE0130 
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : IRequest<TResponse>
    where TException : Exception {
    Task HandleAsync(TRequest request, TException exception, CancellationToken cancellationToken = default);
}