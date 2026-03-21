#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Defines a processor that runs AFTER the handler returns its response.
/// <para>Ideal for: audit logging, cache invalidation, domain event dispatching, response enrichment.</para>
/// <para>
/// Multiple post-processors for the same request type are executed in registration order.
/// The response is already committed — throwing here does NOT roll it back.
/// </para>
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestPostProcessor<in TRequest, TResponse> where TRequest : IRequest<TResponse> {
    /// <summary>
    /// Processes the request and response after the handler has returned.
    /// </summary>
    /// <param name="request">The original request.</param>
    /// <param name="response">The response returned by the handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken = default);
}