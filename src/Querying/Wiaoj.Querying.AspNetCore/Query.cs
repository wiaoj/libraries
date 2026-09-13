using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;
using Wiaoj.Querying.AspNetCore.Binders;

namespace Wiaoj.Querying.AspNetCore;

/// <summary>
/// A minimal-API bindable wrapper around <see cref="QueryRequest"/>, scoped to a specific entity type
/// <typeparamref name="TEntity"/> so that ASP.NET Core's parameter-binding convention (which resolves
/// <c>BindAsync</c> per closed generic type) can locate it without requiring <see cref="QueryRequest"/>
/// itself to depend on ASP.NET Core.
/// </summary>
/// <typeparam name="TEntity">
/// The entity type this query targets. Carries no runtime behavior by itself — it only disambiguates
/// binding and lets <see cref="QueryValidationEndpointFilter{T}"/> locate the correct argument.
/// </typeparam>
/// <param name="Value">The parsed, not-yet-validated <see cref="QueryRequest"/>.</param>
public sealed record Query<TEntity>(QueryRequest Value) : IBindableFromHttpContext<Query<TEntity>> {
    /// <summary>
    /// Parses the incoming request's query string into a <see cref="QueryRequest"/>.
    /// </summary>
    /// <remarks>
    /// This performs parsing only — no schema validation happens here. Validation is the responsibility
    /// of <see cref="QueryValidationEndpointFilter{T}"/>, which runs after binding and can therefore
    /// produce a proper <c>ValidationProblem</c> response instead of a bare 400.
    /// </remarks>
    public static async ValueTask<Query<TEntity>?> BindAsync(HttpContext context, ParameterInfo parameter) {
        Preca.ThrowIfNull(context);

        Endpoint? endpoint = context.GetEndpoint();

        // The schema the endpoint selected, when it selected one: its parameter policies and naming aliases are the
        // ones the validation filter will judge this request by. Resolving by entity would bind with another
        // contract's rules, or throw when the entity has several schemas.
        Type? selected = endpoint?.Metadata.GetMetadata<QueryValidationEndpointMetadata>()?.SchemaType;
        IQuerySchemaParameters? schema = selected is not null
            ? context.RequestServices?.GetService(selected) as IQuerySchemaParameters
            : context.RequestServices?.GetService<QuerySchema<TEntity>>();

        QueryValidationEndpointOptions? endpointOptions = endpoint?.Metadata.GetMetadata<QueryValidationEndpointOptions>();

        QueryRequest request = await QueryRequestBinder.BindAsync(context, schema, endpointOptions).ConfigureAwait(false);
        return new Query<TEntity>(request);
    }

    /// <summary>
    /// Implicitly unwraps to the underlying <see cref="QueryRequest"/> for direct use with
    /// <c>ApplyQuery</c> and other <see cref="QueryRequest"/>-based extension methods.
    /// </summary>
    public static implicit operator QueryRequest(Query<TEntity> query) => query.Value;

    /// <summary>
    /// Implicitly wraps a <see cref="QueryRequest"/>, primarily useful in tests that construct
    /// requests directly without going through HTTP binding.
    /// </summary>
    public static implicit operator Query<TEntity>(QueryRequest request) => new(request);
}