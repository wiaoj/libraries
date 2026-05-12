using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text;
using Wiaoj.Primitives;
using Wiaoj.Security;
using Wiaoj.Security.DependencyInjection;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// 1. Master Key'i Environment'dan al (Simülasyon için)
Environment.SetEnvironmentVariable("MASTER_KEY", "r4_V1rtjRmgZDnYZZSYLXx7yFKP_4sA7E_o9y0O87Cc");

// 2. Kütüphane servislerini ayağa kaldır
builder.Services.AddWiaojSecurity(opts => {
    opts.KeySizeInBits = 256;
    opts.RotationInterval = TimeSpan.FromMinutes(1);
})
.AddEnvironmentMasterKey("MASTER_KEY")
.AddInMemoryKeyStore()
.AddManagedProtector<WebhookSigningSecretContext>();

// 3. Test Worker'ı ekle
builder.Services.AddHostedService<TestSecurityWorker>();

using IHost host = builder.Build();
await host.RunAsync();

public class TestSecurityWorker(
    ISecretProtector<WebhookSigningSecretContext> protector,
    IServiceProvider serviceProvider,
    ILogger<TestSecurityWorker> logger) : BackgroundService {

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        logger.LogInformation("--- STRES TESTİ BAŞLIYOR ---");

        // 1. Eşzamanlı (Concurrent) Şifreleme Testi
        // 10 thread aynı anda şifreleme yapıyor.
        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () => {
            var enc = protector.Protect($"Veri_{i}");
            using var dec = protector.Unprotect(enc);
            dec.Expose(b => {
                if(Encoding.UTF8.GetString(b) != $"Veri_{i}")
                    throw new Exception("Data Corruption!");
            });
        }));
        await Task.WhenAll(tasks);
        logger.LogInformation("Concurrent Şifreleme/Çözme OK.");

        // 2. Hızlı ve Agresif Rotasyon Testi
        using(var scope = serviceProvider.CreateScope()) {
            var rotator = scope.ServiceProvider.GetRequiredService<KeyRotationService<WebhookSigningSecretContext>>();

            for(int i = 0; i < 5; i++) {
                logger.LogInformation("Agresif rotasyon {I}/5...", i + 1);
                await rotator.ForceRotateAsync(stoppingToken);
            }
        }

        // 3. Geçmişe dönük doğrulama (V1 ile şifrelenmiş veriyi V6 zamanında çözme)
        var earlyEnc = protector.Protect("Ancient_Data");

        // 4-5 rotasyon daha yapalım
        using(var scope = serviceProvider.CreateScope()) {
            var rotator = scope.ServiceProvider.GetRequiredService<KeyRotationService<WebhookSigningSecretContext>>();
            await rotator.ForceRotateAsync(stoppingToken);
        }

        // Şimdi eskiyi çözmeyi dene
        using var oldDec = protector.Unprotect(earlyEnc);
        oldDec.Expose(b => {
            logger.LogInformation("Ancient_Data (V1'den V6'ya) çözüldü: {R}", Encoding.UTF8.GetString(b));
        });

        logger.LogInformation("--- TÜM STRES TESTLERİ BAŞARIYLA GEÇİLDİ ---");
    }
}

public sealed class InMemoryKeyStore(TimeProvider timeProvider) : IEncryptionKeyStore {
    private readonly List<EncryptionKeyRecord> _keys = [];
    public Task<IReadOnlyList<EncryptionKeyRecord>> LoadKeysAsync(string c, CancellationToken ct) {
        return Task.FromResult((IReadOnlyList<EncryptionKeyRecord>)this._keys);
    }

    public Task<EncryptionKeyRecord> SaveKeyAsync(EncryptionKeyRecord r, CancellationToken ct) {
        this._keys.Add(r);
        return Task.FromResult(r);
    }

    public Task RetireKeyAsync(string c, int v, CancellationToken ct) {
        var record = _keys.FirstOrDefault(x => x.Version == v);
        record?.RetiredAt = timeProvider.GetUtcNow();
        return Task.CompletedTask;
    }

    public Task<EncryptionKeyRecord?> GetKeyAsync(string contextName, int version, CancellationToken ct = default) {
        return Task.FromResult(_keys.FirstOrDefault(x => x.Version == version));
    }
}

public readonly struct WebhookSigningSecretContext : ISecretContext;

public static class SecurityBuilderEfCoreExtensions {
    /// <summary>
    /// Registers <see cref="EfEncryptionKeyStore{TDbContext}"/> as the
    /// <see cref="IEncryptionKeyStore"/> implementation.
    /// </summary>
    /// <typeparam name="TDbContext">
    /// Your application's <c>DbContext</c>. Must implement <see cref="IEncryptionKeyDbContext"/>.
    /// </typeparam>
    /// <remarks>
    /// Register as Scoped — <see cref="EfEncryptionKeyStore{TDbContext}"/> depends on
    /// a scoped <typeparamref name="TDbContext"/>.
    /// <para>
    /// Your <c>DbContext</c> must implement <see cref="IEncryptionKeyDbContext"/> and apply
    /// <see cref="EncryptionKeyRecordConfiguration"/> in <c>OnModelCreating</c>:
    /// <code>
    /// public class AppDbContext : DbContext, IEncryptionKeyDbContext {
    ///     public DbSet&lt;EncryptionKeyRecord&gt; EncryptionKeys { get; set; } = null!;
    ///
    ///     protected override void OnModelCreating(ModelBuilder modelBuilder) {
    ///         modelBuilder.ApplyConfiguration(new EncryptionKeyRecordConfiguration());
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static ISecurityBuilder AddInMemoryKeyStore(this ISecurityBuilder builder) {
        builder.Services.TryAddSingleton<IEncryptionKeyStore, InMemoryKeyStore>();
        return builder;
    }
}
