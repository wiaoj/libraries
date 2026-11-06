namespace Wiaoj.Webhooks.Tests.Unit.Retention;

[Trait("Category", "Unit")]
[Trait("Feature", "Retention")]
[Trait("Component", "Options")]
public sealed class WebhookRetentionOptionsTests {
    [Fact]
    public void Validate_AcceptsDefaults() {
        new WebhookRetentionOptions().Validate();
    }

    [Fact]
    public void Validate_Throws_WhenPollingIntervalIsNotPositive() {
        WebhookRetentionOptions options = new() { PollingInterval = TimeSpan.Zero };
        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Validate_Throws_WhenBatchSizeIsBelowOne(int batchSize) {
        WebhookRetentionOptions options = new() { BatchSize = batchSize };
        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Validate_Throws_WhenDeliveredRetentionIsNegative() {
        WebhookRetentionOptions options = new() { DeliveredRetention = TimeSpan.FromDays(-1) };
        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Validate_Throws_WhenDeadLetterRetentionIsNegative() {
        WebhookRetentionOptions options = new() { DeadLetterRetention = TimeSpan.FromDays(-1) };
        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Validate_AcceptsZeroRetention() {
        // A zero retention means finished jobs are pruned on the next sweep — allowed.
        new WebhookRetentionOptions { DeliveredRetention = TimeSpan.Zero, DeadLetterRetention = TimeSpan.Zero }.Validate();
    }
}
