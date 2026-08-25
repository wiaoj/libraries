using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter.Hosting;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.DistributedCounter;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring distributed counter options, background workers, and counter registrations on <see cref="IDistributedCounterBuilder"/>.
/// </summary>
public static class DistributedCounterBuilderExtensions {

    /// <summary>
    /// Configures global options for the distributed counter engine.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="configure">The delegate used to configure <see cref="DistributedCounterOptions"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder Configure(
        this IDistributedCounterBuilder builder,
        Action<DistributedCounterOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Enables the background periodic auto-flush service for buffered counters.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddAutoFlush(this IDistributedCounterBuilder builder) {
        Preca.ThrowIfNull(builder);

        builder.Services.AddHostedService<CounterAutoFlushService>(sp => {
            IDistributedCounterFactory factory = sp.GetRequiredService<IDistributedCounterFactory>();
            IOptions<DistributedCounterOptions> options = sp.GetRequiredService<IOptions<DistributedCounterOptions>>();
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            ILogger<CounterAutoFlushService> logger = sp.GetService<ILogger<CounterAutoFlushService>>() 
                ?? NullLogger<CounterAutoFlushService>.Instance;
            return new CounterAutoFlushService(factory, options, timeProvider, logger);
        });
        return builder;
    }

    /// <summary>
    /// Registers a specific named counter with a customized synchronization strategy.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="name">The unique name of the counter.</param>
    /// <param name="strategy">The synchronization strategy to use for this counter.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddCounter(
        this IDistributedCounterBuilder builder,
        string name,
        CounterStrategy strategy) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(name);

        return builder.Configure(options => options.AddCounter(name, strategy));
    }

    /// <summary>
    /// Registers a specific named counter with a configuration action.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="name">The unique name of the counter.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddCounter(
        this IDistributedCounterBuilder builder,
        string name,
        Action<CounterConfiguration> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNull(configure);

        return builder.Configure(options => options.AddCounter(name, configure));
    }

    /// <summary>
    /// Registers a strongly-typed counter tag with a specific synchronization strategy.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="strategy">The synchronization strategy. Defaults to <see cref="CounterStrategy.Buffered"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddCounter<TTag>(
        this IDistributedCounterBuilder builder,
        CounterStrategy strategy = CounterStrategy.Buffered) where TTag : notnull {
        Preca.ThrowIfNull(builder);

        return builder.Configure(options => options.AddCounter<TTag>(strategy));
    }

    /// <summary>
    /// Registers a strongly-typed counter tag with a configuration action.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddCounter<TTag>(
        this IDistributedCounterBuilder builder,
        Action<CounterConfiguration> configure) where TTag : notnull {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        return builder.Configure(options => options.AddCounter<TTag>(configure));
    }

    /// <summary>
    /// Registers a strongly-typed counter enforced with <see cref="CounterStrategy.Immediate"/> strategy.
    /// Ideal for critical synchronization, rate-limiting, and circuit breakers.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <param name="builder">The distributed counter builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddImmediateCounter<TTag>(
        this IDistributedCounterBuilder builder) where TTag : notnull {
        Preca.ThrowIfNull(builder);

        return builder.Configure(options => options.AddImmediateCounter<TTag>());
    }

    /// <summary>
    /// Registers a named counter enforced with <see cref="CounterStrategy.Immediate"/> strategy.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="name">The unique name of the counter.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddImmediateCounter(
        this IDistributedCounterBuilder builder,
        string name) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(name);

        return builder.Configure(options => options.AddImmediateCounter(name));
    }

    /// <summary>
    /// Registers a strongly-typed counter enforced with <see cref="CounterStrategy.Buffered"/> strategy.
    /// Ideal for high-throughput metrics and telemetry.
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <param name="builder">The distributed counter builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddBufferedCounter<TTag>(
        this IDistributedCounterBuilder builder) where TTag : notnull {
        Preca.ThrowIfNull(builder);

        return builder.Configure(options => options.AddBufferedCounter<TTag>());
    }

    /// <summary>
    /// Registers a named counter enforced with <see cref="CounterStrategy.Buffered"/> strategy.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="name">The unique name of the counter.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder AddBufferedCounter(
        this IDistributedCounterBuilder builder,
        string name) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(name);

        return builder.Configure(options => options.AddBufferedCounter(name));
    }
}