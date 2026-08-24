namespace Wiaoj.Webhooks.Signing.Asymmetric.Tests.Unit;

/// <summary>
/// Automatically skips tests pending official .NET 11 BCL cryptographic support.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExperimentalFactAttribute : FactAttribute {
    public ExperimentalFactAttribute(string reason = "Experimental: Pending official .NET 11 BCL cryptographic support.") { 
        this.Skip = reason;
    }
}

/// <summary>
/// Automatically skips theories pending official .NET 11 BCL cryptographic support.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExperimentalTheoryAttribute : TheoryAttribute {
    public ExperimentalTheoryAttribute(string reason = "Experimental: Pending official .NET 11 BCL cryptographic support.") {
        this.Skip = reason;
    }
}