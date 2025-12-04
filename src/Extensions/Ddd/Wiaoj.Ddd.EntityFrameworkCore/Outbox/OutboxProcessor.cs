using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Extensions;
using Wiaoj.Serialization.Abstractions;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;
public sealed class OutboxProcessor<TContext>(
    IServiceProvider serviceProvider,
    //IDbContextFactory<TContext> dbContextFactory,
    IOptionsMonitor<OutboxOptions> options,
    ISerializer<DddEfCoreOutboxSerializerKey> serializer,
    ILogger<OutboxProcessor<TContext>> logger)
    : BackgroundService where TContext : DbContext {

    private readonly OutboxOptions _options = options.CurrentValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        logger.LogInformation("Outbox Processor is starting.");

        while (!stoppingToken.IsCancellationRequested) {
            await ProcessOutboxMessagesAsync(stoppingToken);
            await this._options.PollingInterval.Delay(stoppingToken);
        }

        logger.LogInformation("Outbox Processor is stopping.");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken) {
        try { 

            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            IDomainEventDispatcher domainEventDispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

            List<OutboxMessage> messages = await dbContext.Set<OutboxMessage>()
                .Where(m => m.ProcessedAt == null && m.RetryCount < this._options.RetryCount)
                .OrderBy(m => m.OccurredAt)
                .Take(this._options.BatchSize)
                .ToListAsync(stoppingToken);

            if (messages.Count == 0) { 
                return;
            }

            logger.LogDebug("Processing {Count} outbox messages.", messages.Count);

            foreach (OutboxMessage message in messages) {
                IDomainEvent? domainEvent = DeserializeEvent(message);
                if (domainEvent is null) {
                    message.MarkAsFailed("Event could not be deserialized.");
                    logger.LogWarning("Could not deserialize outbox message {MessageId} with type {MessageType}.", message.Id, message.Type);
                    continue;
                }

                try {
                    // Post-commit handler'ları dinamik olarak çağır.
                    await domainEventDispatcher.DispatchPostCommitAsync( (dynamic)domainEvent, stoppingToken);
                    message.MarkAsProcessed(timeProvider.GetUtcNow());
                }
                catch (Exception ex) {
                    message.MarkAsFailed(ex.ToString());
                    logger.LogError(ex, "Error processing outbox message {MessageId}.", message.Id);
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex) {
            logger.LogError(ex, "An unexpected error occurred in the outbox processor.");
        }
    }

    private IDomainEvent? DeserializeEvent(OutboxMessage message) {
        // AssemblyQualifiedName kullanarak tipin bulunmasını garantiliyoruz.
        Type? eventType = Type.GetType(message.Type);
        if (eventType is null) {
            return null;
        }

        return serializer.DeserializeFromString(message.Content, eventType) as IDomainEvent;
    }
}