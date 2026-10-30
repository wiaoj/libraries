using System.Diagnostics.Contracts;

namespace Wiaoj.Results.AspNetCore;

/// <summary>
/// How an <see cref="Error"/> is projected into the RFC 9457 <c>type</c> and <c>title</c> members.
/// </summary>
public enum ProblemTypeStyle {

    /// <summary>
    /// <c>{TypeNamespace}/{error.Code}</c> when a namespace is configured, otherwise <c>about:blank</c>.
    /// </summary>
    /// <remarks>
    /// RFC 9457 §3.1.1 wants <c>type</c> to identify the problem type and to be under the control of whoever
    /// produced the problem, and it names <c>about:blank</c> as the value for "no type URI is available".
    /// Both halves are reachable here, and neither hands a producer's error taxonomy to a third party.
    /// </remarks>
    ProducerNamespaceOrAboutBlank,

    /// <summary>
    /// <c>https://httpstatuses.io/{status}</c> — the pre-9457 projection, kept as an escape hatch.
    /// </summary>
    /// <remarks>
    /// It is the same value for every error sharing a status code, so it carries no machine identity, and it points
    /// at a site nobody in this ecosystem operates. Use it only to pin the old body while migrating: the
    /// <see cref="Error.Code"/> also travels in the <c>code</c> extension member, so a consumer that read the old
    /// <c>title</c> for the code does not need this style.
    /// </remarks>
    HttpStatusIo
}

/// <summary>
/// The rules that turn an <see cref="Error"/> into the <c>type</c> and <c>title</c> members of an
/// <c>application/problem+json</c> body.
/// </summary>
/// <remarks>
/// <para>
/// An instance, passed per call — <see cref="Default"/> is the ambient one. That split exists because a filter deep
/// inside a mapping chain cannot receive a per-call argument, while a test that has to reach those same fields does
/// not want to edit a value every other test is reading.
/// </para>
/// <para>
/// The defaults need no configuration and are RFC 9457-shaped: <c>type</c> is <c>about:blank</c> until a producer
/// sets <see cref="TypeNamespace"/>, <c>title</c> is stable per error type, and the machine-readable
/// <see cref="Error.Code"/> travels in the <c>code</c> extension member instead of squatting in <c>title</c>.
/// </para>
/// </remarks>
public sealed record ProblemDetailsProjection {

    /// <summary>
    /// The projection used when a call site does not pass one. Set it once at startup:
    /// <c>ProblemDetailsProjection.Default = new() { TypeNamespace = "https://errors.example.com" };</c>
    /// </summary>
    /// <remarks>
    /// Assigning this changes what every later projection answers, so a test that sets it has to keep it in the same
    /// xUnit collection as the tests reading the default — that is the discipline behind the
    /// <c>[Collection("ProblemDetailsProjection")]</c> tags in the Results tests.
    /// </remarks>
    public static ProblemDetailsProjection Default { get; set; } = new();

    /// <summary>
    /// Which projection to use for the <c>type</c> member. Defaults to
    /// <see cref="ProblemTypeStyle.ProducerNamespaceOrAboutBlank"/>.
    /// </summary>
    public ProblemTypeStyle TypeStyle { get; init; } = ProblemTypeStyle.ProducerNamespaceOrAboutBlank;

    /// <summary>
    /// The base the machine-readable <see cref="Error.Code"/> is appended to when <see cref="TypeStyle"/> is
    /// <see cref="ProblemTypeStyle.ProducerNamespaceOrAboutBlank"/>. Example: <c>https://errors.prism.dev</c> turns
    /// <c>"User.NotFound"</c> into <c>https://errors.prism.dev/User.NotFound</c>. Left null, <c>type</c> is
    /// <c>about:blank</c>.
    /// </summary>
    public string? TypeNamespace { get; init; }

    /// <summary>
    /// Human summaries keyed by <see cref="ErrorType.Name"/>, overriding the default of the name itself.
    /// RFC 9457 §3.2 expects <c>title</c> to repeat for every occurrence of a problem type, so an entry here must
    /// not vary per instance.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Titles { get; init; }

    /// <summary>
    /// Builds the <c>type</c> member for a projected error.
    /// </summary>
    [Pure]
    public string BuildType(Error error, int statusCode) {
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
    public string BuildTitle(Error error) {
        string typeName = error.Type.Name;

        if(Titles is not null && Titles.TryGetValue(typeName, out string? title) && !string.IsNullOrWhiteSpace(title))
            return title;

        return typeName;
    }
}
