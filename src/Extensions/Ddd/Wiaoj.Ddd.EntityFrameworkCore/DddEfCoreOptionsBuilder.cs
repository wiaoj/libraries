using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Preconditions;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;

namespace Wiaoj.Ddd.EntityFrameworkCore;
/// <summary>
/// A builder for configuring EF Core DDD integration options, including Outbox settings and serialization.
/// </summary>
public sealed class DddEfCoreOptionsBuilder(IServiceCollection services) {
    private readonly OutboxOptions _outboxOptions = new();
    private bool _isSerializerConfigured;

    /// <summary>
    /// Configures the Outbox settings (polling interval, batch size, locking, etc.).
    /// </summary>
    public DddEfCoreOptionsBuilder ConfigureOutbox(Action<OutboxOptions> configure) {
        Preca.ThrowIfNull(configure);
        configure(this._outboxOptions);
        return this;
    }

    /// <summary>
    /// Uses System.Text.Json as the serializer for Outbox messages with default options.
    /// </summary>
    public DddEfCoreOptionsBuilder UseSystemTextJson() {
        return UseSystemTextJson(_ => { });
    }

    /// <summary>
    /// Uses System.Text.Json as the serializer for Outbox messages with custom options.
    /// </summary>
    /// <param name="configure">An action to configure <see cref="JsonSerializerOptions"/>.</param>
    public DddEfCoreOptionsBuilder UseSystemTextJson(Action<JsonSerializerOptions> configure) {
        Preca.ThrowIfNull(configure);

        JsonSerializerOptions options = new();
        configure(options);

        services.TryAddSingleton<ISerializer<DddEfCoreOutboxSerializerKey>>(
            _ => new SystemTextJsonSerializer<DddEfCoreOutboxSerializerKey>(options));

        this._isSerializerConfigured = true;
        return this;
    }

    /// <summary>
    /// Uses a custom serializer for Outbox messages.
    /// </summary>
    /// <param name="factory">A factory that creates the serializer instance.</param>
    public DddEfCoreOptionsBuilder UseSerializer(
        Func<IServiceProvider, ISerializer<DddEfCoreOutboxSerializerKey>> factory) {
        Preca.ThrowIfNull(factory);

        services.TryAddSingleton<ISerializer<DddEfCoreOutboxSerializerKey>>(factory);

        this._isSerializerConfigured = true;
        return this;
    }

    /// <summary>
    /// Applies the accumulated configuration to the service collection.
    /// </summary>
    internal void Build() {
        services.Configure<OutboxOptions>(options => {
            options.BatchSize = this._outboxOptions.BatchSize;
            options.PollingInterval = this._outboxOptions.PollingInterval;
            options.RetryCount = this._outboxOptions.RetryCount;
            options.MaxDomainEventDispatchAttempts = this._outboxOptions.MaxDomainEventDispatchAttempts;
            options.PartitionKey = this._outboxOptions.PartitionKey;
            options.LockDuration = this._outboxOptions.LockDuration;
            options.InitialDelay = this._outboxOptions.InitialDelay;
        });

        if(!this._isSerializerConfigured) {
            UseSystemTextJson();
        }
    }
}