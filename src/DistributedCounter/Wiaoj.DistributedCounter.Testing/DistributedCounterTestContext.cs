using Wiaoj.DistributedCounter.Internal;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter.Testing;

/// <summary>
/// A lightweight, pre-wired test harness that automates dependency creation for unit tests.
/// </summary>
public sealed class DistributedCounterTestContext {
    private DistributedCounterFactory? _factory;

    /// <summary>Gets the time provider driving the test harness.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Gets or sets the test storage double.</summary>
    public FakeCounterStorage Storage { get; set; }

    /// <summary>Gets or sets the key builder.</summary>
    public ICounterKeyBuilder KeyBuilder { get; set; } = new DefaultCounterKeyBuilder();

    /// <summary>Gets or sets the configuration options.</summary>
    public DistributedCounterOptions Options { get; set; } = new();

    /// <summary>
    /// Gets the scoped singleton <see cref="IDistributedCounterFactory"/> instance for this test context.
    /// </summary>
    public IDistributedCounterFactory Factory =>
        this._factory ??= new(this.Storage, this.KeyBuilder, Microsoft.Extensions.Options.Options.Create(this.Options));

    /// <summary>
    /// Initializes a new instance of <see cref="DistributedCounterTestContext"/> using the system clock.
    /// </summary>
    public DistributedCounterTestContext() : this(TimeProvider.System, null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="DistributedCounterTestContext"/> using the system clock and custom options.
    /// </summary>
    /// <param name="configureOptions">A delegate to configure options.</param>
    public DistributedCounterTestContext(Action<DistributedCounterOptions> configureOptions) : this(TimeProvider.System, configureOptions) { }

    /// <summary>
    /// Initializes a new instance of <see cref="DistributedCounterTestContext"/> using a specific <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider instance.</param>
    public DistributedCounterTestContext(TimeProvider timeProvider) : this(timeProvider, null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="DistributedCounterTestContext"/> with a specific <see cref="TimeProvider"/> and custom options.
    /// </summary>
    /// <param name="timeProvider">The time provider instance.</param>
    /// <param name="configureOptions">An optional delegate to configure options.</param>
    public DistributedCounterTestContext(TimeProvider timeProvider, Action<DistributedCounterOptions>? configureOptions) {
        Preca.ThrowIfNull(timeProvider);

        this.TimeProvider = timeProvider;
        this.Storage = new FakeCounterStorage(timeProvider);
        configureOptions?.Invoke(this.Options);
    }

    /// <summary>
    /// Resolves the scoped <see cref="IDistributedCounterFactory"/> instance.
    /// </summary>
    /// <returns>The factory instance.</returns>
    public IDistributedCounterFactory CreateFactory() {
        return this.Factory;
    }

    /// <summary>
    /// Creates a <see cref="IDistributedCounterService"/> instance wired to this context's scoped factory.
    /// </summary>
    /// <returns>The distributed counter service instance.</returns>
    public IDistributedCounterService CreateService() {
        return new DistributedCounterService(
            this.Storage,
            this.KeyBuilder,
            this.Factory,
            Microsoft.Extensions.Options.Options.Create(this.Options));
    }
}