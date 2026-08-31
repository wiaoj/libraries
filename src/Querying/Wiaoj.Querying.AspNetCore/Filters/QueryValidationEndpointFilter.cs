using Microsoft.AspNetCore.Http;
using Wiaoj.Preconditions;

namespace Wiaoj.Querying.AspNetCore;

/// <summary>
/// An internal endpoint filter that validates an already-bound <see cref="Query{T}"/> argument against a
/// <see cref="QuerySchema{T}"/>, automatically producing an RFC 7807 <c>ValidationProblem</c>
/// (HTTP 400 Bad Request) on schema rule violations.
/// </summary>
/// <typeparam name="T">The entity type of the query schema.</typeparam>
/// <remarks>
/// This filter does <b>not</b> perform binding itself — that already happened via
/// <see cref="Query{T}.BindAsync"/> before the filter pipeline runs. It only locates the bound
/// <see cref="Query{T}"/> argument in <see cref="EndpointFilterInvocationContext.Arguments"/> and validates it.
/// </remarks>
internal sealed class QueryValidationEndpointFilter<T> : IEndpointFilter {
    private readonly QuerySchema<T> _schema;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryValidationEndpointFilter{T}"/> class.
    /// </summary>
    /// <param name="schema">The schema rules to enforce.</param>
    public QueryValidationEndpointFilter(QuerySchema<T> schema) {
        Preca.ThrowIfNull(schema);
        this._schema = schema;
    }

    /// <inheritdoc/>
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

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

        QueryValidationResult validation = this._schema.Validate(request.Value);
        if(!validation.IsValid) {
            return ValueTask.FromResult<object?>(Results.ValidationProblem(
                errors: validation.ToDictionary(),
                title: "One or more query validation errors occurred.",
                statusCode: StatusCodes.Status400BadRequest));
        }

        return next(context);
    }
}