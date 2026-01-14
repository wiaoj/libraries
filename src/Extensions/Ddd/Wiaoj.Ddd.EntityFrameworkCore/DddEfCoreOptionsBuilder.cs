using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Serialization;
using Wiaoj.Serialization.DependencyInjection;

namespace Wiaoj.Ddd.EntityFrameworkCore;
/// <summary>
/// A builder for configuring EF Core DDD integration options, including Outbox settings and Serialization.
/// </summary>
public sealed class DddEfCoreOptionsBuilder(IServiceCollection services) {
    public IServiceCollection Services { get; } = services;

    // Outbox ayarlarını geçici olarak burada tutuyoruz
    private readonly OutboxOptions _outboxOptions = new();

    // Serializer'ın ayarlanıp ayarlanmadığını takip ediyoruz (Varsayılan atamak için)
    internal bool IsSerializerConfigured { get; private set; } = false;

    /// <summary>
    /// Configures the Outbox settings (Polling interval, Batch size, Locking, etc.).
    /// </summary>
    public void ConfigureOutbox(Action<OutboxOptions> configure) {
        configure(this._outboxOptions);
    }

    /// <summary>
    /// Configures the serializer used for persisting Outbox messages and Domain Events.
    /// You can use this to register System.Text.Json, MessagePack, etc.
    /// </summary>
    public void UseSerializer(Action<IWiaojSerializationBuilder> configure) {
        this.Services.AddWiaojSerializer(configure);
        this.IsSerializerConfigured = true;
    }

    /// <summary>
    /// Uses System.Text.Json as the serializer for Outbox messages.
    /// </summary>
    /// <param name="configure">Optional action to configure JsonSerializerOptions.</param>
    public void UseSystemTextJson(Action<JsonSerializerOptions>? configure = null) {
        UseSerializer(builder => {
            if (configure is null) {
                builder.UseSystemTextJson<DddEfCoreOutboxSerializerKey>();
            }
            else {
                builder.UseSystemTextJson<DddEfCoreOutboxSerializerKey>(configure);
            }
        });
    }

    /// <summary>
    /// Applies the accumulated configuration to the service collection.
    /// </summary>
    internal void Build() {
        this.Services.Configure<OutboxOptions>(options => {
            options.BatchSize = this._outboxOptions.BatchSize;
            options.PollingInterval = this._outboxOptions.PollingInterval;
            options.RetryCount = this._outboxOptions.RetryCount;
            options.MaxDomainEventDispatchAttempts = this._outboxOptions.MaxDomainEventDispatchAttempts;
            options.PartitionKey = this._outboxOptions.PartitionKey;
            options.LockDuration = this._outboxOptions.LockDuration;
            options.InitialDelay = this._outboxOptions.InitialDelay;
        });

        if (!this.IsSerializerConfigured) {
            UseSystemTextJson();
        }
    }
}