using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;
using Wiaoj.Ddd.EntityFrameworkCore.Internal.Loggers;
using Wiaoj.Ddd.Extensions;
using Wiaoj.Serialization;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;

/// <summary>
/// Claims pending outbox rows and runs the single handler each one names.
/// </summary>
/// <remarks>
/// <para>
/// A row is one (event, handler) pair, so a failure is scoped to the handler that failed: the others already
/// completed and are not repeated. That is what makes retries safe without demanding that every handler be
/// idempotent.
/// </para>
/// <para>
/// A row that keeps failing is dead-lettered rather than quietly dropped once its attempts run out — a
/// terminal state that can be queried and alerted on, instead of a row that simply stops matching the claim
/// predicate and is never heard from again.
/// </para>
/// </remarks>
internal sealed class OutboxProcessor<TContext>(
    IServiceProvider serviceProvider,
    IOptionsMonitor<OutboxOptions> options,
    ISerializer<DddEfCoreOutboxSerializerKey> serializer,
    ILogger<OutboxProcessor<TContext>> logger,
    OutboxInstanceInfo instanceInfo,
    OutboxClaimStrategyFactory claimStrategyFactory,
    IOutboxAliasRegistry aliases,
    OutboxHandlerCatalog handlerCatalog)
    : BackgroundService where TContext : DbContext {

    private readonly string _myInstanceId = instanceInfo.InstanceId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        TimeSpan initialDelay = options.CurrentValue.InitialDelay;

        if(initialDelay > TimeSpan.Zero) {
            logger.LogInitialDelayPending(initialDelay);
            await Task.Delay(initialDelay, stoppingToken);
        }

        using(logger.BeginScope(new Dictionary<string, object> {
            ["ProcessorInstanceId"] = this._myInstanceId,
            ["PartitionKey"] = options.CurrentValue.PartitionKey ?? "Global"
        })) {
            logger.LogServiceStarted(options.CurrentValue.BatchSize, options.CurrentValue.PollingInterval);

            while(!stoppingToken.IsCancellationRequested) {
                try {
                    int processed = await ProcessBatchAsync(stoppingToken);

                    // A full batch means there is probably more waiting; go straight round again rather than
                    // sleeping through a backlog.
                    if(processed >= options.CurrentValue.BatchSize) {
                        continue;
                    }
                }
                catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {
                    break;
                }
                catch(Exception ex) {
                    logger.LogPollingError(ex);
                }

                await Task.Delay(options.CurrentValue.PollingInterval, stoppingToken);
            }
        }
    }

    /// <summary>Claims one batch and runs it. Returns how many rows were claimed.</summary>
    internal async Task<int> ProcessBatchAsync(CancellationToken cancellationToken) {
        OutboxOptions currentOptions = options.CurrentValue;

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        DateTimeOffset now = timeProvider.GetUtcNow();

        IOutboxClaimStrategy claimStrategy = claimStrategyFactory.Create(dbContext);

        IReadOnlyList<OutboxMessage> claimed = await claimStrategy.ClaimAsync(
            dbContext,
            currentOptions.BatchSize,
            this._myInstanceId,
            now.UtcTicks,
            now.Add(currentOptions.LockDuration).UtcTicks,
            currentOptions.PartitionKey,
            cancellationToken).ConfigureAwait(false);

        if(claimed.Count == 0) {
            return 0;
        }

        logger.LogZombieMessagesClaimed(claimed.Count);

        foreach(OutboxMessage message in claimed) {
            if(cancellationToken.IsCancellationRequested) {
                break;
            }

            await ProcessSingleAsync(scope.ServiceProvider, dbContext, message, currentOptions, timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }

        return claimed.Count;
    }

    private async Task ProcessSingleAsync(
        IServiceProvider scopedProvider,
        TContext dbContext,
        OutboxMessage message,
        OutboxOptions currentOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) {

        using IDisposable? logScope = logger.BeginScope(new Dictionary<string, object> {
            ["OutboxMessageId"] = message.Id,
            ["EventAlias"] = message.EventAlias,
            ["HandlerAlias"] = message.HandlerAlias
        });

        logger.LogProcessingStarted();
        Stopwatch stopwatch = Stopwatch.StartNew();

        try {
            Type? eventType = aliases.ResolveEventType(message.EventAlias);

            if(eventType is null) {
                // The event type is gone. No number of retries brings it back.
                await FinalizeAsync(dbContext, message, m => m.MarkDeadLettered(
                    timeProvider.GetUtcNow(), $"Event type '{message.EventAlias}' could not be resolved."), cancellationToken);
                logger.LogDeserializationFailed();
                return;
            }

            if(serializer.DeserializeFromString(message.Payload, eventType) is not IDomainEvent domainEvent) {
                await FinalizeAsync(dbContext, message, m => m.MarkDeadLettered(
                    timeProvider.GetUtcNow(), $"Payload for '{message.EventAlias}' did not deserialize into a domain event."), cancellationToken);
                logger.LogDeserializationFailed();
                return;
            }

            logger.LogDispatchingHandlers();

            bool dispatched = await handlerCatalog
                .TryDispatchAsync(scopedProvider, domainEvent, message.HandlerAlias, cancellationToken)
                .ConfigureAwait(false);

            if(!dispatched) {
                // The handler this row was written for no longer exists. Also terminal.
                await FinalizeAsync(dbContext, message, m => m.MarkDeadLettered(
                    timeProvider.GetUtcNow(), $"Handler '{message.HandlerAlias}' is no longer registered."), cancellationToken);
                return;
            }

            stopwatch.Stop();

            await FinalizeAsync(dbContext, message, m => m.MarkProcessed(timeProvider.GetUtcNow(), this._myInstanceId), cancellationToken);
            logger.LogMessageProcessedSuccessfully(stopwatch.ElapsedMilliseconds);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception ex) {
            stopwatch.Stop();
            logger.LogProcessingFailed(ex, stopwatch.ElapsedMilliseconds);

            DateTimeOffset failedAt = timeProvider.GetUtcNow();
            OutboxRetryPolicy policy = currentOptions.RetryPolicy;

            // Attempts is incremented by whichever branch runs, so compare against the count before it.
            bool exhausted = message.Attempts + 1 >= policy.MaxAttempts;

            if(exhausted) {
                await FinalizeAsync(dbContext, message, m => m.MarkDeadLettered(failedAt, ex.ToString()), CancellationToken.None);
                logger.LogMarkedAsFailed();
            }
            else {
                TimeSpan delay = policy.DelayFor(message.Attempts);
                await FinalizeAsync(dbContext, message, m => m.ScheduleRetry(failedAt, delay, ex.ToString()), CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Applies a terminal or retry transition and persists it.
    /// </summary>
    /// <remarks>
    /// The claim returned untracked instances, so the row is attached before saving. The lock this instance
    /// holds is what makes the write safe: no other processor can be acting on this row.
    /// </remarks>
    private static async Task FinalizeAsync(
        DbContext dbContext,
        OutboxMessage message,
        Action<OutboxMessage> transition,
        CancellationToken cancellationToken) {

        transition(message);

        dbContext.Attach(message);
        dbContext.Entry(message).State = EntityState.Modified;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        dbContext.Entry(message).State = EntityState.Detached;
    }
}
