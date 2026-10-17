using System.Collections;
using System.Reflection;
using Wiaoj.Primitives;
using Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Infrastructure;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.EfCore;

/// <summary>
/// Behaviour specific to the EF Core stores: what the mapping carries and what survives a restart.
/// </summary>
public abstract class EfCoreStoreTests : StoreTestBase {
    /// <summary>
    /// Sets every public property of <see cref="WebhookJobRecord"/> to a non-default value and requires each one to come
    /// back from the store unchanged. A property added to the record but not to the mapping fails here; so does a
    /// property of a type this test cannot fill, until a sample value is added below.
    /// </summary>
    [Fact]
    public async Task EveryPublicPropertyOfWebhookJobRecord_SurvivesTheStore() {
        WebhookJobRecord job = new(
            WebhookJobId.NewJobId(),
            new WebhookEndpointId("ep-mapping"),
            "partition-mapping",
            "event.mapping",
            """{"mapped":true}""",
            new DateTimeOffset(2025, 3, 4, 5, 6, 7, TimeSpan.FromHours(3)).AddTicks(1234));

        PropertyInfo[] properties = typeof(WebhookJobRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach(PropertyInfo property in properties.Where(p => p.CanWrite)) {
            property.SetValue(job, SampleValue(property.PropertyType, property.Name));
        }

        UnixTimestamp at = UnixTimestamp.FromMilliseconds(1_750_000_000_123);
        job.AddAttempt(WebhookDeliveryAttempt.Create(job.EndpointId, 1, at, TimeSpan.FromTicks(98765),
            new WebhookDeliveryResult.TransientFailure("timeout", null, TimeSpan.FromSeconds(12), TransientFailureReason.Timeout)));
        job.AddAttempt(WebhookDeliveryAttempt.CreateReplay(job.EndpointId, 2, at.AddSeconds(1), TimeSpan.FromMilliseconds(5),
            new WebhookDeliveryResult.Delivered(202, "accepted")));

        foreach(PropertyInfo property in properties) {
            Assert.False(IsDefault(property.GetValue(job)),
                $"WebhookJobRecord.{property.Name} still has its default value, so this test cannot prove it is mapped. Give it a value above.");
        }

        await this.JobStore.SaveAsync(job, TestContext.Current.CancellationToken);
        await RestartAsync();
        WebhookJobRecord? loaded = await this.JobStore.GetJobAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        foreach(PropertyInfo property in properties) {
            object? expected = property.GetValue(job);
            object? actual = property.GetValue(loaded);

            if(expected is IEnumerable expectedItems and not string) {
                Assert.True(
                    expectedItems.Cast<object?>().SequenceEqual(((IEnumerable)actual!).Cast<object?>()),
                    $"WebhookJobRecord.{property.Name} did not survive the EF Core store. Map it in WebhookEntityMapper and WebhookJobEntity.");
            }
            else {
                Assert.True(
                    Equals(expected, actual),
                    $"WebhookJobRecord.{property.Name} did not survive the EF Core store (expected '{expected}', got '{actual}'). Map it in WebhookEntityMapper and WebhookJobEntity.");
            }
        }
    }

    private static object SampleValue(Type type, string name) {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if(underlying == typeof(string)) return $"sample-{name}";
        if(underlying == typeof(int)) return 7;
        if(underlying == typeof(long)) return 7L;
        if(underlying == typeof(bool)) return true;
        if(underlying == typeof(DateTimeOffset)) return new DateTimeOffset(2025, 6, 7, 8, 9, 10, TimeSpan.FromHours(-5)).AddTicks(name.Length);
        if(underlying == typeof(TimeSpan)) return TimeSpan.FromTicks(4321);
        if(underlying == typeof(Guid)) return Guid.CreateVersion7();
        if(underlying.IsEnum) {
            Array values = Enum.GetValues(underlying);
            return values.GetValue(values.Length - 1)!;
        }

        throw new NotSupportedException(
            $"WebhookJobRecord.{name} has type {type}, which this test cannot fill. Add a sample value for it here and map the property in WebhookEntityMapper.");
    }

    private static bool IsDefault(object? value) {
        return value switch {
            null => true,
            IEnumerable items and not string => !items.Cast<object?>().Any(),
            _ when value.GetType().IsValueType => value.Equals(Activator.CreateInstance(value.GetType())),
            _ => false
        };
    }

    [Fact]
    public async Task StoredState_SurvivesARestart() {
        WebhookJobRecord job = new(WebhookJobId.NewJobId(), new WebhookEndpointId("ep-1"), "order.created", "{}", this.Time.GetUtcNow());
        await this.JobStore.SaveAsync(job, TestContext.Current.CancellationToken);
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken));
        await this.JobStore.UpdateStatusAsync(job.Id, WebhookJobStatus.Retrying, this.Time.GetUtcNow().AddMinutes(1), TestContext.Current.CancellationToken);

        WebhookSubscription subscription = new(new WebhookEndpointId("ep-1"), "order.*");
        await this.SubscriptionStore.SaveSubscriptionAsync(subscription, TestContext.Current.CancellationToken);

        WebhookPublishBatchRecord batch = new(WebhookBatchId.NewId(), WebhookNamespace.Default, "order.created", "{}", [new WebhookEndpointId("ep-1")], this.Time.GetUtcNow());
        await this.BatchStore.SaveBatchAsync(batch, TestContext.Current.CancellationToken);

        await RestartAsync();

        WebhookJobRecord? reloaded = await this.JobStore.GetJobAsync(job.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(reloaded);
        Assert.Equal(WebhookJobStatus.Retrying, reloaded.Status);
        Assert.Equal([job.Id], (await this.JobStore.GetStaleJobsAsync(null, null, this.Time.GetUtcNow().AddMinutes(2), 10, TestContext.Current.CancellationToken)).Select(j => j.Id));
        Assert.NotNull(await this.SubscriptionStore.GetSubscriptionAsync(subscription.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await this.BatchStore.GetBatchAsync(batch.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_ExistingJob_ReplacesItsStateAndAttempts() {
        WebhookJobRecord job = new(WebhookJobId.NewJobId(), new WebhookEndpointId("ep-1"), "order.created", "{}", this.Time.GetUtcNow());
        job.AddAttempt(WebhookDeliveryAttempt.Create(job.EndpointId, 1, UnixTimestamp.From(this.Time.GetUtcNow()), TimeSpan.Zero, WebhookDeliveryResult.Transient("boom")));
        await this.JobStore.SaveAsync(job, TestContext.Current.CancellationToken);

        WebhookJobRecord replacement = new(job.Id, job.EndpointId, job.EventType, job.SerializedPayload, job.CreatedAt) {
            Status = WebhookJobStatus.Delivered
        };
        await this.JobStore.SaveAsync(replacement, TestContext.Current.CancellationToken);

        WebhookJobRecord? loaded = await this.JobStore.GetJobAsync(job.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Equal(WebhookJobStatus.Delivered, loaded.Status);
        Assert.Empty(loaded.Attempts);
    }
}

public sealed class SqliteEfCoreStoreTests : EfCoreStoreTests {
    protected override StoreBackend CreateBackend() => new SqliteStoreBackend();
}

public sealed class PostgreSqlEfCoreStoreTests : EfCoreStoreTests {
    protected override StoreBackend CreateBackend() => new PostgreSqlStoreBackend();
}
