using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Extensions.DependencyInjection.Tests.Unit;

public sealed class DecoratorTests {
    public interface IService { string GetValue(); }
    public class BaseService : IService {
        public string GetValue() {
            return "Base";
        }
    }
    public class Decorator : IService {
        private readonly IService _inner;
        public Decorator(IService inner) {
            this._inner = inner;
        }

        public string GetValue() {
            return this._inner.GetValue() + "-Decorated";
        }
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void Decorate_ShouldWrapService() {
        ServiceCollection services = new();
        services.AddTransient<IService, BaseService>();
        services.Decorate<IService, Decorator>();

        var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<IService>();

        Assert.Equal("Base-Decorated", instance.GetValue());
    }

    [Fact]
    [Trait("Category", "Basic")]
    public void TryDecorate_ShouldDoNothing_IfServiceNotFound() {
        ServiceCollection services = new();
        // IService not registered
        services.TryDecorate<IService, Decorator>();

        Assert.Empty(services);
    }

    [Fact]
    [Trait("Category", "Predicate")]
    public void Decorate_WithPredicate_ShouldOnlyDecorateMatching() {
        ServiceCollection services = new();
        services.AddTransient<IService, BaseService>();
        services.AddTransient<IService, BaseService>(); // İkinci bir servis

        // Sadece birini decorate et
        services.Decorate(typeof(IService), typeof(Decorator), desc => desc.ImplementationType == typeof(BaseService));

        var provider = services.BuildServiceProvider();
        List<IService> instances = provider.GetServices<IService>().ToList();

        Assert.Equal(2, instances.Count);
        Assert.Contains(instances, i => i.GetValue() == "Base-Decorated");
    }

    [Fact]
    [Trait("Category", "Factory")]
    public void Decorate_WithFactory_ShouldUseProvidedLogic() {
        ServiceCollection services = new();
        services.AddTransient<IService, BaseService>();

        services.Decorate<IService>((inner, sp) => new Decorator(inner));

        var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<IService>();

        Assert.Equal("Base-Decorated", instance.GetValue());
    }

    [Fact]
    [Trait("Category", "Keyed")]
    public void Decorate_WithKeyedService_ShouldMaintainKey() {
        ServiceCollection services = new();
        var key = "test-key";
        services.AddKeyedTransient<IService, BaseService>(key);

        services.Decorate(typeof(IService), typeof(Decorator));

        var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredKeyedService<IService>(key);

        Assert.Equal("Base-Decorated", instance.GetValue());
    }

    [Fact]
    [Trait("Category", "Keyed")]
    public void Decorate_Keyed_ShouldOnlyDecorateTargetKey() {
        var services = new ServiceCollection();
        services.AddKeyedTransient<IService, BaseService>("A");
        services.AddKeyedTransient<IService, BaseService>("B");

        // Sadece "A" anahtarlısını dekore et
        services.Decorate(typeof(IService), typeof(Decorator), desc => desc.ServiceKey?.Equals("A") ?? false);

        var provider = services.BuildServiceProvider();

        Assert.Equal("Base-Decorated", provider.GetRequiredKeyedService<IService>("A").GetValue());
        Assert.Equal("Base", provider.GetRequiredKeyedService<IService>("B").GetValue());
    }

    [Fact]
    [Trait("Category", "ErrorHandling")]
    public void Decorate_ThrowsException_IfServiceNotFound() {
        ServiceCollection services = new();

        Assert.Throws<InvalidOperationException>(() =>
            services.Decorate<IService, Decorator>());
    }

    [Fact]
    [Trait("Category", "OpenGeneric")]
    public void Decorate_OpenGeneric_ShouldThrowNotSupportedException() {
        ServiceCollection services = new();

        services.AddTransient(typeof(IEnumerable<>), typeof(List<>));

        // ACT & ASSERT
        var exception = Assert.Throws<NotSupportedException>(() =>
            services.Decorate(typeof(IEnumerable<>), typeof(List<>))
        );

        // Hata mesajının doğru geldiğini de doğrulayalım (Opsiyonel ama şık durur)
        Assert.Contains("is not supported natively", exception.Message);
    }

    [Fact]
    [Trait("Category", "Lifetime")]
    public void Decorate_ShouldPreserveLifetime() {
        var services = new ServiceCollection();
        services.AddSingleton<IService, BaseService>();
        services.Decorate<IService, Decorator>();

        var descriptor = services.First(d => d.ServiceType == typeof(IService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    [Trait("Category", "Chaining")]
    public void Decorate_MultipleTimes_ShouldCreateChain() {
        var services = new ServiceCollection();
        services.AddTransient<IService, BaseService>();

        services.Decorate<IService, Decorator>(); // Biri
        services.Decorate<IService, Decorator>(); // İkincisi

        var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<IService>();

        // Base -> Decorator -> Decorator
        Assert.Equal("Base-Decorated-Decorated", instance.GetValue());
    }

    public class Dependency { public string Name => "Injected"; }

    public class ComplexDecorator : IService {
        private readonly IService _inner;
        private readonly Dependency _dep;

        // Decorator hem IService'i hem de konteynerden gelen başka bir nesneyi alıyor
        public ComplexDecorator(IService inner, Dependency dep) {
            _inner = inner;
            _dep = dep;
        }

        public string GetValue() => $"{_inner.GetValue()}-{_dep.Name}";
    }

    [Fact]
    [Trait("Category", "Complex")]
    public void Decorate_WithAdditionalDependencies_ShouldResolveCorrectly() {
        var services = new ServiceCollection();

        // Hem servisi hem de dekoratörün ihtiyaç duyacağı bağımlılığı ekle
        services.AddTransient<IService, BaseService>();
        services.AddTransient<Dependency>();

        // Dekore et
        services.Decorate<IService, ComplexDecorator>();

        var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<IService>();

        // Result "Base-Injected" olmalı
        Assert.Equal("Base-Injected", instance.GetValue());
    }

    [Fact]
    [Trait("Category", "Hardcore")]
    public void Decorate_Hardcore_ScopedDependencyInSingletonDecorator() {
        // Scoped bir bağımlılık (Konteynerden gelecek)
        var services = new ServiceCollection();

        services.AddSingleton<IService, BaseService>(); // Singleton
        services.AddScoped<Dependency>();              // Scoped

        // Hardcore Senaryo:
        // Singleton bir servis, Scoped bir Dependency'i dekoratör aracılığıyla alıyor.
        // Bu, DI container'ın "Captive Dependency" (veya validasyon) mekanizmasını tetikleyebilir.
        // Senin sistemin bunu "runtime factory" ile çözebiliyor mu?
        services.Decorate<IService, ComplexDecorator>();

        var provider = services.BuildServiceProvider();

        // Scope oluştur
        using(var scope = provider.CreateScope()) {
            var service = scope.ServiceProvider.GetRequiredService<IService>();

            // Assert: Hem singleton'dan gelen Base hem de scope'tan gelen Injected
            Assert.Equal("Base-Injected", service.GetValue());
        }
    }

    [Fact]
    [Trait("Category", "Hardcore")]
    public void Decorate_RecursiveChain_ShouldNotCauseStackOverflow() {
        var services = new ServiceCollection();
        services.AddTransient<IService, BaseService>();

        // Decorator-1: Base -> Dec1
        services.Decorate<IService, Decorator>();
        // Decorator-2: Base -> Dec1 -> Dec2
        services.Decorate<IService, Decorator>();

        var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<IService>();

        // Sonsuz döngüye girip girmediğini, factory'lerin doğru bağlanıp bağlanmadığını test et
        Assert.Equal("Base-Decorated-Decorated", instance.GetValue());
    }
}