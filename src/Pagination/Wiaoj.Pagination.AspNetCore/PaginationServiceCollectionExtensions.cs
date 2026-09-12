using Microsoft.Extensions.Options;
using Wiaoj.Pagination.AspNetCore;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Registers application-wide pagination settings.
/// </summary>
public static class PaginationServiceCollectionExtensions {
    /// <summary>
    /// Sets the pagination defaults every <c>WithPagination()</c> endpoint uses.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the defaults; omit to register the library defaults explicitly.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Without this, an application wanting ETags off everywhere had to say so at every endpoint, and the next
    /// endpoint written without it silently got the library default instead.
    /// </para>
    /// <para>
    /// Settings layer: library defaults, then this, then an endpoint's own
    /// <c>WithPagination(options =&gt; ...)</c>. Calling this is optional — an application that never does keeps
    /// the library defaults, exactly as before.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddPagination(options => options.EnableETag = false);
    ///
    /// app.MapGet("/a", ...).WithPagination();                                    // ETag off
    /// app.MapGet("/b", ...).WithPagination(options => options.EnableETag = true); // ETag on, here only
    /// </code>
    /// </example>
    public static IServiceCollection AddPagination(
        this IServiceCollection services,
        Action<PaginationOptions>? configure = null) {

        Preca.ThrowIfNull(services);

        OptionsBuilder<PaginationOptions> options = services.AddOptions<PaginationOptions>();

        if(configure is not null) {
            options.Configure(configure);
        }

        return services;
    }
}
