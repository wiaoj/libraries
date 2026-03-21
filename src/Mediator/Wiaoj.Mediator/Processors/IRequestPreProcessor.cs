#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Defines a processor that runs BEFORE the request enters the behavior pipeline.
/// <para>Ideal for: input sanitization, request enrichment, authorization pre-checks, idempotency guards.</para>
/// <para>
/// Multiple pre-processors for the same request type are executed in registration order.
/// Throwing an exception in a pre-processor cancels the entire pipeline.
/// </para>
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestPreProcessor<in TRequest, TResponse> where TRequest : IRequest<TResponse> {
    /// <summary>
    /// Processes the request before it reaches the behavior pipeline.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Process(TRequest request, CancellationToken cancellationToken = default);
}