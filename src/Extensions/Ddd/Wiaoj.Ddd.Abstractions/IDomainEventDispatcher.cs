using Wiaoj.Ddd.Abstractions.DomainEvents;

namespace Wiaoj.Ddd.Abstractions;

public interface IDomainEventDispatcher {  
    ValueTask DispatchPreCommitAsync<TDomainEvent>( TDomainEvent @event, CancellationToken cancellationToken) where TDomainEvent : IDomainEvent;
    ValueTask DispatchPostCommitAsync<TDomainEvent>( TDomainEvent @event, CancellationToken cancellationToken) where TDomainEvent : IDomainEvent;
} 

/// <summary>
/// Veritabanı işlemi (transaction) commit edilmeden HEMEN ÖNCE çalıştırılacak event handler'ları tanımlar.
/// Bu handler'lar ana işlemle aynı transaction'a dahildir. Bir hata oluşması durumunda tüm işlem geri alınır.
/// </summary>
public interface IPreDomainEventHandler<in TDomainEvent> where TDomainEvent : IDomainEvent {
    ValueTask Handle(TDomainEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Veritabanı işlemi (transaction) başarıyla commit edildikten HEMEN SONRA çalıştırılacak event handler'ları tanımlar.
/// Bu handler'lar ana transaction'ın dışındadır ve yan etkiler (örn: bildirim gönderme, mesaj kuyruğuna yazma) için kullanılır.
/// </summary>
public interface IPostDomainEventHandler<in TDomainEvent> where TDomainEvent : IDomainEvent {
    ValueTask Handle(TDomainEvent @event, CancellationToken cancellationToken = default);
}