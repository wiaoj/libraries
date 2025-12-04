using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Concurrency;
using Wiaoj.Consensus.Raft.Abstractions;
using Wiaoj.Consensus.Raft.Persistence.Faster;
using Wiaoj.Consensus.Raft.Roles;
using Wiaoj.Consensus.Raft.Transport.Grpc;
using Wiaoj.Extensions.DependencyInjection;

namespace Wiaoj.Consensus.Raft.Hosting;

/// <summary>
/// Provides extension methods for registering Raft services in the <see cref="IServiceCollection"/>.
/// </summary>
public static class RaftServiceCollectionExtensions {

    /// <summary>
    /// Configures and registers a full Raft node, including its persistence, transport,
    /// and lifecycle management, in the dependency injection container.
    /// </summary>
    /// <typeparam name="TStateMachine">The application-specific implementation of <see cref="IStateMachine"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="sectionName">The name of the configuration section for RaftNodeOptions. Defaults to "Raft".</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRaft<TStateMachine>(
        this IServiceCollection services,
        string sectionName = "Raft")
        where TStateMachine : class, IStateMachine {

        // 1. Yapılandırmayı Kaydet: appsettings.json'dan "Raft" bölümünü okur.
        services.AddOptions<RaftNodeOptions>()
            .BindConfiguration(sectionName)
            .ValidateOnStart(); // Gerekirse validasyon ekle
        services.TryAddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RaftNodeOptions>>().Value);

        // 2. Somut Servisleri Kaydet: Her servis basit bir singleton olarak kaydedilir.
        //    Constructor'ları senkron olduğu için bu adımlar tamamen güvenlidir.
        //    Asenkron başlatma burada DEĞİL, RaftHostedService'de yapılır.

        //    Kalıcılık Katmanı: Henüz başlatılmamış bir FasterState örneği oluşturur.
        services.TryAddSingleton<FasterState>(); // DI, path ve logger'ı constructor'a otomatik enjekte edecek.
        services.TryAddSingleton<IRaftLog>(sp => sp.GetRequiredService<FasterState>());
        services.TryAddSingleton<IStateManager>(sp => sp.GetRequiredService<FasterState>());
         
        //    Ağ Katmanı
        services.TryAddSingleton<IRaftCluster, GrpcCluster>();

        // 4. Roller (Her biri kendi bağımlılıklarını alacak)
        services.TryAddTransient<IRaftRole, FollowerRole>();
        services.TryAddTransient<IRaftRole, CandidateRole>();
        services.TryAddTransient<IRaftRole, LeaderRole>();

        //    Uygulamanın Durum Makinesi
        services.TryAddSingleton<IStateMachine, TStateMachine>();

        //    Çekirdek Raft Motoru
        services.TryAddSingleton<IRaftEngine, RaftEngine>();

        //    gRPC Servisi (gelen çağrıları karşılamak için)
        services.AddSingleton<RaftConsensusService>();

        // 3. Arayüzleri Somut Tiplere Yönlendir: IRaftLog veya IStateManager istendiğinde,
        //    daha önce kaydedilen aynı FasterState örneğini ver.
        services.TryAddSingleton<IRaftLog>(sp => sp.GetRequiredService<FasterState>());
        services.TryAddSingleton<IStateManager>(sp => sp.GetRequiredService<FasterState>());

        // 4. Yaşam Döngüsü ve Başlatma Orkestratörü: Tüm asenkron başlatma mantığını
        //    içeren TEK bir Hosted Service'i kaydet.
        services.AddHostedService<RaftHostedService>();

        return services;
    }
}