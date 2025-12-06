using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Extensions;
using Wiaoj.Serialization.Abstractions;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;
public sealed class OutboxProcessor<TContext>(
    IServiceProvider serviceProvider,
    IOptionsMonitor<OutboxOptions> options,
    ISerializer<DddEfCoreOutboxSerializerKey> serializer,
    ILogger<OutboxProcessor<TContext>> logger,
    OutboxChannel outboxChannel) // <--- OutboxChannel bağımlılığı eklendi.
    : BackgroundService where TContext : DbContext {

    private readonly OutboxOptions _options = options.CurrentValue;

    // Type caching için önerilen ConcurrentDictionary
    private static readonly ConcurrentDictionary<string, Type?> _typeCache = new();

    // Channel'dan okuyucu
    private readonly ChannelReader<OutboxMessage> _channelReader = outboxChannel.Reader;

    // Eski ProcessOutboxMessagesAsync metodu ProcessOutboxMessagesFromDbAsync olarak yeniden adlandırılmıştır.
    // Bu, DB'den toplu sorgulama ve işlemeyi gerçekleştirir.
    private async Task ProcessOutboxMessagesFromDbAsync(CancellationToken stoppingToken) {
        try {
            // Her işlem için yeni scope oluşturulur (IDomainEventDispatcher vb. Scoped servisler için gereklidir)
            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            IDomainEventDispatcher domainEventDispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

            // İşlenmemiş ve tekrar deneme hakkı olan mesajları DB'den çek
            List<OutboxMessage> messages = await dbContext.Set<OutboxMessage>()
                .Where(m => m.ProcessedAt == null && m.RetryCount < this._options.RetryCount)
                .OrderBy(m => m.OccurredAt)
                .Take(this._options.BatchSize)
                .ToListAsync(stoppingToken);

            if (messages.Count == 0) {
                return;
            }

            logger.LogDebug("Processing {Count} outbox messages from DB Polling.", messages.Count);

            foreach (OutboxMessage message in messages) {
                // Mesajı işle ve durumu güncelle
                await ExecuteAndPersistMessageUpdateAsync(domainEventDispatcher, timeProvider, dbContext, message, stoppingToken);
            }

            // Toplu durum güncellemelerini kaydet
            await dbContext.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex) {
            logger.LogError(ex, "An unexpected error occurred in the outbox DB polling processor.");
        }
    }

    private async Task ProcessChannelMessagesAsync(CancellationToken stoppingToken) {
        logger.LogInformation("Outbox Channel Processor is starting.");

        await foreach (OutboxMessage message in _channelReader.ReadAllAsync(stoppingToken)) {
            await ProcessSingleMessageAsync(message, stoppingToken);
        }
    }

    private async Task ProcessDbPollingAsync(CancellationToken stoppingToken) {
        logger.LogInformation("Outbox DB Polling Processor is starting.");
        while (!stoppingToken.IsCancellationRequested) {
            await ProcessOutboxMessagesFromDbAsync(stoppingToken); 
            await this._options.PollingInterval.Delay(stoppingToken);
        }
    }

    private async Task ProcessSingleMessageAsync(OutboxMessage message, CancellationToken stoppingToken) {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IDomainEventDispatcher domainEventDispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        logger.LogDebug("Processing single outbox message {MessageId} from Channel.", message.Id);

        await ExecuteAndPersistMessageUpdateAsync(domainEventDispatcher, timeProvider, dbContext, message, stoppingToken, isChannelMessage: true);

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private async Task ExecuteAndPersistMessageUpdateAsync(
        IDomainEventDispatcher domainEventDispatcher,
        TimeProvider timeProvider,
        TContext dbContext,
        OutboxMessage message,
        CancellationToken stoppingToken,
        bool isChannelMessage = false) {

        IDomainEvent? domainEvent = DeserializeEvent(message);
        if (domainEvent is null) {
            message.MarkAsFailed("Event could not be deserialized.");
            logger.LogWarning("Could not deserialize outbox message {MessageId} with type {MessageType}.", message.Id, message.Type);
            // Channel'dan geliyorsa, durumu güncelleyebilmek için Context'e eklenmelidir
            if (isChannelMessage)
                dbContext.Attach(message);
            return;
        }

        try {
            await domainEventDispatcher.DispatchPostCommitAsync((dynamic)domainEvent, stoppingToken);
            message.MarkAsProcessed(timeProvider.GetUtcNow());
        }
        catch (Exception ex) {
            message.MarkAsFailed(ex.ToString());
            logger.LogError(ex, "Error processing outbox message {MessageId}.", message.Id);
        }

        if (isChannelMessage) {
            dbContext.Attach(message);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        logger.LogInformation("Outbox Processor is starting. Mode: Channel + DB Polling.");

        Task channelReadTask = Task.Run(() => ProcessChannelMessagesAsync(stoppingToken), stoppingToken);

        Task dbPollingTask = Task.Run(() => ProcessDbPollingAsync(stoppingToken), stoppingToken);

        await Task.WhenAll(channelReadTask, dbPollingTask);

        logger.LogInformation("Outbox Processor is stopping.");
    }

    private IDomainEvent? DeserializeEvent(OutboxMessage message) {
        if (!_typeCache.TryGetValue(message.Type, out Type? eventType)) {
            eventType = Type.GetType(message.Type);

            _typeCache.TryAdd(message.Type, eventType);
        }

        if (eventType is null) {
            return null;
        }

        return serializer.DeserializeFromString(message.Content, eventType) as IDomainEvent;
    }
}