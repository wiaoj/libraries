using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;
using Wiaoj.Extensions.DependencyInjection;

namespace Wiaoj.Extensions.DependencyInjection.Tests;

public class AssemblyScanningExtensionsTests {
    [Fact]
    public void Scan_Should_Register_Services_From_Assembly_As_Interfaces() {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.Scan(scan => scan
            .FromAssemblyOf<AssemblyScanningExtensionsTests>()
            .AddClasses(t => t == typeof(TestService))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
        );

        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<ITestService>();
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
        Assert.Null(provider.GetService<TestService>());
    }

    [Fact]
    public void Scan_Should_Register_Services_With_Correct_Lifetime() {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.Scan(scan => scan
            .FromAssemblyOf<AssemblyScanningExtensionsTests>()
            .AddClasses(t => t == typeof(TestService))
            .AsImplementedInterfaces()
            .WithTransientLifetime()
        );
        var provider = services.BuildServiceProvider();

        // Assert
        var service1 = provider.GetService<ITestService>();
        var service2 = provider.GetService<ITestService>();

        Assert.NotNull(service1);
        Assert.NotNull(service2);
        // Transient olduğu için her çağrıda yeni instance gelmeli
        Assert.NotSame(service1, service2);
    }

    [Fact]
    public void Scan_Should_Register_Services_As_Self_If_Specified() {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.Scan(scan => scan
            .FromAssemblyOf<AssemblyScanningExtensionsTests>()
            .AddClasses()
            .AsSelf()
            .WithSingletonLifetime()
        );
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<TestService>();
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);

        // Interface olarak kaydedilmediği için null gelmeli
        Assert.Null(provider.GetService<ITestService>());
    }

    [Fact]
    public void Scan_Should_Register_Services_With_Predicate() {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.Scan(scan => scan
            .FromAssemblyOf<AssemblyScanningExtensionsTests>()
            // Sadece ismi "TestService" olan sınıfı al
            .AddClasses(type => type.Name == nameof(TestService))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
        );
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<ITestService>();
        Assert.NotNull(service);

        // OtherService predicate'e uymadığı için kaydedilmemeli
        Assert.Null(provider.GetService<IOtherService>());
    }

    // --- Dummy Types ---
    public interface ITestService { }
    public class TestService : ITestService { }

    public interface IOtherService { }
    public class OtherService : IOtherService { }
}