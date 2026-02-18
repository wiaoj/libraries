using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.Internal;

namespace Wiaoj.Ddd.Tests.Unit;

public class DddBuilderTests {
    [Fact]
    public void ScanAssemblies_Should_Register_Handlers() {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DddBuilder(services);

        // Act
        // İçinde handler olan assembly'i tara (Current Assembly)
        builder.ScanDomainEventHandlers(ServiceLifetime.Scoped, typeof(DddBuilderTests).Assembly);


        // Assert
        // TestHandler'ın kaydedilip kaydedilmediğini kontrol et
        // (Burada dummy bir event ve handler oluşturup test etmelisin)
        var descriptor = services.FirstOrDefault(s => s.ImplementationType == typeof(TestDomainEventHandler));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    // Dummy types for test
    public record TestEvent : DomainEvent;
    public class TestDomainEventHandler : IPreDomainEventHandler<TestEvent> {
        public ValueTask Handle(TestEvent @event, CancellationToken ct) => ValueTask.CompletedTask;
    }
}