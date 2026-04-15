using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Wiaoj.Extensions.DependencyInjection;
using Xunit;

namespace Wiaoj.Extensions.DependencyInjection.Tests.Unit;

public sealed class GenericDecoratorTests {
    // Test Generic Tipleri
    public interface IRepository<T> { string GetData(); }
    public class BaseRepository<T> : IRepository<T> {
        public string GetData() {
            return $"Base-{typeof(T).Name}";
        }
    }

    // Generic Decorator: IRepository<T>'yi alır, kendisine de T'yi taşır
    public class CachedRepository<T> : IRepository<T> {
        private readonly IRepository<T> _inner;
        public CachedRepository(IRepository<T> inner) {
            this._inner = inner;
        }

        public string GetData() {
            return this._inner.GetData() + "-Cached";
        }
    }

    [Fact]
    [Trait("Category", "Generic")]
    public void Decorate_ClosedGenerics_ShouldSuccessfullyWrapAndChain() {
        ServiceCollection services = new();

        // Kullanıcılar tiplerini kapalı olarak kaydeder
        services.AddTransient<IRepository<int>, BaseRepository<int>>();
        services.AddTransient<IRepository<string>, BaseRepository<string>>();

        // Kapalı tipler üzerinde Decorator uygulanır (Senin harika kodun burada uçar)
        services.Decorate(typeof(IRepository<int>), typeof(CachedRepository<int>));
        services.Decorate(typeof(IRepository<string>), typeof(CachedRepository<string>));

        ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal("Base-Int32-Cached", provider.GetRequiredService<IRepository<int>>().GetData());
        Assert.Equal("Base-String-Cached", provider.GetRequiredService<IRepository<string>>().GetData());
    }
}