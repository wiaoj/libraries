using System.Net;
using System.Net.Sockets;
using Wiaoj.Preconditions;

namespace Wiaoj.Net;

/// <summary>
/// Connects to the first of several addresses that answers, racing the attempts as Happy Eyeballs v2 (RFC 8305) describes.
/// </summary>
/// <remarks>
/// <para>
/// Trying addresses one after another spends the whole connect timeout on an address whose path silently drops packets,
/// and the next address — often a healthy one of the other family — is never tried. Here each attempt gets a head start of
/// <see cref="AttemptDelay"/> (RFC 8305 §5 recommends 250 ms); when it has not completed by then the next one starts
/// alongside it, and an attempt that fails starts the next one at once.
/// </para>
/// <para>
/// The first connection wins; the attempts still pending are cancelled (§5), and a socket one of them opens anyway is
/// disposed.
/// </para>
/// </remarks>
internal sealed class HappyEyeballsConnector {
    /// <summary>The connection attempt delay RFC 8305 §5 recommends.</summary>
    public static readonly TimeSpan DefaultAttemptDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>RFC 8305 §5: a subsequent attempt MUST NOT start within 10 ms of the previous one.</summary>
    private static readonly TimeSpan MinimumAttemptDelay = TimeSpan.FromMilliseconds(10);

    private readonly TimeProvider _timeProvider;
    private readonly Func<IPEndPoint, CancellationToken, ValueTask<Socket>> _connect;

    public static HappyEyeballsConnector Default { get; } = new(DefaultAttemptDelay, TimeProvider.System, ConnectSocketAsync);

    public HappyEyeballsConnector(TimeSpan attemptDelay, TimeProvider timeProvider, Func<IPEndPoint, CancellationToken, ValueTask<Socket>> connect) {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptDelay, MinimumAttemptDelay);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(connect);

        this.AttemptDelay = attemptDelay;
        this._timeProvider = timeProvider;
        this._connect = connect;
    }

    public TimeSpan AttemptDelay { get; }

    /// <summary>
    /// Orders addresses for connection attempts: RFC 8305 §4 interleaves the address families, starting with the family
    /// of the first address (a First Address Family Count of 1), and keeps the order within each family.
    /// </summary>
    /// <remarks>
    /// The resolver's order is taken as the RFC 6724 destination address selection §4 asks to sort by first; the system
    /// resolver returns addresses in that order.
    /// </remarks>
    public static IPAddress[] Interleave(IReadOnlyList<IPAddress> addresses) {
        if(addresses.Count == 0) {
            return [];
        }

        AddressFamily first = addresses[0].AddressFamily;
        Queue<IPAddress> preferred = new(addresses.Where(address => address.AddressFamily == first));
        Queue<IPAddress> other = new(addresses.Where(address => address.AddressFamily != first));

        IPAddress[] ordered = new IPAddress[addresses.Count];
        for(int i = 0; i < ordered.Length; i++) {
            bool takePreferred = other.Count == 0 || (preferred.Count > 0 && i % 2 == 0);
            ordered[i] = takePreferred ? preferred.Dequeue() : other.Dequeue();
        }

        return ordered;
    }

    /// <summary>
    /// Connects to one of <paramref name="addresses"/>, attempted in the order given.
    /// </summary>
    /// <param name="addresses">The addresses, already ordered and allowed; at least one.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="cancellationToken">Cancels every attempt, pending or not yet started.</param>
    /// <returns>The connected socket of the first attempt to succeed.</returns>
    /// <exception cref="SocketException">Every attempt failed; the last failure is reported.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async ValueTask<Socket> ConnectAsync(IReadOnlyList<IPAddress> addresses, int port, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfZero(addresses.Count);

        using CancellationTokenSource race = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        List<Task<Socket>> pending = new(addresses.Count);
        Exception? lastError = null;
        int next = 0;

        try {
            while(true) {
                cancellationToken.ThrowIfCancellationRequested();

                // Each pass starts one attempt: the first, the next after a delay elapsed, or the next after a failure.
                if(next < addresses.Count) {
                    pending.Add(this.AttemptAsync(new IPEndPoint(addresses[next++], port), race.Token));
                }

                if(pending.Count == 0) {
                    break;
                }

                Task completed;
                if(next < addresses.Count) {
                    using CancellationTokenSource delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(race.Token);
                    Task delay = Task.Delay(this.AttemptDelay, this._timeProvider, delayCancellation.Token);
                    completed = await Task.WhenAny([.. pending, delay]).ConfigureAwait(false);
                    delayCancellation.Cancel();

                    if(completed == delay) {
                        continue;
                    }
                }
                else {
                    completed = await Task.WhenAny(pending).ConfigureAwait(false);
                }

                Task<Socket> attempt = (Task<Socket>)completed;
                pending.Remove(attempt);

                if(attempt.IsCompletedSuccessfully) {
                    return attempt.Result;
                }

                lastError = attempt.Exception?.InnerException ?? lastError;
            }
        }
        finally {
            race.Cancel();
            foreach(Task<Socket> loser in pending) {
                _ = loser.ContinueWith(
                    static task => {
                        if(task.IsCompletedSuccessfully) {
                            task.Result.Dispose();
                        }
                        else {
                            _ = task.Exception;
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        throw lastError ?? new SocketException((int)SocketError.HostUnreachable);
    }

    private async Task<Socket> AttemptAsync(IPEndPoint endpoint, CancellationToken cancellationToken) {
        return await this._connect(endpoint, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<Socket> ConnectSocketAsync(IPEndPoint endpoint, CancellationToken cancellationToken) {
        Socket socket = new(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try {
            await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch {
            socket.Dispose();
            throw;
        }
    }
}
