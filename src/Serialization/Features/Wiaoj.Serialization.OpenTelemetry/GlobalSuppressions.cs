using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", 
    Justification = "OpenTelemetry extension pattern requires namespace to match target type for discoverability", 
    Scope = "namespace", Target = "~N:OpenTelemetry.Trace")]

[assembly: SuppressMessage("Style", "IDE0130:Namespace does not match folder structure", 
    Justification = "OpenTelemetry extension pattern requires namespace to match target type for discoverability", 
    Scope = "namespace", Target = "~N:OpenTelemetry.Metrics")]