using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace Wiaoj.WellKnown;

/// <summary>Marks that <c>AddSecurityTxt</c> was called, so mapping without it fails with a clear message.</summary>
internal sealed class SecurityTxtRegistration;

/// <summary>Whether a mapped endpoint has already warned that the file expired while running.</summary>
internal sealed class SecurityTxtExpiredWarning {
    private int _logged;

    public bool TryClaim() => Interlocked.Exchange(ref this._logged, 1) == 0;
}

/// <summary>Serves the file and warns about its expiry.</summary>
internal static partial class SecurityTxtEndpoint {
    public const string LoggerCategory = "Wiaoj.WellKnown.SecurityTxt";

    /// <summary>RFC 9116 §2.5.5 recommends an Expires less than a year ahead.</summary>
    public static readonly TimeSpan RecommendedMaximumLifetime = TimeSpan.FromDays(365);

    /// <summary>Warns this long before Expires, so the date is renewed before researchers see a stale file.</summary>
    public static readonly TimeSpan RenewalWarning = TimeSpan.FromDays(30);

    /// <summary>Logs a warning when <paramref name="expires"/> is further ahead than recommended, or close.</summary>
    public static void WarnAboutExpiry(ILogger logger, DateTimeOffset expires, DateTimeOffset now) {
        TimeSpan remaining = expires - now;
        string formatted = SecurityTxtDocument.FormatExpires(expires);

        if(remaining > RecommendedMaximumLifetime) {
            LogExpiresTooFar(logger, formatted);
        }
        else if(remaining < RenewalWarning) {
            LogExpiresSoon(logger, formatted, Math.Max(0, (int)Math.Ceiling(remaining.TotalDays)));
        }
    }

    public static Task WriteAsync(HttpContext context, SecurityTxtExpiredWarning expiredWarning) {
        IServiceProvider services = context.RequestServices;
        SecurityTxtOptions options = services.GetRequiredService<IOptionsMonitor<SecurityTxtOptions>>().CurrentValue;

        // Startup refuses an expired date; a process that keeps running past it still serves the file — its contacts are
        // the best available — and says so once.
        TimeProvider timeProvider = services.GetService<TimeProvider>() ?? TimeProvider.System;
        if(options.Expires is { } expires && expires <= timeProvider.GetUtcNow() && expiredWarning.TryClaim()) {
            LogExpired(services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory), SecurityTxtDocument.FormatExpires(expires));
        }

        byte[] body = Encoding.UTF8.GetBytes(SecurityTxtDocument.Render(options));

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = SecurityTxtDocument.ContentType;
        context.Response.Headers.CacheControl = options.CacheDuration > TimeSpan.Zero
            ? $"public, max-age={((long)options.CacheDuration.TotalSeconds).ToString(CultureInfo.InvariantCulture)}"
            : "no-cache";
        context.Response.ContentLength = body.Length;
        return context.Response.Body.WriteAsync(body, context.RequestAborted).AsTask();
    }

    public static Task RedirectAsync(HttpContext context) {
        context.Response.Redirect(context.Request.PathBase + SecurityTxtDocument.WellKnownPath, permanent: true);
        return Task.CompletedTask;
    }

    [LoggerMessage(1, LogLevel.Warning, "security.txt Expires {Expires} is more than a year ahead. RFC 9116 recommends less than a year, so the file is reviewed regularly.")]
    private static partial void LogExpiresTooFar(ILogger logger, string expires);

    [LoggerMessage(2, LogLevel.Warning, "security.txt Expires {Expires} is {Days} days away. Review the file and set a new date before it expires, or the application will not start.")]
    private static partial void LogExpiresSoon(ILogger logger, string expires, int days);

    [LoggerMessage(3, LogLevel.Warning, "security.txt Expires {Expires} has passed while the application was running; researchers will treat the file as stale. Set a new date.")]
    private static partial void LogExpired(ILogger logger, string expires);
}
