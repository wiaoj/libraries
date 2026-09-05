namespace Wiaoj.Querying;

/// <summary>
/// Defines parameter policy inspection for query schemas.
/// </summary>
public interface IQuerySchemaParameters {
    /// <summary>
    /// Gets a value indicating whether this schema ignores global parameters from <see cref="QueryOptions"/>.
    /// </summary>
    bool IgnoresGlobalParameters { get; }

    /// <summary>
    /// Determines whether the specified parameter name is configured to be ignored by this schema.
    /// </summary>
    /// <param name="parameterName">The parameter name to check.</param>
    /// <returns><see langword="true"/> if the parameter is ignored; otherwise, <see langword="false"/>.</returns>
    bool IsParameterIgnored(string parameterName);

    /// <summary>
    /// Determines whether the specified parameter name is explicitly allowed (un-ignored) by this schema.
    /// </summary>
    /// <param name="parameterName">The parameter name to check.</param>
    /// <returns><see langword="true"/> if the parameter is explicitly allowed; otherwise, <see langword="false"/>.</returns>
    bool IsParameterAllowed(string parameterName);
}
