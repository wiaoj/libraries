# Wiaoj.Webhooks.Testing

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Official test doubles, pre-wired test harness, and fluent assertion library for **Wiaoj.Webhooks** and **Wiaoj.Webhooks.Publishing**.

Designed to test application business logic, background workers, and ASP.NET Core integration tests (`WebApplicationFactory`) without relying on dynamic mocking frameworks (Moq, NSubstitute).

---

## 📑 Table of Contents

- [Overview](#-overview)
- [Available Test Doubles](#-available-test-doubles)
- [Usage Scenarios](#-usage-scenarios)
  - [1. Unit Testing Application Services](#1-unit-testing-application-services)
  - [2. Using the Pre-Wired WebhookTestContext](#2-using-the-pre-wired-webhooktestcontext)
  - [3. Integration Testing with WebApplicationFactory](#3-integration-testing-with-webapplicationfactory)
  - [4. Testing 1-to-N Publishing Fan-Out](#4-testing-1-to-n-publishing-fan-out)
  - [5. Simulating Failures with FakeWebhookDeliverer](#5-simulating-failures-with-fakewebhookdeliverer)
- [Fluent Assertion API](#-fluent-assertion-api)
- [Ecosystem Packages](#-ecosystem-packages)
- [License](#-license)

---

## 🔍 Overview

`Wiaoj.Webhooks.Testing` provides thread-safe, controllable in-memory doubles of all core webhook contracts. It eliminates boilerplate mock setups and offers domain-specific assertion methods to verify events dispatched, jobs enqueued, and delivery attempts captured.

---

## Available Test Doubles

| Test Double | Interface Implemented | Description |
|---|---|---|
| **`FakeWebhookDispatcher`** | `IWebhookDispatcher` | Captures single and batch dispatches, tracks payloads, supports cancellation simulation. |
| **`FakeWebhookPublisher`** | `IWebhookPublisher` | Captures 1-to-N fan-out events across logical isolation namespaces. |
| **`FakeWebhookTransport`** | `IWebhookTransport` | Captures enqueued units of work, delayed retry timers, and batch writes. |
| **`FakeWebhookEndpointResolver`** | `IWebhookEndpointResolver` | In-memory directory for registering and resolving target `WebhookEndpoint` definitions. |
| **`FakeWebhookDeliverer`** | `IWebhookDeliverer` | Simulates HTTP transmission outcomes (200 OK, 5xx transient, 4xx permanent, timeouts) and captures contexts. |
| **`WebhookTestContext`** | *(Orchestrator)* | Pre-wires all test doubles with a deterministic `FakeTimeProvider`. |

---

## Usage Scenarios

### 1. Unit Testing Application Services

Test domain services that depend on `IWebhookDispatcher` directly:

```csharp
public class OrderService(IWebhookDispatcher dispatcher, IOrderRepository repository)
{
    public async Task CompleteOrderAsync(string orderId, decimal amount, CancellationToken ct)
    {
        await repository.MarkCompletedAsync(orderId, ct);
        await dispatcher.DispatchAsync(new WebhookEndpointId("ep_customer_1"), new OrderCompletedEvent(orderId, amount), ct);
    }
}

// Unit Test
public sealed class OrderServiceTests
{
    [Fact]
    public async Task CompleteOrder_DispatchesWebhookEvent()
    {
        var dispatcher = new FakeWebhookDispatcher();
        var repository = new FakeOrderRepository();
        var service = new OrderService(dispatcher, repository);

        await service.CompleteOrderAsync("ORD-100", 250m, CancellationToken.None);

        dispatcher.ShouldHaveDispatched<OrderCompletedEvent>(new WebhookEndpointId("ep_customer_1"));
        dispatcher.ShouldHaveDispatchCount(1);
    }
}
```

---

### 2. Using the Pre-Wired `WebhookTestContext`

When testing components that interact with multiple parts of the webhook engine, `WebhookTestContext` provides all doubles ready out of the box with deterministic time control:

```csharp
[Fact]
public async Task ProcessPayment_DispatchesAndEnqueuesJob()
{
    var context = new WebhookTestContext();
    var service = new PaymentService(context.Dispatcher);

    await service.CapturePaymentAsync("PAY-99", CancellationToken.None);

    context.Dispatcher.ShouldHaveDispatched<PaymentCapturedEvent>();
    Assert.True(context.Dispatcher.Calls[0].PartitionKey.Value == "PAY-99");
}
```

---

### 3. Integration Testing with `WebApplicationFactory`

Replace real network transports, HTTP senders, and dispatchers in ASP.NET Core integration tests with a single line:

```csharp
public sealed class WebhookIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebhookIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddWiaojWebhooks(webhooks =>
                {
                    // Replaces dispatcher, transport, and deliverer with test doubles in DI
                    webhooks.UseFakeInfrastructure(out WebhookTestContext testContext);
                });
            });
        });
    }

    [Fact]
    public async Task PostOrderApi_TriggersWebhookDispatch()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new { OrderId = "ORD-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

---

### 4. Testing 1-to-N Publishing Fan-Out

Verify events published across tenant namespaces:

```csharp
[Fact]
public async Task PublishInvoice_BroadcastsToNamespace()
{
    var publisher = new FakeWebhookPublisher();
    var service = new BillingService(publisher);

    await service.FinalizeInvoiceAsync("INV-500", "tenant-eu", CancellationToken.None);

    Assert.Single(publisher.Calls);
    Assert.Equal("tenant-eu", publisher.Calls[0].Namespace.Value);
}
```

---

### 5. Simulating Failures with `FakeWebhookDeliverer`

Simulate transient or permanent failures in pipeline tests:

```csharp
[Fact]
public async Task Pipeline_WhenDelivererReturns503_HandlesTransientFailure()
{
    // Configure deliverer to return 503 on attempt 1, and 200 on attempt 2
    var deliverer = new FakeWebhookDeliverer(
        WebhookDeliveryResult.Transient("Service Unavailable", 503),
        WebhookDeliveryResult.Success(200));

    var runner = new WebhookPipelineRunner([], deliverer, TimeProvider.System, NullLogger<WebhookPipelineRunner>.Instance);
    var context = WebhookTestFactory.CreateContext();

    var attempt1 = await runner.RunAsync(context);

    Assert.False(attempt1.IsSuccess);
    Assert.Equal(503, ((WebhookDeliveryResult.TransientFailure)attempt1.Result).StatusCode);
}
```

---

## Fluent Assertion API

`Wiaoj.Webhooks.Testing` provides extension methods that throw clear `InvalidOperationException` diagnostic messages on assertion failures:

### Dispatcher Assertions
```csharp
// Assert event type was dispatched
dispatcher.ShouldHaveDispatched<OrderCreatedEvent>();

// Assert event was dispatched to a specific endpoint
dispatcher.ShouldHaveDispatched<OrderCreatedEvent>(endpointId);

// Assert with custom payload predicate
dispatcher.ShouldHaveDispatched<OrderCreatedEvent>(endpointId, e => e.Amount > 100m);

// Assert an endpoint was never dispatched to
dispatcher.ShouldNotHaveDispatched(endpointId);

// Assert total dispatch count
dispatcher.ShouldHaveDispatchCount(3);
```

### Transport Assertions
```csharp
// Assert job ID was enqueued
transport.ShouldHaveEnqueued(jobId);

// Assert endpoint was targeted in queue
transport.ShouldHaveEnqueued(endpointId);

// Assert total enqueued work count
transport.ShouldHaveEnqueuedCount(5);
```

### Deliverer Assertions
```csharp
// Assert HTTP delivery was attempted to endpoint
deliverer.ShouldHaveDeliveredTo(endpointId);

// Assert total transmission attempt count
deliverer.ShouldHaveDeliveryCount(2);
```