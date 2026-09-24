using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;

namespace Wiaoj.Resilience.Tests.Unit.Configuration;

public sealed class ResilienceRegistrationShapeTests {
    private sealed class PaymentPolicy;

    [Fact]
    public async Task Fluent_Overload_Registers_Policies_On_The_Returned_Builder() {
        ServiceCollection services = new();
        services.AddDistributedCounter(c => c.UseInMemory());

        services.AddWiaojResilience()
            .AddConsecutiveBreaker<PaymentPolicy>(o => o.FailureThreshold = 2);

        using ServiceProvider provider = services.BuildServiceProvider();
        ICircuitBreaker<PaymentPolicy> breaker = provider.GetRequiredService<ICircuitBreaker<PaymentPolicy>>();

        Assert.True((await breaker.TryAcquireAsync("payments", TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public void Callback_Overload_Returns_The_Service_Collection() {
        ServiceCollection services = new();

        Assert.Same(services, services.AddWiaojResilience(_ => { }));
    }
}
