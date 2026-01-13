using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using Wiaoj.Extensions.DependencyInjection;

namespace Wiaoj.Extensions.DependencyInjection.Tests;

public class DecoratorServiceCollectionExtensionsTests {
    [Fact]
    public void Decorate_Should_Decorate_Service() {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IService, ServiceImpl>();

        // Act
        services.Decorate<IService, Decorator>();
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetRequiredService<IService>();
        Assert.IsType<Decorator>(service);
    }

    [Fact]
    public void Decorate_Should_Call_Inner_Service() {
        // Arrange
        // NSubstitute ile mock (substitute) oluşturuyoruz
        var subService = Substitute.For<IService>();
        subService.DoSomething().Returns("Inner");

        var services = new ServiceCollection();
        // Inner service olarak substitute'u ekliyoruz
        services.AddTransient<IService>(sp => subService);

        // Decorator ile sarmalıyoruz
        services.Decorate<IService, Decorator>();

        var provider = services.BuildServiceProvider();

        // Act
        var decoratedService = provider.GetRequiredService<IService>();
        var result = decoratedService.DoSomething();

        // Assert
        Assert.Equal("Decorated: Inner", result);

        // Inner service'in çağrıldığını doğruluyoruz (Received = Verify)
        subService.Received(1).DoSomething();
    }

    [Fact]
    public void Decorate_With_Factory_Should_Decorate_Service() {
        // Arrange
        var subService = Substitute.For<IService>();
        subService.DoSomething().Returns("Inner");

        var services = new ServiceCollection();
        services.AddTransient<IService>(sp => subService);

        // Factory kullanarak dekorasyon
        services.Decorate<IService>((innerService, provider) => {
            return new FactoryDecorator(innerService);
        });

        var provider = services.BuildServiceProvider();

        // Act
        var decoratedService = provider.GetRequiredService<IService>();
        var result = decoratedService.DoSomething();

        // Assert
        Assert.Equal("FactoryDecorated: Inner", result);
        subService.Received(1).DoSomething();
    }

    [Fact]
    public void Decorate_With_Predicate_Should_Decorate_Matching_Service() {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IService, ServiceImpl>();
        services.AddTransient<IOtherService, OtherServiceImpl>();

        // Sadece ServiceImpl tipinde implementasyonu olanları dekore et
        services.Decorate(typeof(IService), typeof(Decorator), descriptor => descriptor.ImplementationType == typeof(ServiceImpl));

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetRequiredService<IService>();
        var otherService = provider.GetRequiredService<IOtherService>();

        // Assert
        Assert.IsType<Decorator>(service);
        Assert.IsType<OtherServiceImpl>(otherService); // Bu dekore edilmemeli
    }

    [Fact]
    public void TryDecorate_Should_Not_Throw_If_Service_Not_Found() {
        // Arrange
        var services = new ServiceCollection();
        // IService hiç eklenmedi.

        // Act & Assert
        var exception = Record.Exception(() => services.TryDecorate<IService, Decorator>());
        Assert.Null(exception); // Hata atmamalı
    }

    [Fact]
    public void Decorate_With_Open_Generic_Should_Replace_Implementation() {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient(typeof(IRepository<>), typeof(EfRepository<>));

        // Act
        services.Decorate(typeof(IRepository<>), typeof(CachedRepository<>));
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetRequiredService<IRepository<string>>();
        Assert.IsType<CachedRepository<string>>(service);
    }

    // --- Dummy Types ---
    public interface IService {
        string DoSomething();
    }
    public class ServiceImpl : IService {
        public virtual string DoSomething() => "Inner";
    }
    public class Decorator : IService {
        private readonly IService _inner;
        public Decorator(IService inner) {
            _inner = inner;
        }
        public string DoSomething() => "Decorated: " + _inner.DoSomething();
    }

    public interface IOtherService { }
    public class OtherServiceImpl : IOtherService { }

    public class FactoryDecorator : IService {
        private readonly IService _inner;
        public FactoryDecorator(IService inner) {
            _inner = inner;
        }
        public string DoSomething() => "FactoryDecorated: " + _inner.DoSomething();
    }

    public interface IRepository<T> { }
    public class EfRepository<T> : IRepository<T> { }
    public class CachedRepository<T> : IRepository<T> { }
}