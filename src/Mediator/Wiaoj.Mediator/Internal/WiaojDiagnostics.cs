using System.Diagnostics;

namespace Wiaoj.Mediator.Internal;
internal static class WiaojDiagnostics {
    public const string ActivitySourceName = "Wiaoj.Mediator";
    public static readonly ActivitySource Source = new(ActivitySourceName);
}