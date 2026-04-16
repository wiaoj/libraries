using Wiaoj.BloomFilter;
using Wiaoj.Samples.BloomFilter;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBloomFilter(bf => {
    // SENARYO 1: Tamamen kod üzerinden "Tipli (Typed)" filtre eklemek.
    // Hem IBloomFilter<UserRegistrationTag> olarak hem de "UserFilter" ismiyle DI'a eklenir.
    bf.AddFilter<UserRegistrationTag>(name: "UserFilter", expectedItems: 1_000_000, errorRate: 0.01);

    // SENARYO 2: Tamamen kod üzerinden "İsimli (Keyed)" filtre eklemek (Tip yok).
    // Sadece [FromKeyedServices("RateLimitFilter")] ile çağrılabilir.
    bf.AddFilter(name: "RateLimitFilter", expectedItems: 100_000, errorRate: 0.05);

    // SENARYO 3: appsettings.json'daki "BlacklistedIPs" ayarlarını bir Tipe bağlamak (Map).
    // Geliştirici bunu IBloomFilter<IpBlacklistTag> olarak enjekte edebilir.
    bf.MapFilter<IpBlacklistTag>("BlacklistedIPs");
});

builder.Services.AddHostedService<BloomTestWorker>();
var host = builder.Build();
host.Run();


public class UserRegistrationTag;
public class IpBlacklistTag;

public class SecurityService {
    private readonly IBloomFilter<UserRegistrationTag> _userFilter;
    private readonly IBloomFilter<IpBlacklistTag> _ipFilter;
    private readonly IBloomFilter _rateLimitFilter;

    public SecurityService(
        IBloomFilter<UserRegistrationTag> userFilter,          // Senaryo 1 (Tipli, koddan)
        IBloomFilter<IpBlacklistTag> ipFilter,                 // Senaryo 3 (Tipli, JSON'dan)
        [FromKeyedServices("RateLimitFilter")] IBloomFilter rateLimitFilter) // Senaryo 2 (Keyed, Tipli değil)
    {
        _userFilter = userFilter;
        _ipFilter = ipFilter;
        _rateLimitFilter = rateLimitFilter;
    }

    public void ProcessRequest(string email, string ipAddress, string clientId) {
        // 1. Tipli Kullanım (Kod ile tanımlanmış)
        if(_userFilter.Contains(email)) {
            throw new Exception("Bu email zaten kayıtlı!");
        }
        _userFilter.Add(email);

        // 2. Tipli Kullanım (appsettings'den maplenmiş)
        if(_ipFilter.Contains(ipAddress)) {
            throw new Exception("Bu IP adresi kara listede!");
        }

        // 3. İsimli (Keyed) Kullanım
        if(_rateLimitFilter.Contains(clientId)) {
            throw new Exception("Çok fazla istek attınız, yavaşlayın.");
        }
        _rateLimitFilter.Add(clientId);
    }
}