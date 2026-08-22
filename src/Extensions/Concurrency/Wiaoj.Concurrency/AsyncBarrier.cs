using System.Diagnostics;

namespace Wiaoj.Concurrency;
/// <summary>
/// Enables multiple tasks to cooperatively work on an algorithm in parallel through multiple phases.
/// </summary>
[DebuggerDisplay("Participants = {_participantCount}, Remaining = {_remainingParticipants}")]
public class AsyncBarrier {
    private readonly int _participantCount;
    private int _remainingParticipants;
    private TaskCompletionSource<bool>? _tcs;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncBarrier"/> class.
    /// </summary>
    public AsyncBarrier(int participantCount) {
        Preca.ThrowIfOutOfRange(participantCount, 1, int.MaxValue);
        this._participantCount = participantCount;
        this._remainingParticipants = participantCount;
        this._tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Signals that a participant has reached the barrier and waits for all other participants to reach the barrier.
    /// </summary>
    public Task SignalAndWaitAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        // Atomic.Read, 'volatile' olmadan da güvenli bir okuma sağlar.
        TaskCompletionSource<bool>? currentPhaseTcs = Atomic.Read(ref this._tcs);
        Preca.ThrowIfNull(currentPhaseTcs); // Null kontrolü ekleyerek güvenliği artıralım.

        // Atomic.Decrement, 'volatile' olmadan da güvenli bir azaltma sağlar.
        if (Atomic.Decrement(ref this._remainingParticipants) == 0) {
            // Last arrival: reset for the next phase.
            this._remainingParticipants = this._participantCount;
            TaskCompletionSource<bool> nextPhaseTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // Atomic.Exchange, 'volatile' olmadan da güvenli bir takas sağlar.
            Atomic.Exchange(ref this._tcs, nextPhaseTcs);

            // Signal completion for the current phase.
            currentPhaseTcs.TrySetResult(true);
        }

        // All participants wait on the task of the phase they arrived in.
        return currentPhaseTcs.Task.WaitAsync(cancellationToken);
    }
}