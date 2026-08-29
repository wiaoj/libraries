# Wiaoj.Compensation

A lightweight, in-process reversible execution pipeline for .NET. Coordinates sequential operations across non-transactional resources with deterministic Last-In-First-Out (LIFO) compensation and isolated rollback cancellation.

---

## Overview

In modern backend services, operations frequently touch heterogeneous, non-transactional resources (e.g., local file system, cloud storage, Redis caches, external payment gateways). Database transactions cannot coordinate or roll back these side effects.

`Wiaoj.Compensation` provides a structured, low-allocation execution engine that executes steps sequentially and automatically unwinds completed steps in reverse order if any step faults or is cancelled.

### Scope Boundaries

* **What it is:** An in-process coordinator for synchronous/short-lived multi-step operations executing within the same call stack.
* **What it is not:** A distributed saga engine or a durable workflow orchestrator across message queues. For cross-service messaging over brokers (RabbitMQ/Kafka) with persistent outbox storage, use a distributed saga framework.

---

## Key Architectural Guarantees

1. **Deterministic LIFO Rollback:** Succeeded steps are tracked on an internal execution stack. Upon failure, their compensation actions are invoked in reverse order.
2. **Isolated Rollback Cancellation:** If an execution fails due to caller cancellation (`OperationCanceledException` / HTTP request abortion), the rollback phase executes under an isolated, bounded timeout (`TimeSpan`), preventing cleanup routines from being aborted prematurely.
3. **Best-Effort Fault Tolerance:** If a compensation step throws an exception during rollback, the engine records the failure into `CompensationReport.CompensationErrors` and continues executing the remaining compensation steps.
4. **Low-Allocation & AOT-Safe:** Reflection-free, trim-friendly, and utilizes `ValueTask`, `Stopwatch.GetTimestamp()`, and `EquatableArray<T>` to minimize runtime memory overhead.
5. **Native Diagnostics:** Built-in `System.Diagnostics.ActivitySource` support for OpenTelemetry tracing without external dependencies.

---

## Installation

```bash
dotnet add package Wiaoj.Compensation
```

---

## Usage

### 1. Class-Based Step Handler (Recommended)

Define a shared context and implement `ICompensationStep<TContext>`:

```csharp
public sealed class DocumentContext
{
    public Guid DocumentId { get; set; }
    public string? TempFilePath { get; set; }
    public string? S3Key { get; set; }
}

public sealed class CreateTempFileStep : ICompensationStep<DocumentContext>
{
    public async ValueTask ExecuteAsync(DocumentContext context, CancellationToken cancellationToken)
    {
        context.TempFilePath = Path.Combine(Path.GetTempPath(), $"{context.DocumentId}.tmp");
        await File.WriteAllTextAsync(context.TempFilePath, "data", cancellationToken);
    }

    public ValueTask CompensateAsync(DocumentContext context, CancellationToken cancellationToken)
    {
        if (context.TempFilePath is not null && File.Exists(context.TempFilePath))
        {
            File.Delete(context.TempFilePath);
        }
        return ValueTask.CompletedTask;
    }
}
```

### 2. Building and Running the Pipeline

```csharp
var pipeline = new CompensationPipeline<DocumentContext>()
    .AddStep(new CreateTempFileStep())
    .AddStep(
        name: "UploadToS3",
        execute: async (ctx, ct) =>
        {
            ctx.S3Key = await s3Client.UploadAsync(ctx.TempFilePath, ct);
        },
        compensate: async (ctx, rollbackCt) =>
        {
            await s3Client.DeleteAsync(ctx.S3Key, rollbackCt);
        }
    );

var context = new DocumentContext { DocumentId = Guid.NewGuid() };

CompensationReport<DocumentContext> report = await pipeline.RunAsync(
    context: context,
    cancellationToken: cancellationToken
);

if (!report.IsSuccess)
{
    if (report.IsClean)
    {
        // Failed, but all side-effects were completely rolled back.
        logger.LogWarning("Operation failed at {Step}: {Error}. State cleanly restored.", 
            report.FailedStepName, report.ErrorMessage);
    }
    else if (report.RequiresManualIntervention)
    {
        // Critical: One or more compensation steps failed during rollback.
        logger.LogCritical("Zombie data detected! Failed compensations: {Errors}", 
            report.CompensationErrors);
    }
}
```

---

## Pipeline Status Model

`CompensationReport<TContext>` exposes a deterministic `PipelineStatus` enum:

| Status | Description | `IsClean` | `RequiresManualIntervention` |
| :--- | :--- | :---: | :---: |
| `Success` | All steps executed successfully. | `true` | `false` |
| `Faulted_NoCompensationRequired` | Failed on the first step; no compensation was needed. | `true` | `false` |
| `Faulted_FullyCompensated` | Failed, and all previously completed steps were reverted. | `true` | `false` |
| `Faulted_PartiallyCompensated` | Failed, and one or more rollback steps threw an exception. | `false` | `true` |
| `Faulted_CompensationTimedOut` | Failed, and rollback exceeded the allocated timeout window. | `false` | `true` |
| `Cancelled_NoCompensationRequired` | Cancelled before any compensatable steps completed. | `true` | `false` |
| `Cancelled_FullyCompensated` | Cancelled mid-execution, all completed steps were reverted. | `true` | `false` |
| `Cancelled_PartiallyCompensated` | Cancelled, but rollback encountered errors. | `false` | `true` |
| `Cancelled_CompensationTimedOut` | Cancelled, and rollback timed out. | `false` | `true` |

---

## OpenTelemetry Tracing

`Wiaoj.Compensation` includes a built-in `ActivitySource` under the name `"Wiaoj.Compensation"`. When OpenTelemetry tracing is registered, pipeline and rollback phases automatically emit standard activities and tags (`compensation.status`, `compensation.is_clean`, `compensation.steps_count`, etc.).

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddSource("Wiaoj.Compensation")
        .AddOtlpExporter());
```

---

## Benchmarks

Benchmarked on .NET 10.0 (RyuJIT AVX-512) via BenchmarkDotNet:

```
BenchmarkDotNet v0.15.2, Windows 11
.NET SDK 10.0.400-preview
[Host] : .NET 10.0.9, X64 RyuJIT AVX-512
```

| Method | Mean | Error | StdDev | Allocated |
| :--- | ---: | ---: | ---: | ---: |
| Base Overhead (0 Steps) | 122.87 ns | 0.50 ns | 0.47 ns | 80 B |
| Happy Path (3 Class Steps) | 142.86 ns | 0.53 ns | 0.49 ns | 104 B |
| Happy Path (3 Lambda Steps) | 142.13 ns | 0.48 ns | 0.42 ns | 104 B |
| Happy Path (10 Class Steps) | 175.15 ns | 0.52 ns | 0.46 ns | 160 B |
| Happy Path (20 Class Steps) | 249.17 ns | 1.28 ns | 1.20 ns | 240 B |
| Faulted & Rollback (3 Steps) | 1,567.53 ns | 4.71 ns | 4.41 ns | 600 B |
| Faulted & Rollback (10 Steps) | 1,656.88 ns | 3.74 ns | 3.12 ns | 656 B |

---

## Contract Requirements

* **Idempotency:** Compensation delegates (`CompensateAsync`) must be designed to be idempotent. In the event of retries or concurrent cleanups, executing a compensation step multiple times should not cause unintended side effects.
* **CancellationToken Observance:** Steps must forward the provided `CancellationToken` to any downstream asynchronous calls (such as `HttpClient`, `DbContext`, or storage SDKs). Failure to forward tokens will prevent the pipeline from enforcing timeouts during the rollback phase.
