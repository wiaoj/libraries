using Microsoft.AspNetCore.Http;
using System.Reflection;
using Wiaoj.Pagination;
using Wiaoj.Pagination.AspNetCore;
using Wiaoj.Pagination.AspNetCore.Filters;
using Wiaoj.Preconditions;
using Wiaoj.Preconditions.Exceptions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring pagination endpoint filters on route handlers.
/// </summary>
public static class EndpointRouteBuilderExtensions {

    /// <summary>
    /// Adds RFC 8288 <c>Link</c> headers and <c>ETag</c> / <c>304 Not Modified</c> evaluation to an endpoint
    /// returning a <see cref="PagedResult{T}"/> or <see cref="CursorResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// Uses the application's settings from <c>services.AddPagination(...)</c>, or the library defaults when
    /// that was never called. Reuses a shared filter instance.
    /// </remarks>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <returns>The endpoint builder for chaining.</returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// app.MapGet("/api/products", async (AppDbContext db, [AsParameters] PageRequest request) =>
    ///     await db.Products.OrderBy(p => p.Id).ToPagedResultAsync(request)).WithPagination();
    /// </code>
    /// </example>
    public static TBuilder WithPagination<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(builder);

        builder.WithMetadata(PaginationEndpointFilter.Default.Metadata);
        builder.AddEndpointFilter(PaginationEndpointFilter.Default);
        return builder;
    }

    /// <summary>
    /// Adds pagination to an endpoint, refining the application's settings for this endpoint only.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="configureOptions">
    /// Applied on top of the application's settings from <c>services.AddPagination(...)</c> — not on top of
    /// fresh defaults — so an endpoint states only what makes it different.
    /// </param>
    /// <returns>The endpoint builder for chaining.</returns>
    /// <exception cref="PrecaArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// app.MapGet("/api/orders", ...).WithPagination(options => options.EnableETag = false);
    /// </code>
    /// </example>
    public static TBuilder WithPagination<TBuilder>(
        this TBuilder builder,
        Action<PaginationOptions> configureOptions) where TBuilder : IEndpointConventionBuilder {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configureOptions);

        PaginationEndpointMetadata metadata = new(configureOptions);

        builder.WithMetadata(metadata);
        builder.AddEndpointFilter(new PaginationEndpointFilter(metadata));
        return builder;
    }

    /// <summary>
    /// Adds offset pagination to an endpoint whose response carries a page alongside other data.
    /// </summary>
    /// <typeparam name="TResponse">The response type the handler returns.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="metadata">Reads the page metadata from the response.</param>
    /// <param name="configureOptions">Refines the application's settings for this endpoint, if given.</param>
    /// <returns>The route handler builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// A paged response is often more than the page — a workspace view carrying summaries beside the rows.
    /// It cannot be a <see cref="PagedResult{T}"/>, and it should not have to implement a library interface to
    /// be recognised: the response type is the application's contract, and how it is paginated is a fact about
    /// the endpoint. So the endpoint says where the metadata is, and the response stays a plain record.
    /// </para>
    /// <para>
    /// The declaration is checked when the endpoint is built. A <typeparamref name="TResponse"/> that does not
    /// appear in the handler's return type throws there, rather than leaving an accessor that never runs and an
    /// endpoint that silently sends no <c>Link</c> header. A handler returning an opaque <see cref="IResult"/>
    /// cannot be checked and is accepted.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// app.MapGet("api/v1/applications/{applicationId}/workspace", Handle)
    ///    .WithPagination&lt;ApplicationWorkspaceResponse&gt;(response => response.Metadata);
    /// </code>
    /// </example>
    public static RouteHandlerBuilder WithPagination<TResponse>(
        this RouteHandlerBuilder builder,
        Func<TResponse, PageMetadata> metadata,
        Action<PaginationOptions>? configureOptions = null) {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(metadata);

        return AddEnvelope<TResponse>(
            builder,
            PaginationStyle.Offset,
            value => value is TResponse response ? metadata(response) : null,
            configureOptions);
    }

    /// <summary>
    /// Adds keyset pagination to an endpoint whose response carries a window alongside other data.
    /// </summary>
    /// <typeparam name="TResponse">The response type the handler returns.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="metadata">Reads the cursor metadata from the response.</param>
    /// <param name="configureOptions">Refines the application's settings for this endpoint, if given.</param>
    /// <returns>The route handler builder for chaining.</returns>
    /// <remarks>See the offset overload for why the endpoint, not the response type, declares this.</remarks>
    public static RouteHandlerBuilder WithPagination<TResponse>(
        this RouteHandlerBuilder builder,
        Func<TResponse, CursorMetadata> metadata,
        Action<PaginationOptions>? configureOptions = null) {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(metadata);

        return AddEnvelope<TResponse>(
            builder,
            PaginationStyle.Cursor,
            value => value is TResponse response ? metadata(response) : null,
            configureOptions);
    }

    private static RouteHandlerBuilder AddEnvelope<TResponse>(
        RouteHandlerBuilder builder,
        PaginationStyle style,
        Func<object, object?> readMetadata,
        Action<PaginationOptions>? configureOptions) {

        PaginationEndpointMetadata metadata = new(configureOptions, style, typeof(TResponse), readMetadata);
        PaginationEndpointFilter filter = new(metadata);

        builder.WithMetadata(metadata);
        builder.AddEndpointFilterFactory((context, next) => {
            EnsureHandlerReturns(context.MethodInfo, typeof(TResponse));
            return invocation => filter.InvokeAsync(invocation, next);
        });

        return builder;
    }

    /// <summary>
    /// Fails the endpoint build when the declared response type cannot be what the handler returns.
    /// </summary>
    private static void EnsureHandlerReturns(MethodInfo handler, Type declared) {
        Type returned = handler.ReturnType;

        if(IsOpaque(returned) || Mentions(returned, declared, depth: 0)) {
            return;
        }

        throw new InvalidOperationException(
            $"WithPagination<{declared.Name}> was declared on '{handler.Name}', which returns " +
            $"{returned.Name}. {declared.Name} does not appear in that return type, so the metadata accessor " +
            "would never run and the endpoint would send no Link header or ETag. Declare the type the handler " +
            "actually returns.");
    }

    private static bool Mentions(Type type, Type target, int depth) {
        if(depth > 8) {
            return false;
        }

        if(target.IsAssignableFrom(type)) {
            return true;
        }

        if(type.IsArray) {
            return Mentions(type.GetElementType()!, target, depth + 1);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(argument => Mentions(argument, target, depth + 1));
    }

    /// <summary>A return type that says nothing about the body, and so cannot be checked.</summary>
    private static bool IsOpaque(Type type) {
        while(type.IsGenericType &&
              (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>))) {
            type = type.GetGenericArguments()[0];
        }

        return type == typeof(object) || type == typeof(IResult) || type == typeof(Task) || type == typeof(ValueTask);
    }
}
