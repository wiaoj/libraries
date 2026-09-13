namespace Wiaoj.WellKnown;

public sealed class OAuthProtectedResourceOptions {
    /// <summary>
    /// Kaynağın public adresi (Örn: https://api.verba.app)
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Token dağıtan Auth Server adresleri (Örn: https://auth.vaultex.app)
    /// </summary>
    public List<string> AuthorizationServers { get; set; } = [];

    /// <summary>
    /// Modüllerden toplanan yetki (scope) havuzu.
    /// </summary>
    public HashSet<string> Scopes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// API dokümantasyon URL'i.
    /// </summary>
    public string? ResourceDocumentation { get; set; }

    /// <summary>
    /// Yanıtın HTTP Cache süresi (Varsayılan: 24 saat).
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);
}