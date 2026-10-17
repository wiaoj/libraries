using System.Text.Json;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// Converts between the webhook records and their row shapes.
/// </summary>
/// <remarks>
/// Every public property of <see cref="WebhookJobRecord"/> must be carried here in both directions. A round-trip test
/// in the EF Core test suite sets every public property by reflection and fails when one does not survive the store.
/// </remarks>
internal static class WebhookEntityMapper {
    internal const string Delivered = "delivered";
    internal const string Deduplicated = "deduplicated";
    internal const string TransientFailure = "transient_failure";
    internal const string PermanentFailure = "permanent_failure";

    public static long ToTicks(DateTimeOffset value) {
        return value.UtcTicks;
    }

    public static long? ToTicks(DateTimeOffset? value) {
        return value?.UtcTicks;
    }

    public static DateTimeOffset FromTicks(long ticks) {
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    public static DateTimeOffset? FromTicks(long? ticks) {
        return ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : null;
    }

    // ── Jobs ─────────────────────────────────────────────────────────────────

    public static WebhookJobEntity ToEntity(WebhookJobRecord record) {
        WebhookJobEntity entity = new() {
            Id = record.Id.Value,
            EndpointId = record.EndpointId.Value,
            PartitionKey = record.PartitionKey,
            EventType = record.EventType,
            SerializedPayload = record.SerializedPayload,
            CreatedAtTicks = ToTicks(record.CreatedAt),
            HopCount = record.HopCount,
            CausalChain = record.CausalChain
        };

        CopyState(record, entity);
        return entity;
    }

    /// <summary>
    /// Copies the mutable state of <paramref name="record"/> onto an existing row, replacing its attempts.
    /// </summary>
    public static void CopyState(WebhookJobRecord record, WebhookJobEntity entity) {
        entity.BatchId = record.BatchId;
        entity.Status = record.Status;
        entity.NextAttemptAtTicks = ToTicks(record.NextAttemptAt);
        entity.LockedBy = record.LockedBy;
        entity.LockExpiresAtTicks = ToTicks(record.LockExpiresAt);

        entity.Attempts.Clear();
        foreach(WebhookDeliveryAttempt attempt in record.Attempts) {
            entity.Attempts.Add(ToEntity(record.Id, attempt));
        }
    }

    public static WebhookJobRecord ToRecord(WebhookJobEntity entity) {
        WebhookJobRecord record = new(
            new WebhookJobId(entity.Id),
            new WebhookEndpointId(entity.EndpointId),
            entity.PartitionKey,
            entity.EventType,
            entity.SerializedPayload,
            FromTicks(entity.CreatedAtTicks)) {
            BatchId = entity.BatchId,
            HopCount = entity.HopCount,
            CausalChain = entity.CausalChain
        };

        record.Status = entity.Status;
        record.NextAttemptAt = FromTicks(entity.NextAttemptAtTicks);
        record.LockedBy = entity.LockedBy;
        record.LockExpiresAt = FromTicks(entity.LockExpiresAtTicks);

        foreach(WebhookDeliveryAttemptEntity attempt in entity.Attempts.OrderBy(static a => a.Id)) {
            record.AddAttempt(ToAttempt(attempt));
        }

        return record;
    }

    // ── Attempts ─────────────────────────────────────────────────────────────

    public static WebhookDeliveryAttemptEntity ToEntity(WebhookJobId jobId, WebhookDeliveryAttempt attempt) {
        WebhookDeliveryAttemptEntity entity = new() {
            JobId = jobId.Value,
            EndpointId = attempt.EndpointId.Value,
            AttemptNumber = attempt.AttemptNumber,
            AttemptedAtUnixMilliseconds = attempt.AttemptedAt.TotalMilliseconds,
            DurationTicks = attempt.Duration.Ticks,
            IsReplay = attempt.IsReplay
        };

        switch(attempt.Result) {
            case WebhookDeliveryResult.Delivered delivered:
                entity.ResultKind = Delivered;
                entity.StatusCode = delivered.StatusCode;
                entity.ResponseBody = delivered.ResponseBody;
                break;
            case WebhookDeliveryResult.Deduplicated deduplicated:
                entity.ResultKind = Deduplicated;
                entity.DeduplicationKey = deduplicated.DeduplicationKey;
                break;
            case WebhookDeliveryResult.TransientFailure transient:
                entity.ResultKind = TransientFailure;
                entity.ErrorMessage = transient.ErrorMessage;
                entity.StatusCode = transient.StatusCode;
                entity.RetryAfterTicks = transient.RetryAfter?.Ticks;
                entity.FailureReason = (int)transient.Reason;
                break;
            case WebhookDeliveryResult.PermanentFailure permanent:
                entity.ResultKind = PermanentFailure;
                entity.ErrorMessage = permanent.ErrorMessage;
                entity.StatusCode = permanent.StatusCode;
                entity.FailureReason = (int)permanent.Reason;
                break;
            default:
                throw new NotSupportedException(
                    $"Delivery result '{attempt.Result.GetType().FullName}' has no mapping in the EF Core webhook store.");
        }

        return entity;
    }

    public static WebhookDeliveryAttempt ToAttempt(WebhookDeliveryAttemptEntity entity) {
        WebhookDeliveryResult result = entity.ResultKind switch {
            Delivered => new WebhookDeliveryResult.Delivered(entity.StatusCode ?? 0, entity.ResponseBody),
            Deduplicated => new WebhookDeliveryResult.Deduplicated(entity.DeduplicationKey!),
            TransientFailure => new WebhookDeliveryResult.TransientFailure(
                entity.ErrorMessage!,
                entity.StatusCode,
                entity.RetryAfterTicks.HasValue ? TimeSpan.FromTicks(entity.RetryAfterTicks.Value) : null,
                (TransientFailureReason)(entity.FailureReason ?? 0)),
            PermanentFailure => new WebhookDeliveryResult.PermanentFailure(
                entity.ErrorMessage!,
                entity.StatusCode,
                (PermanentFailureReason)(entity.FailureReason ?? 0)),
            _ => throw new NotSupportedException($"Unknown delivery result kind '{entity.ResultKind}' in the webhook attempts table.")
        };

        return new WebhookDeliveryAttempt(
            new WebhookEndpointId(entity.EndpointId),
            entity.AttemptNumber,
            UnixTimestamp.FromMilliseconds(entity.AttemptedAtUnixMilliseconds),
            TimeSpan.FromTicks(entity.DurationTicks),
            result,
            entity.IsReplay);
    }

    // ── Subscriptions ────────────────────────────────────────────────────────

    public static WebhookSubscriptionEntity ToEntity(WebhookSubscription subscription) {
        WebhookSubscriptionEntity entity = new() {
            Id = subscription.Id.Value,
            CreatedAtTicks = ToTicks(subscription.CreatedAt)
        };

        CopyState(subscription, entity);
        return entity;
    }

    public static void CopyState(WebhookSubscription subscription, WebhookSubscriptionEntity entity) {
        entity.Namespace = subscription.Namespace.Value;
        entity.EndpointId = subscription.EndpointId.Value;
        entity.EventTypePattern = subscription.EventTypePattern;
        entity.FilterExpression = subscription.FilterExpression;
        entity.IsEnabled = subscription.IsEnabled;
        entity.Description = subscription.Description;
    }

    public static WebhookSubscription ToSubscription(WebhookSubscriptionEntity entity) {
        return new WebhookSubscription(
            new WebhookSubscriptionId(entity.Id),
            new WebhookNamespace(entity.Namespace),
            new WebhookEndpointId(entity.EndpointId),
            entity.EventTypePattern,
            FromTicks(entity.CreatedAtTicks)) {
            FilterExpression = entity.FilterExpression,
            IsEnabled = entity.IsEnabled,
            Description = entity.Description
        };
    }

    // ── Publish batches ──────────────────────────────────────────────────────

    public static WebhookPublishBatchEntity ToEntity(WebhookPublishBatchRecord batch) {
        string[] targets = new string[batch.TargetEndpoints.Count];
        for(int i = 0; i < targets.Length; i++) {
            targets[i] = batch.TargetEndpoints[i].Value;
        }

        WebhookPublishBatchEntity entity = new() {
            Id = batch.Id.Value,
            Namespace = batch.Namespace.Value,
            EventName = batch.EventName,
            SerializedPayload = batch.SerializedPayload,
            TargetEndpoints = JsonSerializer.Serialize(targets),
            CreatedAtTicks = ToTicks(batch.CreatedAt)
        };

        CopyState(batch, entity);
        return entity;
    }

    public static void CopyState(WebhookPublishBatchRecord batch, WebhookPublishBatchEntity entity) {
        entity.DispatchedCount = batch.DispatchedCount;
        entity.Status = batch.Status;
        entity.LockedBy = batch.LockedBy;
        entity.LockExpiresAtTicks = ToTicks(batch.LockExpiresAt);
        entity.UpdatedAtTicks = ToTicks(batch.UpdatedAt);
    }

    public static WebhookPublishBatchRecord ToBatch(WebhookPublishBatchEntity entity) {
        string[] targets = JsonSerializer.Deserialize<string[]>(entity.TargetEndpoints) ?? [];
        WebhookEndpointId[] endpoints = new WebhookEndpointId[targets.Length];
        for(int i = 0; i < targets.Length; i++) {
            endpoints[i] = new WebhookEndpointId(targets[i]);
        }

        return new WebhookPublishBatchRecord(
            new WebhookBatchId(entity.Id),
            new WebhookNamespace(entity.Namespace),
            entity.EventName,
            entity.SerializedPayload,
            endpoints,
            FromTicks(entity.CreatedAtTicks)) {
            DispatchedCount = entity.DispatchedCount,
            Status = entity.Status,
            LockedBy = entity.LockedBy,
            LockExpiresAt = FromTicks(entity.LockExpiresAtTicks),
            UpdatedAt = FromTicks(entity.UpdatedAtTicks)
        };
    }
}
