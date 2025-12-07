using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Serialization.Abstractions;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;
/// <summary>
/// A background service that processes Outbox messages.
/// Implements a dual-path strategy: Fast Path (Channel) and Recovery Path (DB Polling).
/// Uses "Owner-Processing" pattern with pessimistic locking via <see cref="OutboxMessage.LockId"/>.
/// </summary>
internal sealed class OutboxProcessor<TContext>(
    IServiceProvider serviceProvider,
    IOptionsMonitor<OutboxOptions> options,
    ISerializer<DddEfCoreOutboxSerializerKey> serializer,
    ILogger<OutboxProcessor<TContext>> logger,
    OutboxChannel outboxChannel,
    OutboxInstanceInfo instanceInfo)
    : BackgroundService where TContext : DbContext {

    private static readonly ConcurrentDictionary<string, Type?> _typeCache = new();
    private readonly ChannelReader<OutboxMessage> _channelReader = outboxChannel.Reader;
    private readonly string _myInstanceId = instanceInfo.InstanceId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        logger.LogInformation("Outbox Processor started. InstanceId: {InstanceId}, PartitionKey: {Partition}",
            _myInstanceId, options.CurrentValue.PartitionKey ?? "Global");

        // 1. Channel Task: Processes messages created by this instance (Fast)
        Task channelTask = Task.Run(() => ProcessChannelMessagesAsync(stoppingToken), stoppingToken);

        // 2. Polling Task: Recovers expired locks (Zombies) or unclaimed messages (Fallback)
        Task pollingTask = Task.Run(() => ProcessDbPollingAsync(stoppingToken), stoppingToken);

        await Task.WhenAll(channelTask, pollingTask);
    }

    /// <summary>
    /// Continuously reads from the in-memory channel.
    /// Only processes messages locked by THIS instance to avoid database round-trips for lock checks.
    /// </summary>
    private async Task ProcessChannelMessagesAsync(CancellationToken stoppingToken) {
        await foreach (OutboxMessage message in _channelReader.ReadAllAsync(stoppingToken)) {
            // Safety check: Only process if we own the lock (should always be true for channel messages)
            if (message.LockId == _myInstanceId) {
                await ProcessSingleMessageSafeAsync(message, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Periodically polls the database to claim and process messages that:
    /// 1. Have no lock (Unclaimed)
    /// 2. Have expired locks (Zombies from crashed instances)
    /// </summary>
    private async Task ProcessDbPollingAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await RecoverAndProcessZombiesAsync(stoppingToken);
            }
            catch (Exception ex) {
                logger.LogError(ex, "An error occurred during DB polling for zombie recovery.");
            }

            await Task.Delay(options.CurrentValue.PollingInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Claims ownership of available messages using atomic database updates and then processes them.
    /// </summary>
    private async Task RecoverAndProcessZombiesAsync(CancellationToken stoppingToken) {
        var currentOptions = options.CurrentValue;

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        DateTimeOffset now = timeProvider.GetUtcNow();

        // Query for unprocessed messages where Lock is missing OR expired
        var query = dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null)
            .Where(m => m.LockId == null || m.LockExpiration < now);

        if (!string.IsNullOrEmpty(currentOptions.PartitionKey)) {
            query = query.Where(m => m.PartitionKey == currentOptions.PartitionKey);
        }

        // Atomic Update: Claim LockId and extend Expiration
        int claimedCount = await query
            .OrderBy(m => m.OccurredAt)
            .Take(currentOptions.BatchSize)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.LockId, _myInstanceId)
                .SetProperty(m => m.LockExpiration, now.Add(currentOptions.LockDuration)),
                stoppingToken);

        if (claimedCount == 0)
            return;

        logger.LogInformation("Recovered {Count} stalled outbox messages.", claimedCount);

        // Fetch the messages we just successfully locked
        var messages = await dbContext.Set<OutboxMessage>()
            .Where(m => m.LockId == _myInstanceId && m.ProcessedAt == null)
            .ToListAsync(stoppingToken);

        foreach (var message in messages) {
            await ProcessSingleMessageSafeAsync(message, stoppingToken);
        }
    }

    /// <summary>
    /// Executes the dispatch logic and safely updates the message status in the database.
    /// Ensures status is ONLY updated if the lock is still held by this instance.
    /// </summary>
    private async Task ProcessSingleMessageSafeAsync(OutboxMessage message, CancellationToken stoppingToken) {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        try {
            // 1. Deserialize
            IDomainEvent? domainEvent = DeserializeEvent(message);

            if (domainEvent is null) {
                // If we can't deserialize, we must fail it to stop the loop.
                await MarkAsFailedDbAsync(dbContext, message.Id, $"Type not found: {message.Type}", stoppingToken);
                return;
            }

            // 2. Dispatch (Post-Commit Handlers)
            await dispatcher.DispatchPostCommitAsync((dynamic)domainEvent, stoppingToken);

            // 3. Safe Completion Update (The "GG" Prevention)
            // We use ExecuteUpdateAsync with a WHERE clause checking LockId.
            // If the lock expired and was stolen by another instance during processing,
            // this update will affect 0 rows, preserving data integrity.
            int rowsAffected = await dbContext.Set<OutboxMessage>()
                .Where(m => m.Id == message.Id && m.LockId == _myInstanceId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.ProcessedAt, timeProvider.GetUtcNow())
                    .SetProperty(m => m.Error, (string?)null),
                    stoppingToken);

            if (rowsAffected == 0) {
                logger.LogWarning("Message {Id} processed successfully, but DB update failed because lock was lost (expired).", message.Id);
            }
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to process message {Id}.", message.Id);
            await MarkAsFailedDbAsync(dbContext, message.Id, ex.ToString(), stoppingToken);
        }
    }

    /// <summary>
    /// Safely marks a message as failed in the database without releasing the lock immediately (waiting for expiration or next retry).
    /// </summary>
    private async Task MarkAsFailedDbAsync(TContext dbContext, Guid id, string error, CancellationToken token) {
        try {
            await dbContext.Set<OutboxMessage>()
                .Where(m => m.Id == id && m.LockId == _myInstanceId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Error, error)
                    .SetProperty(m => m.RetryCount, c => c.RetryCount + 1),
                    token);
        }
        catch {
            // Suppress errors during error handling to avoid crashing the loop
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IDomainEvent? DeserializeEvent(OutboxMessage message) {
        if (!_typeCache.TryGetValue(message.Type, out Type? eventType)) {
            eventType = Type.GetType(message.Type);
            _typeCache.TryAdd(message.Type, eventType);
        }

        if (eventType is null)
            return null;

        return serializer.DeserializeFromString(message.Content, eventType) as IDomainEvent;
    }
}