using System.Net.Http.Headers;

namespace Wiaoj.Webhooks.Internal;

internal static class HttpHeaderExtensions {
    /// <summary>
    /// Safely parses the standard HTTP 'Retry-After' header (either as delta seconds or as an HTTP date).
    /// </summary>
    public static TimeSpan? ExtractRetryAfter(this HttpResponseHeaders headers, TimeProvider timeProvider) {
        if(headers.RetryAfter is null) {
            return null;
        }

        if(headers.RetryAfter.Delta.HasValue) {
            return headers.RetryAfter.Delta.Value;
        }

        if(headers.RetryAfter.Date.HasValue) {
            TimeSpan delta = headers.RetryAfter.Date.Value - timeProvider.GetUtcNow();
            return delta > TimeSpan.Zero ? delta : null;
        }

        return null;
    }
}