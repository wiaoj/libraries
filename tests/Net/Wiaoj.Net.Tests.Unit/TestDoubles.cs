using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>Resolves names from a fixed table, counting lookups; an unknown name fails as the system resolver does.</summary>
internal sealed class FakeDnsResolver(IReadOnlyDictionary<string, IPAddress[]> records) : DnsResolver {
    private int _lookups;

    public int Lookups => Volatile.Read(ref this._lookups);

    public override ValueTask<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) {
        Interlocked.Increment(ref this._lookups);
        return records.TryGetValue(host, out IPAddress[]? addresses)
            ? ValueTask.FromResult(addresses)
            : ValueTask.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound));
    }
}

/// <summary>An HTTP/1.1 server on 127.0.0.1 answering 200, counting accepted connections.</summary>
internal sealed class LoopbackHttpServer : IDisposable {
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private int _connections;

    public LoopbackHttpServer() {
        this._listener.Start();
        _ = Task.Run(this.AcceptAsync);
    }

    public int Port => ((IPEndPoint)this._listener.LocalEndpoint).Port;

    public int Connections => Volatile.Read(ref this._connections);

    private async Task AcceptAsync() {
        try {
            while(!this._stop.IsCancellationRequested) {
                using TcpClient client = await this._listener.AcceptTcpClientAsync(this._stop.Token);
                Interlocked.Increment(ref this._connections);
                NetworkStream stream = client.GetStream();
                await stream.ReadAsync(new byte[8192], this._stop.Token);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"), this._stop.Token);
            }
        }
        catch(OperationCanceledException) { }
        catch(ObjectDisposedException) { }
    }

    public void Dispose() {
        this._stop.Cancel();
        this._listener.Stop();
    }
}
