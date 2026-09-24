using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd.DomainEvents;

namespace Wiaoj.Ddd.Tests.Unit;

public class DddRegistrationTests {
    [Fact]
    public void AddDdd_Returns_One_Builder_Per_Service_Collection() {
        ServiceCollection services = new();

        IDddBuilder first = services.AddDdd();
        IDddBuilder second = services.AddDdd();

        Assert.Same(first, second);
        Assert.Same(services, first.Services);
        Assert.Single(services, d => d.ServiceType == typeof(IDddBuilder));
        Assert.Single(services, d => d.ServiceType == typeof(IDomainEventDispatcher));
    }

    [Fact]
    public void AddDdd_With_Callback_Configures_The_Shared_Builder_And_Returns_The_Services() {
        ServiceCollection services = new();
        IDddBuilder? configured = null;

        IServiceCollection returned = services.AddDdd(ddd => configured = ddd);

        Assert.Same(services, returned);
        Assert.Same(services.AddDdd(), configured);
    }
}
