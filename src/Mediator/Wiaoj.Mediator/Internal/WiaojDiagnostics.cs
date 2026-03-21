using System.Diagnostics;

namespace Wiaoj.Mediator.Internal;
internal static class WiaojDiagnostics {
    // ActivitySource adı benzersiz olmalı. Genelde paket/namespace adı verilir.
    public const string ActivitySourceName = "Wiaoj.TracingMediator";

    // Statik instance. DI'ya gerek yok, uygulama boyunca tek ve sabit.
    public static readonly ActivitySource Source = new(ActivitySourceName);
}