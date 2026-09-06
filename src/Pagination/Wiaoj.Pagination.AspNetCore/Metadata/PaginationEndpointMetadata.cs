using Wiaoj.Preconditions;

namespace Wiaoj.Pagination.AspNetCore;

/// <summary>
/// Marks an endpoint as paginated, and records the options it was configured with.
/// </summary>
/// <remarks>
/// <para>
/// <c>WithPagination()</c> installs an endpoint filter, and a filter leaves no trace on the endpoint itself.
/// Anything reading endpoint metadata afterwards — OpenAPI document generation above all — therefore could
/// not tell a paginated endpoint from any other, nor whether it emits <c>Link</c> headers or evaluates
/// <c>ETag</c>s. This says so.
/// </para>
/// </remarks>
public sealed class PaginationEndpointMetadata {
    /// <summary>Gets the options the endpoint was configured with.</summary>
    public PaginationOptions Options { get; }

    /// <summary>Gets a value indicating whether the endpoint emits RFC 8288 <c>Link</c> headers.</summary>
    public bool EmitsLinkHeaders => this.Options.EnableLinkHeaders;

    /// <summary>Gets a value indicating whether the endpoint evaluates <c>ETag</c>s and can answer 304.</summary>
    public bool EvaluatesETag => this.Options.EnableETag;

    /// <summary>Initializes a new instance of the <see cref="PaginationEndpointMetadata"/> class.</summary>
    /// <param name="options">The options the endpoint was configured with.</param>
    public PaginationEndpointMetadata(PaginationOptions options) {
        Preca.ThrowIfNull(options);
        this.Options = options;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return $"Pagination(Link={this.EmitsLinkHeaders}, ETag={this.EvaluatesETag})";
    }
}
