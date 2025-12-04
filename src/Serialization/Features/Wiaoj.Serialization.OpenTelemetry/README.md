# OpenTelemetry Instrumentation for Wiaoj Serialization

[![NuGet](https://img.shields.io/nuget/v/Wiaoj.Serialization.Observability.OpenTelemetry.svg)](https://www.nuget.org/packages/Wiaoj.Serialization.Observability.OpenTelemetry)

This package provides OpenTelemetry instrumentation for the [Wiaoj Serialization](https://github.com/wiaoj/serialization) library, enabling automatic distributed tracing and metrics collection for serialization operations.

## Features

- **Distributed Tracing**: Automatic span creation for all serialization/deserialization operations
- **Comprehensive Metrics**: Operation duration, count, and data size metrics
- **Multiple Serializers**: Supports System.Text.Json, MessagePack, and other serializers
- **Performance Optimized**: Minimal overhead with aggressive inlining
- **Semantic Conventions**: Follows OpenTelemetry semantic conventions

## Installation

```bash
dotnet add package Wiaoj.Serialization.Observability.OpenTelemetry
```

## Quick Start

### Enable Tracing

```csharp
using OpenTelemetry.Trace;

services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddWiaojSerializationInstrumentation()
        .AddOtlpExporter());
```

### Enable Metrics

```csharp
using OpenTelemetry.Metrics;

services.AddOpenTelemetry()
    .WithMetrics(builder => builder
        .AddWiaojSerializationInstrumentation()
        .AddOtlpExporter());
```

### Advanced Configuration

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddWiaojSerializationInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithSerializationDetails = true;
            options.MaxDataLength = 2048;
        }))
    .WithMetrics(builder => builder
        .AddWiaojSerializationInstrumentation(options =>
        {
            options.RecordDataSize = true;
            options.RecordOperationCount = true;
        }));
```

## Collected Telemetry

### Traces (Activities)

Each serialization operation creates a span with the following information:

- **Activity Name**: `{serializer}.{operation}` (e.g., `systemtextjson.serialize`)
- **Tags**:
  - `wiaoj.serialization.system`: Serializer type (systemtextjson, messagepack, etc.)
  - `wiaoj.serialization.key`: Serializer key identifier
  - `wiaoj.serialization.operation`: Operation type (serialize, deserialize, try_deserialize)
  - `wiaoj.serialization.destination`: Target format (string, byte_array, stream)
  - `wiaoj.serialization.type`: .NET type being serialized
  - `exception.type`: Exception type (on errors)
  - `exception.message`: Exception message (on errors)

### Metrics

| Metric Name | Type | Description | Tags |
|-------------|------|-------------|------|
| `wiaoj.serialization.operation.duration` | Histogram | Operation duration in milliseconds | system, key, operation, destination, type, status |
| `wiaoj.serialization.operation.count` | Counter | Total number of operations | system, key, operation, destination, type, status |
| `wiaoj.serialization.data.size` | Histogram | Size of serialized data in bytes | system, key, operation, destination, type, status |

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `RecordException` | `bool` | `true` | Record exception details in spans |
| `EnrichWithSerializationDetails` | `bool` | `true` | Add detailed serialization context to spans |
| `MaxDataLength` | `int` | `1024` | Maximum data length to record (0 = disabled) |
| `RecordDataSize` | `bool` | `true` | Record data size metrics |
| `RecordOperationCount` | `bool` | `true` | Record operation count metrics |

## Performance Considerations

- The instrumentation uses `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for minimal overhead
- Metrics are recorded only when OpenTelemetry listeners are active
- Activity creation follows OpenTelemetry's sampling decisions
- Static ActivitySource and Meter instances minimize allocation overhead

## Compatibility

- **.NET**: 9.0+
- **OpenTelemetry**: 1.12.0+
- **Wiaoj.Serialization**: 1.0.0+

## Contributing

Contributions are welcome! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.