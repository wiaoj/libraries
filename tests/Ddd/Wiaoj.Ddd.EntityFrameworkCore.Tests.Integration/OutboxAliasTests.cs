using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// An alias is written into a row and read back by a different process, possibly after a redeploy. It has to
/// be short enough to store, stable across rebuilds, and resolvable from a cold start.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Ddd")]
[Trait("Component", "Outbox")]
public sealed class OutboxAliasTests {

    public sealed record PlainEvent(Guid Id, DateTimeOffset OccurredAt) : IDomainEvent;
    public sealed record GenericEvent<TPayload, TChannel>(Guid Id, DateTimeOffset OccurredAt) : IDomainEvent;
    public sealed class Payload { }
    public sealed class Channel { }

    [Fact]
    public void An_Attributed_Type_Should_Use_Its_Alias() {
        OutboxAliasRegistry registry = new();

        Assert.Equal("invoices.raised.v1", registry.GetEventAlias(typeof(InvoiceRaised)));
    }

    [Fact]
    public void A_Generic_Type_Should_Not_Carry_Assembly_Or_Version_Information() {
        OutboxAliasRegistry registry = new();

        string alias = registry.GetEventAlias(typeof(GenericEvent<Payload, Channel>));

        // Type.FullName embeds Version= for every type argument, so a version bump would change the alias and
        // orphan every row already written under the old one.
        Assert.DoesNotContain("Version=", alias, StringComparison.Ordinal);
        Assert.DoesNotContain("PublicKeyToken", alias, StringComparison.Ordinal);
        Assert.DoesNotContain("Culture=", alias, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Generic_Type_Should_Fit_The_Default_Column_Width() {
        OutboxAliasRegistry registry = new();

        string alias = registry.GetEventAlias(typeof(GenericEvent<Payload, Channel>));

        // The same type's FullName is 400+ characters, which no sane alias column would hold.
        Assert.True(alias.Length <= new OutboxModelOptions().AliasMaxLength,
            $"Alias is {alias.Length} characters: {alias}");
    }

    [Fact]
    public void A_Generic_Type_Should_Still_Name_Its_Arguments() {
        OutboxAliasRegistry registry = new();

        string alias = registry.GetEventAlias(typeof(GenericEvent<Payload, Channel>));

        Assert.Contains(nameof(Payload), alias, StringComparison.Ordinal);
        Assert.Contains(nameof(Channel), alias, StringComparison.Ordinal);
        Assert.NotEqual(alias, registry.GetEventAlias(typeof(GenericEvent<Channel, Payload>)));
    }

    [Fact]
    public void A_Registered_Type_Should_Resolve_By_Its_Alias() {
        OutboxAliasRegistry registry = new();
        registry.Register(typeof(PlainEvent));

        Assert.Equal(typeof(PlainEvent), registry.ResolveEventType(registry.GetEventAlias(typeof(PlainEvent))));
    }

    [Fact]
    public void An_Unregistered_Alias_Should_Resolve_To_Nothing() {
        OutboxAliasRegistry registry = new();

        Assert.Null(registry.ResolveEventType("something.that.was.deleted"));
    }
}

/// <summary>
/// A row written before a restart must resolve on the very first poll after it. Seeding the registry only at
/// enqueue time meant a cold process could not resolve anything it had not just written — and an
/// unresolvable row is dead-lettered, not retried.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Ddd")]
[Trait("Component", "Outbox")]
public sealed class OutboxAliasRegistrySeedingTests {

    [Fact]
    public void Should_Resolve_An_Event_Type_Without_Anything_Having_Been_Enqueued_First() {
        // A cold process: handlers registered, nothing published yet.
        ServiceCollection services = new();
        services.AddScoped<IPostDomainEventHandler<InvoiceRaised>, LedgerHandler>();
        services.AddSingleton(new HandlerLog());
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDdd(ddd => ddd.AddEntityFrameworkCore<OutboxTestContext>());

        IOutboxAliasRegistry registry = services.BuildServiceProvider().GetRequiredService<IOutboxAliasRegistry>();

        Assert.Equal(typeof(InvoiceRaised), registry.ResolveEventType("invoices.raised.v1"));
    }
}
