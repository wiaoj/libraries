using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Wiaoj.Net;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Internal diagnostic extensions for analyzing, unwrapping, and categorizing exception hierarchies in the webhook pipeline.
/// </summary>
internal static class WebhookExceptionExtensions {
    /// <summary>
    /// Determines whether the exception or any exception in its causal chain is an SSRF refusal.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <returns><see langword="true"/> if the delivery was refused by SSRF protection; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSsrfBlocked(this Exception exception) {
        Preca.ThrowIfNull(exception);
        return TryGetSsrfRefusal(exception, out _);
    }

    /// <summary>
    /// Attempts to locate an SSRF refusal in the root, base, or inner exception chain: an
    /// <see cref="OutboundNetworkPolicyException"/> raised while connecting, or a <see cref="WebhookSsrfBlockedException"/>.
    /// </summary>
    /// <remarks>
    /// Unwraps standard .NET exceptions such as <see cref="HttpRequestException"/> (which carries a connection refusal as its
    /// inner exception) and <see cref="AggregateException"/>.
    /// </remarks>
    /// <param name="exception">The exception to traverse.</param>
    /// <param name="refusal">When this method returns <see langword="true"/>, the refusal that was found.</param>
    /// <returns><see langword="true"/> if an SSRF refusal was located in the causal chain; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <see langword="null"/>.</exception>
    public static bool TryGetSsrfRefusal(
        this Exception exception,
        [NotNullWhen(true)] out Exception? refusal) {
        Preca.ThrowIfNull(exception);

        Exception? current = exception;
        while(current is not null) {
            if(current is OutboundNetworkPolicyException or WebhookSsrfBlockedException) {
                refusal = current;
                return true;
            }

            if(current is AggregateException aggregate && aggregate.InnerExceptions.Count > 0) {
                for(int i = 0; i < aggregate.InnerExceptions.Count; i++) {
                    if(TryGetSsrfRefusal(aggregate.InnerExceptions[i], out refusal)) {
                        return true;
                    }
                }
            }

            current = current.InnerException;
        }

        refusal = null;
        return false;
    }
}