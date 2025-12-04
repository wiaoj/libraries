using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wiaoj.Consensus.Raft.Abstractions;
using Wiaoj.Consensus.Raft.Persistence.Faster;

///// <summary>
///// Manages the lifecycle of the RaftEngine, starting it with the application
///// and ensuring it's gracefully disposed on shutdown.
///// </summary>
//internal sealed class RaftHostedService : IHostedService {
//    private readonly ILogger<RaftHostedService> _logger;
//    private readonly IRaftEngine _raftEngine;

//    public RaftHostedService(ILogger<RaftHostedService> logger, IRaftEngine raftEngine) {
//        _logger = logger;
//        _raftEngine = raftEngine;
//    }

//    public Task StartAsync(CancellationToken cancellationToken) {
//        _logger.LogInformation("Raft Hosted Service is starting.");
//        // RaftEngine'i başlat, ancak bu metodun tamamlanmasını bekleme (ateşle ve unut).
//        // RaftEngine kendi arka plan görevlerini yönetir.
//        return _raftEngine.StartAsync(cancellationToken);
//    }

//    public async Task StopAsync(CancellationToken cancellationToken) {
//        _logger.LogInformation("Raft Hosted Service is stopping.");
//        // RaftEngine'in IAsyncDisposable olduğunu varsayarak onu düzgünce dispose et.
//        if (_raftEngine is IAsyncDisposable disposable) {
//            await disposable.DisposeAsync();
//        }
//    }
//}


namespace Wiaoj.Consensus.Raft.Hosting;

internal sealed class RaftHostedService : IHostedService {
    private readonly ILogger<RaftHostedService> _logger;
    private readonly FasterState _fasterState;
    private readonly IRaftEngine _raftEngine;
    private readonly IHostApplicationLifetime _appLifetime;

    // IHostApplicationLifetime'ı DI'dan alıyoruz. Bu, "uygulama ne zaman tam olarak başladı?"
    // sorusunun cevabını bilmemizi sağlar.
    public RaftHostedService(
        ILogger<RaftHostedService> logger,
        FasterState fasterState,
        IRaftEngine raftEngine,
        IHostApplicationLifetime appLifetime) {
        this._logger = logger;
        this._fasterState = fasterState;
        this._raftEngine = raftEngine;
        this._appLifetime = appLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        // StartAsync içinde HİÇBİR 'await' veya engelleme yapmıyoruz.
        // Sadece, "uygulama tamamen başladığında şu işi yap" diye kaydediyoruz.
        this._appLifetime.ApplicationStarted.Register(OnApplicationStarted);
        return Task.CompletedTask;
    }
    private async void OnApplicationStarted() {
        this._logger.LogInformation("Uygulama tamamen başlatıldı. Raft başlatma süreci tetikleniyor...");

        _ = Task.Run(async () => {
            try {
                // ADIM 1: Kalıcılık Katmanını Asenkron Olarak Başlat.
                // Bu, diskten Term, VotedFor ve Log'un okunmasını sağlar.
                this._logger.LogInformation("Kalıcılık katmanı (FasterState) başlatılıyor...");
                await this._fasterState.InitializeAsync(this._appLifetime.ApplicationStopping);
                this._logger.LogInformation("Kalıcılık katmanı başarıyla başlatıldı.");

                // ADIM 2: Kalıcılık hazır olduğuna göre, artık Raft motorunu güvenle başlatabiliriz.
                // Motor başladığında, doğru Term ve Log durumuyla başlayacak.
                this._logger.LogInformation("Raft Motoru başlatılıyor...");
                await this._raftEngine.StartAsync(this._appLifetime.ApplicationStopping);
                this._logger.LogInformation("Raft Motoru başarıyla başlatıldı.");
            }
            catch (Exception ex) {
                this._logger.LogCritical(ex, "Raft başlatma sürecinde ölümcül bir hata oluştu. Uygulama durdurulabilir.");
                this._appLifetime.StopApplication();
            }
        });
    }

    // StopAsync metoduna da _fasterState.DisposeAsync() eklemeyi unutma
    public async Task StopAsync(CancellationToken cancellationToken) {
        this._logger.LogInformation("Raft servisi durduruluyor...");

        List<Task> disposeTasks = [];

        if (this._raftEngine is IAsyncDisposable disposableEngine) {
            disposeTasks.Add(disposableEngine.DisposeAsync().AsTask());
        }

        if (this._fasterState is IAsyncDisposable disposableFaster) {
            disposeTasks.Add(disposableFaster.DisposeAsync().AsTask());
        }

        // Her iki servisin de kapatma işleminin tamamlanmasını paralel olarak bekle.
        await Task.WhenAll(disposeTasks);
    }

    //private void OnApplicationStarted() {
    //    // Bu metot, ana başlatma thread'inden FARKLI bir thread üzerinde çalışır.
    //    // Bu, deadlock riskini tamamen ortadan kaldırır.
    //    this._logger.LogInformation("Uygulama tamamen başlatıldı. Raft başlatma süreci tetikleniyor...");

    //    // Görevi ateşle ve unut (fire-and-forget), çünkü Register metodu async olamaz.
    //    // Hataları yakalamak ve loglamak için kendi try-catch bloğumuzu ekliyoruz.
    //    _ = Task.Run(async () => {
    //        try {
    //            // ADIM 1: Kalıcılık Katmanını Asenkron Olarak Başlat.
    //            this._logger.LogInformation("Kalıcılık katmanı (FasterState) başlatılıyor...");
    //            await _fasterState.InitializeAsync(_appLifetime.ApplicationStopping);
    //            this._logger.LogInformation("Kalıcılık katmanı başarıyla başlatıldı.");

    //            // ADIM 2: Kalıcılık hazır olduğuna göre, artık Raft motorunu güvenle başlatabiliriz.
    //            this._logger.LogInformation("Raft Motoru başlatılıyor...");
    //            await this._raftEngine.StartAsync(this._appLifetime.ApplicationStopping); // Uygulama kapanış token'ını ver
    //            this._logger.LogInformation("Raft Motoru başarıyla başlatıldı.");
    //        }
    //        catch (Exception ex) {
    //            this._logger.LogCritical(ex, "Raft başlatma sürecinde ölümcül bir hata oluştu. Uygulama durdurulabilir.");
    //            // İsteğe bağlı: Başlatma başarısız olursa uygulamayı kapat.
    //            _appLifetime.StopApplication();
    //        }
    //    });
    //}

    //public async Task StopAsync(CancellationToken cancellationToken) {
    //    this._logger.LogInformation("Raft servisi durduruluyor...");
    //    if (this._raftEngine is IAsyncDisposable disposable) {
    //        await disposable.DisposeAsync();
    //    }

    //    if (_fasterState is IAsyncDisposable faster) {
    //        await faster.DisposeAsync();
    //    }
    //}
}