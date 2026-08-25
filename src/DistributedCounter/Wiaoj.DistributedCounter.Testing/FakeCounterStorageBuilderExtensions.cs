using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.DistributedCounter.Testing;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.DistributedCounter;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring fake storage doubles on <see cref="IDistributedCounterBuilder"/>.
/// </summary>
public static class FakeCounterStorageBuilderExtensions {

    /// <summary>
    /// Configures the distributed counter engine to use an isolated <see cref="FakeCounterStorage"/> in DI.
    /// Ideal for integration tests, WebApplicationFactory, and local mock testing.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseFakeStorage(this IDistributedCounterBuilder builder) {
        Preca.ThrowIfNull(builder);

        FakeCounterStorage fakeStorage = new();
        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage>(fakeStorage);
        builder.Services.AddSingleton(fakeStorage);

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter engine to use a <see cref="FakeCounterStorage"/> and exports the storage instance.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="storage">When this method returns, contains the configured fake storage instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseFakeStorage(
        this IDistributedCounterBuilder builder,
        out FakeCounterStorage storage) {
        Preca.ThrowIfNull(builder);

        storage = new FakeCounterStorage();
        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage>(storage);
        builder.Services.AddSingleton(storage);

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter engine to use an existing <see cref="FakeCounterStorage"/> instance.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <param name="storage">The existing fake storage instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseFakeStorage(
        this IDistributedCounterBuilder builder,
        FakeCounterStorage storage) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(storage);

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage>(storage);
        builder.Services.AddSingleton(storage);

        return builder;
    }
}