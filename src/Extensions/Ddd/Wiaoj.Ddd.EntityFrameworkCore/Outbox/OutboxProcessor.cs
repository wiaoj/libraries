using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.EntityFrameworkCore.Internal.Loggers;
using Wiaoj.Ddd.Extensions;
using Wiaoj.Extensions;
using Wiaoj.Serialization;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;
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
        TimeSpan initialDelay = options.CurrentValue.InitialDelay;

        if(initialDelay > TimeSpan.Zero) { 
            logger.LogInitialDelayPending(initialDelay); 
            await initialDelay.Delay(stoppingToken);
        }

        using(logger.BeginScope(new Dictionary<string, object> {
            ["ProcessorInstanceId"] = this._myInstanceId,
            ["PartitionKey"] = options.CurrentValue.PartitionKey ?? "Global"
        })) {
            // Extension Method Call
            logger.LogServiceStarted(options.CurrentValue.BatchSize, options.CurrentValue.PollingInterval);

            Task channelTask = Task.Run(() => ProcessChannelMessagesAsync(stoppingToken), stoppingToken);
            Task pollingTask = Task.Run(() => ProcessDbPollingAsync(stoppingToken), stoppingToken);

            await Task.WhenAll(channelTask, pollingTask);
        }
    }

    private async Task ProcessChannelMessagesAsync(CancellationToken stoppingToken) {
        logger.LogFastPathStarted();

        try {
            await foreach(OutboxMessage message in this._channelReader.ReadAllAsync(stoppingToken)) {
                if(message.LockId == this._myInstanceId) {
                    await ProcessSingleMessageSafeAsync(message, processingMode: "FastPath", stoppingToken);
                }
            }
        }
        catch(OperationCanceledException) {
            logger.LogFastPathStopped();
        }
        catch(Exception ex) {
            logger.LogFastPathCriticalError(ex);
        }
    }

    private async Task ProcessDbPollingAsync(CancellationToken stoppingToken) {
        logger.LogSlowPathStarted();

        while(!stoppingToken.IsCancellationRequested) {
            try {
                await RecoverAndProcessZombiesAsync(stoppingToken);
            }
            catch(Exception ex) {
                logger.LogPollingError(ex);
            }

            await Task.Delay(options.CurrentValue.PollingInterval, stoppingToken);
        }
    }

    private async Task RecoverAndProcessZombiesAsync(CancellationToken stoppingToken) {
        OutboxOptions currentOptions = options.CurrentValue;

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        DateTimeOffset now = timeProvider.GetUtcNow();

        IQueryable<OutboxMessage> query = dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null)
            .Where(m => m.LockId == null || m.LockExpiration < now)
            .Where(m => m.RetryCount < currentOptions.RetryCount);

        if(!string.IsNullOrEmpty(currentOptions.PartitionKey)) {
            query = query.Where(m => m.PartitionKey == currentOptions.PartitionKey);
        }

        int claimedCount = await query
            .OrderBy(m => m.OccurredAt)
            .Take(currentOptions.BatchSize)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.LockId, this._myInstanceId)
                .SetProperty(m => m.LockExpiration, now.Add(currentOptions.LockDuration)),
                stoppingToken);

        if(claimedCount == 0) {
            return;
        }

        logger.LogZombieMessagesClaimed(claimedCount);

        List<OutboxMessage> messages = await dbContext.Set<OutboxMessage>()
            .Where(m => m.LockId == this._myInstanceId && m.ProcessedAt == null)
            .ToListAsync(stoppingToken);

        foreach(OutboxMessage? message in messages) {
            await ProcessSingleMessageSafeAsync(message, processingMode: "SlowPath", stoppingToken);
        }
    }

    private async Task ProcessSingleMessageSafeAsync(OutboxMessage message, string processingMode, CancellationToken stoppingToken) {
        using IDisposable? logScope = logger.BeginScope(new Dictionary<string, object> {
            ["OutboxMessageId"] = message.Id,
            ["MessageType"] = message.Type,
            ["ProcessingMode"] = processingMode
        });

        logger.LogProcessingStarted();

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        IDomainEventDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        Stopwatch stopwatch = Stopwatch.StartNew();

        try {
            IDomainEvent? domainEvent = DeserializeEvent(message);

            if(domainEvent is null) {
                logger.LogDeserializationFailed();
                await MarkAsFailedDbAsync(dbContext, message.Id, $"Type resolution failed: {message.Type}", stoppingToken);
                return;
            }

            logger.LogDispatchingHandlers();
            await dispatcher.DispatchPostCommitCompiledAsync(domainEvent, stoppingToken);

            stopwatch.Stop();
            logger.LogHandlersExecuted(stopwatch.ElapsedMilliseconds);

            int rowsAffected = await dbContext.Set<OutboxMessage>()
                .Where(m => m.Id == message.Id && m.LockId == this._myInstanceId)
                .Where(m => m.ProcessedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.ProcessedAt, timeProvider.GetUtcNow())
                    .SetProperty(m => m.Error, (string?)null)
                    .SetProperty(m => m.LockExpiration, (DateTimeOffset?)null),
                    stoppingToken);

            if(rowsAffected > 0) {
                logger.LogMessageProcessedSuccessfully(stopwatch.ElapsedMilliseconds);
            }
            else {
                await RunDiagnosticsForMissingUpdateAsync(dbContext, message.Id, stopwatch.ElapsedMilliseconds, stoppingToken);
            }
        }
        catch(Exception ex) {
            stopwatch.Stop();
            logger.LogProcessingFailed(ex, stopwatch.ElapsedMilliseconds);
            await MarkAsFailedDbAsync(dbContext, message.Id, ex.ToString(), stoppingToken);
        }
    }

    private async Task RunDiagnosticsForMissingUpdateAsync(TContext dbContext, Guid messageId, long durationMs, CancellationToken stoppingToken) {
        OutboxMessage? actualState = await dbContext.Set<OutboxMessage>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId, stoppingToken);

        if(actualState is null) {
            logger.LogDiagRecordNotFound();
            return;
        }

        if(actualState.ProcessedAt.HasValue) {
            logger.LogDiagAlreadyProcessed(actualState.ProcessedAt);
            return;
        }

        if(actualState.LockId != this._myInstanceId) {
            logger.LogDiagLockLost(actualState.LockId ?? "NULL", durationMs);
            return;
        }

        logger.LogDiagRaceCondition(actualState.LockId, actualState.LockExpiration);
    }

    private async Task MarkAsFailedDbAsync(TContext dbContext, Guid id, string error, CancellationToken token) {
        try {
            int rows = await dbContext.Set<OutboxMessage>()
                .Where(m => m.Id == id && m.LockId == this._myInstanceId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Error, error)
                    .SetProperty(m => m.RetryCount, c => c.RetryCount + 1),
                    token);

            if(rows > 0) {
                logger.LogMarkedAsFailed();
            }
            else {
                logger.LogMarkFailedStatusUpdateLost();
            }
        }
        catch(Exception ex) {
            logger.LogCriticalMarkFailedError(ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IDomainEvent? DeserializeEvent(OutboxMessage message) {
        if(!_typeCache.TryGetValue(message.Type, out Type? eventType)) {
            try {
                eventType = Type.GetType(message.Type);
                _typeCache.TryAdd(message.Type, eventType);
            }
            catch(Exception ex) {
                logger.LogTypeResolutionError(ex, message.Type);
                return null;
            }
        }

        if(eventType is null) {
            logger.LogTypeResolutionNull(message.Type);
            return null;
        }

        try {
            return serializer.DeserializeFromString(message.Content, eventType) as IDomainEvent;
        }
        catch(Exception ex) {
            logger.LogDeserializationError(ex, message.Type);
            return null;
        }
    }
}