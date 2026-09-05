using Wiaoj.Preconditions;

namespace Wiaoj.Querying.AspNetCore;

/// <summary>
/// Configures endpoint-level query validation and parameter ignoring policies for ASP.NET Core route handlers.
/// </summary>
public sealed class QueryValidationEndpointOptions {
    /// <summary>
    /// Gets the collection of query parameter names to ignore for this endpoint.
    /// Ignored parameters will not produce validation errors and will not be bound as query filters.
    /// </summary>
    public HashSet<string> IgnoredParameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the collection of query parameter names to explicitly allow (un-ignore) for this endpoint.
    /// Overrides any global or inherited parameter ignoring rules.
    /// </summary>
    public HashSet<string> AllowedParameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets a value indicating whether this endpoint bypasses all globally configured ignored parameters from <see cref="QueryOptions"/>.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool IgnoresGlobalParameters { get; set; }

    /// <summary>
    /// Configures this endpoint to bypass all globally configured ignored parameters from <see cref="QueryOptions"/>.
    /// </summary>
    /// <returns>The current options instance for method chaining.</returns>
    public QueryValidationEndpointOptions IgnoreGlobalParameters() => IgnoreGlobalParameters(true);

    /// <summary>
    /// Configures whether this endpoint bypasses all globally configured ignored parameters from <see cref="QueryOptions"/>.
    /// </summary>
    /// <param name="ignore"><see langword="true"/> to ignore global parameters; otherwise, <see langword="false"/>.</param>
    /// <returns>The current options instance for method chaining.</returns>
    public QueryValidationEndpointOptions IgnoreGlobalParameters(bool ignore) {
        this.IgnoresGlobalParameters = ignore;
        return this;
    }

    /// <summary>
    /// Configures one or more parameter names to be ignored for this endpoint during query binding and validation.
    /// </summary>
    /// <param name="parameters">The parameter names to ignore.</param>
    /// <returns>The current options instance for method chaining.</returns>
    public QueryValidationEndpointOptions IgnoreParameters(params ReadOnlySpan<string> parameters) {
        for(int i = 0; i < parameters.Length; i++) {
            string? param = parameters[i];
            if(!string.IsNullOrWhiteSpace(param)) {
                string trimmed = param.Trim();
                this.IgnoredParameters.Add(trimmed);
                this.AllowedParameters.Remove(trimmed);
            }
        }
        return this;
    }

    /// <summary>
    /// Configures parameter names to be ignored for this endpoint during query binding and validation.
    /// </summary>
    /// <param name="parameters">The collection of parameter names to ignore.</param>
    /// <returns>The current options instance for method chaining.</returns>
    public QueryValidationEndpointOptions IgnoreParameters(IEnumerable<string> parameters) {
        Preca.ThrowIfNull(parameters);
        foreach(string? param in parameters) {
            if(!string.IsNullOrWhiteSpace(param)) {
                string trimmed = param.Trim();
                this.IgnoredParameters.Add(trimmed);
                this.AllowedParameters.Remove(trimmed);
            }
        }
        return this;
    }

    /// <summary>
    /// Configures one or more parameter names to be explicitly allowed (un-ignored) for this endpoint.
    /// This overrides any global or schema-level parameter ignoring rules.
    /// </summary>
    /// <param name="parameters">The parameter names to allow.</param>
    /// <returns>The current options instance for method chaining.</returns>
    public QueryValidationEndpointOptions AllowParameter(params ReadOnlySpan<string> parameters) => AllowParameters(parameters);

    /// <summary>
    /// Configures one or more parameter names to be explicitly allowed (un-ignored) for this endpoint.
    /// This overrides any global or schema-level parameter ignoring rules.
    /// </summary>
    /// <param name="parameters">The parameter names to allow.</param>
    /// <returns>The current options instance for method chaining.</returns>
    public QueryValidationEndpointOptions AllowParameters(params ReadOnlySpan<string> parameters) {
        for(int i = 0; i < parameters.Length; i++) {
            string? param = parameters[i];
            if(!string.IsNullOrWhiteSpace(param)) {
                string trimmed = param.Trim();
                this.AllowedParameters.Add(trimmed);
                this.IgnoredParameters.Remove(trimmed);
            }
        }
        return this;
    }

    /// <summary>
    /// Configures parameter names to be explicitly allowed (un-ignored) for this endpoint.
    /// This overrides any global or schema-level parameter ignoring rules.
    /// </summary>
    /// <param name="parameters">The collection of parameter names to allow.</param>
    /// <returns>The current options instance for method chaining.</returns>
    public QueryValidationEndpointOptions AllowParameters(IEnumerable<string> parameters) {
        Preca.ThrowIfNull(parameters);
        foreach(string? param in parameters) {
            if(!string.IsNullOrWhiteSpace(param)) {
                string trimmed = param.Trim();
                this.AllowedParameters.Add(trimmed);
                this.IgnoredParameters.Remove(trimmed);
            }
        }
        return this;
    }
}
