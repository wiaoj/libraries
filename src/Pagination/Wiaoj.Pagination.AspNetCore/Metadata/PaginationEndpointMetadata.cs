using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using Wiaoj.Preconditions;

namespace Wiaoj.Pagination.AspNetCore;

/// <summary>
/// Marks an endpoint as paginated, and records how it was configured.
/// </summary>
/// <remarks>
/// <para>
/// <c>WithPagination()</c> installs an endpoint filter, and a filter leaves no trace on the endpoint itself.
/// Anything reading endpoint metadata afterwards — OpenAPI document generation above all — therefore could
/// not tell a paginated endpoint from any other, nor whether it emits <c>Link</c> headers or evaluates
/// <c>ETag</c>s. This says so.
/// </para>
/// <para>
/// The filter reads its settings from this same instance. The document and the behaviour are resolved by one
/// piece of code from one object, so they cannot disagree about whether an ETag is sent.
/// </para>
/// </remarks>
public sealed class PaginationEndpointMetadata {
    private static readonly PaginationOptions LibraryDefaults = new();

    private readonly PaginationOptions? _fixedOptions;
    private readonly Action<PaginationOptions>? _configure;
    private readonly ConditionalWeakTable<PaginationOptions, PaginationOptions> _refined = new();

    /// <summary>
    /// Initializes a new instance fixed to <paramref name="options"/>, ignoring application-wide settings.
    /// </summary>
    /// <param name="options">The options the endpoint uses.</param>
    public PaginationEndpointMetadata(PaginationOptions options) {
        Preca.ThrowIfNull(options);
        this._fixedOptions = options;
        this.Options = options;
    }

    internal PaginationEndpointMetadata(
        Action<PaginationOptions>? configure,
        PaginationStyle? style = null,
        Type? responseType = null,
        Func<object, object?>? readMetadata = null) {

        this._configure = configure;
        this.Style = style;
        this.ResponseType = responseType;
        this.ReadMetadata = readMetadata;

        PaginationOptions mapTime = new();
        configure?.Invoke(mapTime);
        this.Options = mapTime;
    }

    /// <summary>
    /// Gets the options as configured on the endpoint alone, without application-wide settings.
    /// </summary>
    /// <remarks>Use <see cref="Resolve"/> for the options actually in effect.</remarks>
    public PaginationOptions Options { get; }

    /// <summary>Gets a value indicating whether the endpoint, on its own configuration, emits <c>Link</c> headers.</summary>
    public bool EmitsLinkHeaders => this.Options.EnableLinkHeaders;

    /// <summary>Gets a value indicating whether the endpoint, on its own configuration, evaluates <c>ETag</c>s.</summary>
    public bool EvaluatesETag => this.Options.EnableETag;

    /// <summary>
    /// Gets the paging style the endpoint declared, or <see langword="null"/> when it is inferred from the
    /// response type.
    /// </summary>
    public PaginationStyle? Style { get; }

    /// <summary>
    /// Gets the response type whose metadata the endpoint declared a way to read, or <see langword="null"/>
    /// when the response is a <see cref="PagedResult{T}"/> or <see cref="CursorResult{T}"/> read directly.
    /// </summary>
    public Type? ResponseType { get; }

    /// <summary>Reads <see cref="PageMetadata"/> or <see cref="CursorMetadata"/> from an envelope response.</summary>
    internal Func<object, object?>? ReadMetadata { get; }

    /// <summary>
    /// Resolves the options in effect: library defaults, then <c>AddPagination</c>, then this endpoint's own.
    /// </summary>
    /// <param name="services">The application's services; <see langword="null"/> uses no application settings.</param>
    /// <returns>The effective options. Do not modify the instance returned.</returns>
    public PaginationOptions Resolve(IServiceProvider? services) {
        if(this._fixedOptions is not null) {
            return this._fixedOptions;
        }

        PaginationOptions? application = services?.GetService<IOptions<PaginationOptions>>()?.Value;

        if(this._configure is null) {
            return application ?? LibraryDefaults;
        }

        if(application is null) {
            return this.Options;
        }

        // Keyed on the application's options instance, so two hosts in one process — every integration test
        // suite — never see each other's settings, and an endpoint refines each application's options once.
        return this._refined.GetValue(application, Refine);
    }

    private PaginationOptions Refine(PaginationOptions application) {
        PaginationOptions refined = application.Clone();
        this._configure!(refined);
        return refined;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return $"Pagination(Link={this.EmitsLinkHeaders}, ETag={this.EvaluatesETag}, Style={this.Style?.ToString() ?? "inferred"})";
    }
}
