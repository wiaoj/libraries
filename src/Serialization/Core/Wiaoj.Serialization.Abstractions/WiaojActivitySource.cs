using System.Diagnostics;
using System.Reflection;

namespace Wiaoj.Serialization;

internal static class WiaojActivitySource {
    public const string SourceName = "Wiaoj.Serialization";
    public static readonly string Version = GetAssemblyVersion();
    public static readonly ActivitySource Source = new(SourceName, Version);

    private static string GetAssemblyVersion() {
        Assembly assembly = Assembly.GetExecutingAssembly();
        Version? version = assembly.GetName().Version;
        return version?.ToString(3) ?? "1.0.0"; // Major.Minor.Build
    }
}