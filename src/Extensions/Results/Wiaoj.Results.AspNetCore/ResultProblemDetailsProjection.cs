using System.Diagnostics.Contracts;

namespace Wiaoj.Results.AspNetCore;

/// <summary>
/// How <see cref="Error"/> is projected into the RFC 9457 <c>type</c> member.
/// </summary>
public enum ProblemTypeStyle {

    /// <summary>
    /// <c>{TypeNamespace}/{error.Code}</c> when a namespace is configured, otherwise <c>about:blank</c>.
    /// </summary>
    /// <remarks>
    /// RFC 9457 §3.1.1 wants <c>type</c> to identify the problem type and to be under the control of whoever
    /// produced the problem, and it blesses <c>about:blank</c> as the value for "no type URI is available".
    /// Both halves of that are reachable here, and neither hands a producer's error taxonomy to a third party.
    /// </remarks>
    ProducerNamespaceOrAboutBlank,

    /// <summary>
    /// <c>https://httpstatuses.io/{status}</c> — the pre-9457 projection, kept as an escape hatch.
    /// </summary>
    /// <remarks>
    /// It is the same value for every error that shares a status code, so it carries no machine identity, and it
    /// points at a site nobody in this ecosystem operates. Use it only to pin the old body while migrating;
    /// <see cref="Error.Code"/> also lands in the <c>code</c> extension member, so a consumer that read the old
    /// <c>title</c> for the code does not need this style.
    /// </remarks>
    HttpStatusIo
}

/// <summary>
/// The process-wide projection rules that turn an <see cref="Error"/> into the members of a
/// <c>application/problem+json</c> body.
/// </summary>
/// <remarks>
/// <para>
/// Static and mutable, the same shape as <see cref="ErrorTypeHttpMapper.OnUnmapped"/>, because an endpoint filter
/// deep inside a mapping chain has no way to receive a per-call options argument. Configure it once at startup.
/// </para>
/// <para>
/// The defaults are RFC 9457-shaped and need no configuration: <c>type</c> is <c>about:blank</c> until a producer
/// sets <see cref="TypeNamespace"/>, <c>title</c> is stable per error type, and the machine-readable
/// <see cref="Error.Code"/> travels in the <c>code</c> extension member instead of squatting in <c>title</c>.
/// </para>
/// </remarks>
public static class ResultProblemDetailsProjection {

    /// <summary>
    /// Which projection to use for the <c>type</c> member. Defaults to
    /// <see cref="ProblemTypeStyle.ProducerNamespaceOrAboutBlank"/>.
    /// </summary>
    public static ProblemTypeStyle TypeStyle { get; set; } = ProblemTypeStyle.ProducerNamespaceOrAboutBlank;

    /// <summary>
    /// The base the machine-readable <see cref="Error.Code"/> is appended to when
    /// <see cref="TypeStyle"/> is <see cref="ProblemTypeStyle.ProducerNamespaceOrAboutBlank"/>.
    /// Example: <c>https://errors.example.dev</c> turns <c>"User.NotFound"</c> into
    /// <c>https://errors.example.dev/User.NotFound</c>. Left null, <c>type</c> is <c>about:blank</c>.
    /// </summary>
    public static string? TypeNamespace { get; set; }

    /// <summary>
    /// Human summaries keyed by <see cref="ErrorType.Name"/>, overriding the default of the name itself.
    /// RFC 9457 §3.2 expects <c>title</c> to be the same for every occurrence of a problem type, so an entry
    /// here must not vary per instance.
    /// </summary>
    public static IDictionary<string, string> Titles { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Builds the <c>type</c> member for a projected error.
    /// </summary>
    [Pure]
    public static string BuildType(Error error, int statusCode) {
        if(TypeStyle == ProblemTypeStyle.HttpStatusIo)
            return $"https://httpstatuses.io/{statusCode}";

        string? ns = TypeNamespace;

        if(string.IsNullOrWhiteSpace(ns))
            return "about:blank";

        return $"{ns.TrimEnd('/')}/{error.Code}";
    }

    /// <summary>
    /// Builds the <c>title</c> member for a projected error: the configured summary for its
    /// <see cref="ErrorType"/>, or that type's name when nothing is configured.
    /// </summary>
    [Pure]
    public static string BuildTitle(Error error) {
        string typeName = error.Type.Name;

        return Titles.TryGetValue(typeName, out string? title) && !string.IsNullOrWhiteSpace(title)
            ? title
            : typeName;
    }
}
