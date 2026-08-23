using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Webhooks.Retries;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring backoff retry policies and resilience middleware on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderRetryExtensions {
    /// <summary>
    /// Configures exponential backoff retry strategy with default settings (5 attempts, 2s initial delay, medium jitter) and registers <see cref="RetryMiddleware"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseExponentialBackoffRetry(this IWebhookBuilder builder) {
        return UseExponentialBackoffRetry(builder, new ExponentialBackoffOptions());
    }

    /// <summary>
    /// Configures exponential backoff retry strategy with specified options and registers <see cref="RetryMiddleware"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="options">The exponential backoff options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseExponentialBackoffRetry(this IWebhookBuilder builder, ExponentialBackoffOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton<IWebhookRetryPolicy>(new ExponentialBackoffPolicy(options));
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures linear backoff retry strategy and registers <see cref="RetryMiddleware"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="maxAttempts">The maximum total number of delivery attempts.</param>
    /// <param name="initialDelay">The delay before the first retry attempt.</param>
    /// <param name="step">The additional duration added to each subsequent retry delay.</param>
    /// <param name="maxDelay">The maximum delay cap.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is out of valid bounds.</exception>
    public static IWebhookBuilder UseLinearBackoffRetry(this IWebhookBuilder builder, int maxAttempts, TimeSpan initialDelay, TimeSpan step, TimeSpan maxDelay) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton<IWebhookRetryPolicy>(new LinearBackoffPolicy(maxAttempts, initialDelay, step, maxDelay));
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures fixed interval retry strategy and registers <see cref="RetryMiddleware"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="maxAttempts">The maximum total number of delivery attempts.</param>
    /// <param name="interval">The constant delay interval between retry attempts.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxAttempts"/> is less than 1 or <paramref name="interval"/> is negative.</exception>
    public static IWebhookBuilder UseFixedIntervalRetry(this IWebhookBuilder builder, int maxAttempts, TimeSpan interval) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton<IWebhookRetryPolicy>(new FixedIntervalBackoffPolicy(maxAttempts, interval));
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures a custom retry policy type and registers <see cref="RetryMiddleware"/>.
    /// </summary>
    /// <typeparam name="TPolicy">The type implementing <see cref="IWebhookRetryPolicy"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseRetryPolicy<TPolicy>(this IWebhookBuilder builder) where TPolicy : class, IWebhookRetryPolicy {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton<IWebhookRetryPolicy, TPolicy>();
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures a custom retry policy instance and registers <see cref="RetryMiddleware"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="policy">The retry policy instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="policy"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseRetryPolicy(this IWebhookBuilder builder, IWebhookRetryPolicy policy) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(policy);
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton(policy);
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures a custom retry policy using a factory delegate and registers <see cref="RetryMiddleware"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="implementationFactory">The factory delegate used to resolve the retry policy.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="implementationFactory"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseRetryPolicy(this IWebhookBuilder builder, Func<IServiceProvider, IWebhookRetryPolicy> implementationFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(implementationFactory);
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton(implementationFactory);
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }
}