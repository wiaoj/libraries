using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.DependencyInjection;

public sealed class BloomFilterRegistrationShapeTests {
    private sealed record BlocklistTag;

    [Fact]
    public void Fluent_Overload_Registers_Filters_On_The_Returned_Builder() {
        ServiceCollection services = new();

        services.AddBloomFilter()
            .AddFilter<BlocklistTag>("blocklist", 1_000, 0.01);

        using ServiceProvider provider = services.BuildServiceProvider();
        IBloomFilter<BlocklistTag> filter = provider.GetRequiredService<IBloomFilter<BlocklistTag>>();
        filter.Add("item");

        Assert.Equal("blocklist", filter.Name);
        Assert.True(filter.Contains("item"));
    }

    [Fact]
    public void Fluent_Lifecycle_Features_Added_After_Return_Are_Registered() {
        ServiceCollection services = new();

        IBloomFilterBuilder builder = services.AddBloomFilter();
        builder.AddAutoSave().AddWarmUp();

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void Callback_Overload_Returns_The_Service_Collection() {
        ServiceCollection services = new();

        Assert.Same(services, services.AddBloomFilter(_ => { }));
    }
}
