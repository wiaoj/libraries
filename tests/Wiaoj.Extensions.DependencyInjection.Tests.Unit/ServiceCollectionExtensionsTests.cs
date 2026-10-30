using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Wiaoj.Extensions.DependencyInjection;

namespace Wiaoj.Extensions.DependencyInjection.Tests;

public class ServiceCollectionExtensionsTests {
    // Regression for issue #217: before the fix every public Replace overload self-resolved
    // inside the Microsoft.Extensions.DependencyInjection namespace and killed the process
    // with "Stack overflow." before the assertion below could run.
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

        // The registration was replaced, not added: exactly one descriptor for IService,
        // pointing at the new implementation with the requested lifetime.
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IService));
        Assert.Equal(typeof(AnotherServiceImpl), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    // Overload 2 (Replace<TService>(instance)). Asserts the descriptor really became a
    // singleton-instance registration for the given object, not a duplicate Add.
    [Fact]
    public void Replace_Instance_Should_Replace_Service_With_Instance() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IService, ServiceImpl>();
        IService instance = new AnotherServiceImpl();

        // Act
        services.Replace(instance);

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IService));
        Assert.Same(instance, descriptor.ImplementationInstance);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        var provider = services.BuildServiceProvider();
        Assert.Same(instance, provider.GetRequiredService<IService>());
    }

    // Overload 3 (Replace<TService>(factory, lifetime)). Asserts the resolved object comes
    // from the new factory and the collection holds exactly the replaced descriptor.
    [Fact]
    public void Replace_Factory_Should_Replace_Service_With_Factory() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IService, ServiceImpl>();
        var created = new AnotherServiceImpl();

        // Act
        services.Replace<IService>(_ => created, ServiceLifetime.Scoped);

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IService));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);

        var provider = services.BuildServiceProvider();
        Assert.Same(created, provider.GetRequiredService<IService>());
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
