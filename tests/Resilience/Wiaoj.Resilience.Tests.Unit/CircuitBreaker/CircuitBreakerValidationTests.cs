using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.Internal;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "Validation")]
public sealed class CircuitBreakerValidationTests {

    private static DistributedCircuitBreakerStore CreateStore() {
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        return new DistributedCircuitBreakerStore(
            counterFactory,
            TimeProvider.System,
            NullLogger<DistributedCircuitBreakerStore>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Methods_ThrowArgumentException_WhenKeyIsNullOrWhitespace(string? invalidKey) {
        DistributedCircuitBreakerStore store = CreateStore();
        CircuitBreakerOptions options = new();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            store.CanExecuteAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            store.RecordSuccessAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            store.RecordFailureAsync(invalidKey!, options, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public void CircuitBreakerOptions_Validate_Throws_WhenValuesAreInvalid() {
        CircuitBreakerOptions optionsZeroThreshold = new() { FailureThreshold = 0 };
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => optionsZeroThreshold.Validate());

        CircuitBreakerOptions optionsNegativeThreshold = new() { FailureThreshold = -1 };
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => optionsNegativeThreshold.Validate());

        CircuitBreakerOptions optionsZeroBreak = new() { BreakDuration = TimeSpan.Zero };
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => optionsZeroBreak.Validate());

        CircuitBreakerOptions optionsNegativeBreak = new() { BreakDuration = TimeSpan.FromSeconds(-5) };
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => optionsNegativeBreak.Validate());
    }

    [Fact]
    public void CircuitExecutionDecision_Denied_Throws_WhenRetryAfterIsNegative() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(-1)));
    }
}