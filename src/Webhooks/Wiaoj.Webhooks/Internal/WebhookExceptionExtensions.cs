using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Wiaoj.Webhooks.Exceptions;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Internal diagnostic extensions for analyzing, unwrapping, and categorizing exception hierarchies in the webhook pipeline.
/// </summary>
internal static class WebhookExceptionExtensions {
    /// <summary>
    /// Determines whether the exception or any of its inner exceptions in the causal chain is a <see cref="WebhookSsrfBlockedException"/>.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <returns><see langword="true"/> if a <see cref="WebhookSsrfBlockedException"/> exists in the exception hierarchy; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSsrfBlocked(this Exception exception) {
        Preca.ThrowIfNull(exception);
        return TryGetSsrfException(exception, out _);
    }

    /// <summary>
    /// Attempts to locate and extract a <see cref="WebhookSsrfBlockedException"/> from the root, base, or inner exception chain.
    /// </summary>
    /// <remarks>
    /// Unwraps standard .NET exceptions such as <see cref="HttpRequestException"/> and <see cref="AggregateException"/>
    /// to retrieve the root SSRF security violation without manual type casting.
    /// </remarks>
    /// <param name="exception">The exception to traverse.</param>
    /// <param name="ssrfException">When this method returns, contains the extracted <see cref="WebhookSsrfBlockedException"/> if found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an SSRF exception was located in the causal chain; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <see langword="null"/>.</exception>
    public static bool TryGetSsrfException(
        this Exception exception,
        [NotNullWhen(true)] out WebhookSsrfBlockedException? ssrfException) {
        Preca.ThrowIfNull(exception);

        Exception? current = exception;
        while(current is not null) {
            if(current is WebhookSsrfBlockedException matched) {
                ssrfException = matched;
                return true;
            }

            if(current is AggregateException aggregate && aggregate.InnerExceptions.Count > 0) {
                for(int i = 0; i < aggregate.InnerExceptions.Count; i++) {
                    if(TryGetSsrfException(aggregate.InnerExceptions[i], out ssrfException)) {
                        return true;
                    }
                }
            }

            current = current.InnerException;
        }

        ssrfException = null;
        return false;
    }
}