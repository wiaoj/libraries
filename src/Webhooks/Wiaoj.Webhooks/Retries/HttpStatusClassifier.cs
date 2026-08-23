namespace Wiaoj.Webhooks.Retries;

/// <summary>
/// Provides utility methods to classify HTTP status codes into transient (retryable) and non-transient (permanent) errors.
/// </summary>
public static class HttpStatusClassifier {
    /// <summary>
    /// Determines whether the given HTTP status code represents a transient error that can potentially succeed on retry.
    /// </summary>
    /// <param name="statusCode">The HTTP status code, or <see langword="null"/> if a network/timeout failure occurred before a response was received.</param>
    /// <returns><see langword="true"/> if the error is transient; otherwise, <see langword="false"/>.</returns>
    public static bool IsTransient(int? statusCode) {
        if(!statusCode.HasValue) {
            // Network connection refused, DNS resolution failure, TCP reset, or TLS handshake error
            return true;
        }

        int code = statusCode.Value;

        // Rate limiting and timeouts
        if(code is 408 or 429) {
            return true;
        }

        // Server errors (5xx)
        if(code is >= 500 and <= 599) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the given HTTP status code represents a permanent failure that should not be retried.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns><see langword="true"/> if the error is permanent; otherwise, <see langword="false"/>.</returns>
    public static bool IsPermanentFailure(int? statusCode) {
        return !IsTransient(statusCode);
    }
}