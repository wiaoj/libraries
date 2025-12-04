using Wiaoj;
using Wiaoj.Serialization.Abstractions;

namespace OpenTelemetry.Trace {
    /// <summary>
    /// Extension methods for configuring Wiaoj Serialization tracing instrumentation.
    /// </summary>
    public static class WiaojSerializationTracerProviderBuilderExtensions {
        /// <summary>
        /// Enables Wiaoj Serialization instrumentation for distributed tracing.
        /// </summary>
        /// <param name="builder">The <see cref="TracerProviderBuilder"/> instance.</param>
        /// <returns>The <see cref="TracerProviderBuilder"/> instance for method chaining.</returns>
        /// <remarks>
        /// This method registers the Wiaoj Serialization ActivitySource and enables automatic
        /// tracing of all serialization and deserialization operations performed through
        /// the Wiaoj Serialization library.
        /// </remarks>
        public static TracerProviderBuilder AddWiaojSerializationInstrumentation(this TracerProviderBuilder builder) {
            Preca.ThrowIfNull(builder);
            return builder
                .AddSource(WiaojActivitySource.SourceName);
        }
    }
}

namespace OpenTelemetry.Metrics {
    /// <summary>
    /// Extension methods for configuring Wiaoj Serialization metrics instrumentation.
    /// </summary>
    public static class WiaojSerializationMeterProviderBuilderExtensions {
        /// <summary>
        /// Enables Wiaoj Serialization instrumentation for metrics collection.
        /// </summary>
        /// <param name="builder">The <see cref="MeterProviderBuilder"/> instance.</param>
        /// <returns>The <see cref="MeterProviderBuilder"/> instance for method chaining.</returns>
        /// <remarks>
        /// This method registers the Wiaoj Serialization Meter and enables automatic
        /// collection of metrics such as operation duration, operation count, and data size
        /// for all serialization and deserialization operations.
        /// </remarks>
        public static MeterProviderBuilder AddWiaojSerializationInstrumentation(this MeterProviderBuilder builder) {
            Preca.ThrowIfNull(builder);
            return builder
                .AddMeter(WiaojActivitySource.SourceName);
        }
    }
}