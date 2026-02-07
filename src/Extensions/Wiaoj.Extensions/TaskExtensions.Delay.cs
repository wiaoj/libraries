using Wiaoj.Primitives;


namespace Wiaoj.Extensions;
/// <summary>
/// Provides extension methods for asynchronous delays using the <see cref="OperationTimeout"/> primitive.
/// </summary>
public static class TaskDelayExtensions {
    /// <summary>
    /// Creates a task that completes after a specified timeout, allowing for combined time-based
    /// and token-based cancellation.
    /// </summary>
    /// <param name="timeout">
    /// The timeout policy that defines the duration of the delay and/or a cancellation token to observe.
    /// </param>
    /// <returns>A task that represents the asynchronous delay operation.</returns>
    /// <remarks>
    /// This method serves as a powerful, unified alternative to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// It leverages the <see cref="OperationTimeout.CreateCancellationTokenSource()"/> method to handle the underlying cancellation logic.
    /// <example>
    /// <code>
    /// // Wait for 5 seconds
    /// await OperationTimeout.FromSeconds(5).DelayAsync();
    ///
    /// // Wait for 30 seconds or until a token is cancelled
    /// await OperationTimeout.FromMilliseconds(30.TotalSeconds(), myToken).DelayAsync();
    /// </code>
    /// </example>
    /// </remarks>
    public static Task DelayAsync(this OperationTimeout timeout) {
        // 1. OperationTimeout politikasından tek bir CancellationTokenSource oluştur.
        //    Bu CTS, hem süre dolduğunda hem de içindeki token iptal olduğunda iptal olur.
        CancellationTokenSource cts = timeout.CreateCancellationTokenSource();

        // 2. Task.Delay'e sonsuz bir süre ver, çünkü iptal mantığını tamamen
        //    oluşturduğumuz CTS yönetecek.
        //    cts.Token.WaitHandle.WaitOne(0) kontrolü, eğer token zaten iptal edilmişse
        //    gereksiz bir Task oluşturmayı engeller ve anında iptal edilmiş bir Task döndürür.
        if (cts.IsCancellationRequested) {
            cts.Dispose();
            return Task.FromCanceled(cts.Token);
        }

        // 3. Task.Delay'i oluştur ve tamamlandığında (ya da hata verdiğinde) CTS'i dispose et.
        return Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)
            .ContinueWith(t => {
                cts.Dispose();
                // Orijinal iptal nedenini korumak için, yeni bir Canceled task oluşturmak yerine
                // tamamlanmış task'in kendisini (veya exception'ını) yay.
                // Not: Task.Delay başarılı bir şekilde tamamlanmaz, sadece iptal olur.
                // Bu yüzden burada sadece hata durumunu ele alıyoruz.
                t.GetAwaiter().GetResult();
            }, TaskContinuationOptions.ExecuteSynchronously);
    }
}