using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Wiaoj.Extensions.DependencyInjection;

namespace Wiaoj.Extensions.DependencyInjection.Tests;

public class ServiceCollectionExtensionsTests {
    [Fact]
    public void Replace_Should_Replace_Service() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IService, ServiceImpl>();

        // Act
        // ServiceLifetime.Scoped olarak değiştiriyoruz
        services.Replace<IService, AnotherServiceImpl>(ServiceLifetime.Scoped);
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetRequiredService<IService>();
        Assert.IsType<AnotherServiceImpl>(service);
    }

    [Fact]
    public void AddLazySupport_Should_Add_Lazy_Support() {
        // Arrange
        ServiceCollection services = new();
        services.AddLazySupport();
        services.AddTransient<IMyService, MyServiceImpl>();

        // Act
        var provider = services.BuildServiceProvider();
        var lazyService = provider.GetService<Lazy<IMyService>>();

        // Assert
        Assert.NotNull(lazyService);
        // Lazy değerine erişildiğinde servisin doğru tipte geldiğini kontrol et
        Assert.IsType<MyServiceImpl>(lazyService.Value);
    }

    // --- Dummy Types ---
    public interface IService { }
    public class ServiceImpl : IService { }
    public class AnotherServiceImpl : IService { }

    public interface IMyService { }
    public class MyServiceImpl : IMyService { }
}