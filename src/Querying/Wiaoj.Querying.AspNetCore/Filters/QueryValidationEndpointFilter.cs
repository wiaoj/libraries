using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.Preconditions;

namespace Wiaoj.Querying.AspNetCore;

/// <summary>
/// Endpoint filter that validates an already-bound <see cref="Query{T}"/> argument against a
/// <see cref="QuerySchema{T}"/> and automatically produces an RFC 7807 <c>ValidationProblem</c>
/// response (HTTP 400 Bad Request) on schema rule violations.
/// </summary>
/// <typeparam name="T">The entity type of the query schema.</typeparam>
internal sealed class QueryValidationEndpointFilter<T> : IEndpointFilter {
    private const string AcceptQueryHeader = "Accept-Query";
    private const string SupportedQueryMediaTypes = "text/plain, application/x-www-form-urlencoded";

    private readonly QuerySchema<T> _schema;
    private readonly QueryValidationEndpointOptions? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryValidationEndpointFilter{T}"/> class.
    /// </summary>
    /// <param name="schema">The schema rules to enforce.</param>
    /// <param name="options">Optional endpoint-specific validation and parameter options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="schema"/> is <see langword="null"/>.</exception>
    public QueryValidationEndpointFilter(QuerySchema<T> schema, QueryValidationEndpointOptions? options = null) {
        Preca.ThrowIfNull(schema);
        this._schema = schema;
        this._options = options;
    }

    /// <inheritdoc/>
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        context.HttpContext.Response.Headers[AcceptQueryHeader] = SupportedQueryMediaTypes;

        QueryRequest? request = null;
        for(int i = 0; i < context.Arguments.Count; i++) {
            if(context.Arguments[i] is Query<T> boundQuery) {
                request = boundQuery.Value;
                break;
            }
        }

        if(request is null) {
            throw new InvalidOperationException(
                $"WithQueryValidation<{typeof(T).Name}>() requires an endpoint parameter of type " +
                $"Query<{typeof(T).Name}>, but none was found in the handler signature.");
        }

        QueryValidationEndpointOptions? endpointOpts = this._options ?? context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<QueryValidationEndpointOptions>();
        QueryOptions? globalOptions = context.HttpContext.RequestServices?.GetService<IOptions<QueryOptions>>()?.Value;

        bool ignoresGlobal = (endpointOpts?.IgnoresGlobalParameters == true) || this._schema.IgnoresGlobalParameters;
        QueryOptions? effectiveGlobal = ignoresGlobal ? null : globalOptions;

        QueryValidationResult validation = this._schema.Validate(request.Value, effectiveGlobal);
        if(!validation.IsValid && endpointOpts?.IgnoredParameters is { Count: > 0 }) {
            List<QueryValidationError> remaining = [];
            for(int i = 0; i < validation.Errors.Count; i++) {
                QueryValidationError err = validation.Errors[i];
                if(err.PropertyName is not null && endpointOpts.IgnoredParameters.Contains(err.PropertyName)) {
                    continue;
                }
                remaining.Add(err);
            }

            if(remaining.Count == 0) {
                validation = QueryValidationResult.Success;
            }
            else if(remaining.Count != validation.Errors.Count) {
                validation = new QueryValidationResult(remaining);
            }
        }

        if(!validation.IsValid) {
            return ValueTask.FromResult<object?>(Problem(validation));
        }

        return InvokeHandlerAsync(context, next);
    }

    /// <summary>
    /// Runs the handler, answering a <see cref="QueryValidationException"/> it throws the way an invalid query string is
    /// answered.
    /// </summary>
    /// <remarks>
    /// Some of what makes a query invalid is only known once the handler applies it — a cursor issued for a different
    /// sort, or a sort on a field the endpoint cannot page by. Those are the caller's to fix, like any other invalid
    /// query, and deserve the same 400 with the same error shape rather than a 500.
    /// </remarks>
    private static async ValueTask<object?> InvokeHandlerAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        try {
            return await next(context).ConfigureAwait(false);
        }
        catch(QueryValidationException error) {
            return Problem(error.Result);
        }
    }

    private static IResult Problem(QueryValidationResult validation) {
        return Results.ValidationProblem(
            errors: validation.ToDictionary(),
            title: "One or more query validation errors occurred.",
            statusCode: StatusCodes.Status400BadRequest);
    }
}