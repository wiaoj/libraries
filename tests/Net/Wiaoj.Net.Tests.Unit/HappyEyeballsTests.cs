using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>
/// Connection attempts across a host's addresses are raced as Happy Eyeballs v2 (RFC 8305) describes, so an address that
/// does not answer no longer uses up the connect timeout before the next one is tried (#140).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "HappyEyeballs")]
public sealed class HappyEyeballsTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeSpan Delay = HappyEyeballsConnector.DefaultAttemptDelay;

    private static IPAddress Ip(string address) => IPAddress.Parse(address);

    /// <summary>
    /// A scripted connect: each address gets a completion source the test settles, and every attempt is recorded with the
    /// token it was given.
    /// </summary>
    private sealed class ScriptedConnect {
        private readonly ConcurrentDictionary<IPAddress, TaskCompletionSource<Socket>> _outcomes = new();
        private readonly Channel<(IPEndPoint Endpoint, CancellationToken Token)> _started = Channel.CreateUnbounded<(IPEndPoint, CancellationToken)>();

        public List<(IPEndPoint Endpoint, CancellationToken Token)> Attempts { get; } = [];

        public TaskCompletionSource<Socket> For(IPAddress address) {
            return this._outcomes.GetOrAdd(address, _ => new TaskCompletionSource<Socket>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public async ValueTask<Socket> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken) {
            lock(this.Attempts) {
                this.Attempts.Add((endpoint, cancellationToken));
            }

            this._started.Writer.TryWrite((endpoint, cancellationToken));
            return await this.For(endpoint.Address).Task;
        }

        public int Started {
            get {
                lock(this.Attempts) {
                    return this.Attempts.Count;
                }
            }
        }

        public async Task<IPEndPoint> NextStartedAsync() {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            return (await this._started.Reader.ReadAsync(timeout.Token)).Endpoint;
        }
    }

    private static Socket UnconnectedSocket() => new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    public sealed class OrderingAddresses {
        [Theory]
        // RFC 8305 §4: interleave families, starting with the family of the first address, keeping the order within each.
        [InlineData("2001:db8::1,2001:db8::2,192.0.2.1,192.0.2.2", "2001:db8::1,192.0.2.1,2001:db8::2,192.0.2.2")]
        [InlineData("192.0.2.1,192.0.2.2,2001:db8::1", "192.0.2.1,2001:db8::1,192.0.2.2")]
        [InlineData("2001:db8::1,192.0.2.1,192.0.2.2,192.0.2.3", "2001:db8::1,192.0.2.1,192.0.2.2,192.0.2.3")]
        [InlineData("192.0.2.1,192.0.2.2", "192.0.2.1,192.0.2.2")]
        [InlineData("2001:db8::1", "2001:db8::1")]
        public void Should_Interleave_Address_Families_Starting_With_The_First(string resolved, string expected) {
            IPAddress[] input = [.. resolved.Split(',').Select(Ip)];

            IPAddress[] ordered = HappyEyeballsConnector.Interleave(input);

            Assert.Equal(expected.Split(',').Select(Ip), ordered);
        }

        [Fact]
        public void Should_Return_Nothing_For_No_Addresses() {
            Assert.Empty(HappyEyeballsConnector.Interleave([]));
        }

        [Fact]
        public void Should_Refuse_An_Attempt_Delay_Below_The_Rfc_Minimum() {
            // RFC 8305 §5: a subsequent connection MUST NOT be started within 10 milliseconds of the previous attempt.
            Assert.Throws<ArgumentOutOfRangeException>(() => new HappyEyeballsConnector(TimeSpan.FromMilliseconds(9), TimeProvider.System, HappyEyeballsConnector.ConnectSocketAsync));
            _ = new HappyEyeballsConnector(TimeSpan.FromMilliseconds(10), TimeProvider.System, HappyEyeballsConnector.ConnectSocketAsync);
        }

        [Fact]
        public void Should_Use_The_Recommended_Attempt_Delay_By_Default() {
            Assert.Equal(TimeSpan.FromMilliseconds(250), HappyEyeballsConnector.Default.AttemptDelay);
        }
    }

    public sealed class RacingAttempts {
        private static readonly IPAddress First = Ip("2001:db8::1");
        private static readonly IPAddress Second = Ip("192.0.2.1");
        private static readonly IPAddress Third = Ip("2001:db8::2");

        [Fact]
        public async Task Should_Start_The_Next_Attempt_Only_When_The_Delay_Elapses() {
            FakeTimeProvider clock = new();
            ScriptedConnect connect = new();
            HappyEyeballsConnector connector = new(Delay, clock, connect.ConnectAsync);

            ValueTask<Socket> connecting = connector.ConnectAsync([First, Second], 443, Ct);

            Assert.Equal(First, (await connect.NextStartedAsync()).Address);
            clock.Advance(Delay - TimeSpan.FromMilliseconds(1));
            Assert.Equal(1, connect.Started);

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(Second, (await connect.NextStartedAsync()).Address);
            Assert.Equal(443, connect.Attempts[1].Endpoint.Port);

            // The earlier attempt keeps running: whichever answers first wins.
            using Socket socket = UnconnectedSocket();
            connect.For(First).SetResult(socket);
            Assert.Same(socket, await connecting);
        }

        [Fact]
        public async Task Should_Start_The_Next_Attempt_At_Once_When_One_Fails() {
            FakeTimeProvider clock = new();
            ScriptedConnect connect = new();
            HappyEyeballsConnector connector = new(Delay, clock, connect.ConnectAsync);
            connect.For(First).SetException(new SocketException((int)SocketError.ConnectionRefused));
            using Socket socket = UnconnectedSocket();
            connect.For(Second).SetResult(socket);

            // The clock never moves: only the failure can have started the second attempt.
            Assert.Same(socket, await connector.ConnectAsync([First, Second], 443, Ct));
            Assert.Equal(2, connect.Started);
        }

        [Fact]
        public async Task Should_Cancel_The_Attempts_Still_Pending_When_One_Succeeds() {
            FakeTimeProvider clock = new();
            ScriptedConnect connect = new();
            HappyEyeballsConnector connector = new(Delay, clock, connect.ConnectAsync);

            ValueTask<Socket> connecting = connector.ConnectAsync([First, Second, Third], 443, Ct);
            await connect.NextStartedAsync();
            clock.Advance(Delay);
            await connect.NextStartedAsync();

            using Socket socket = UnconnectedSocket();
            connect.For(Second).SetResult(socket);
            Assert.Same(socket, await connecting);

            Assert.True(connect.Attempts[0].Token.IsCancellationRequested);
            clock.Advance(Delay * 10);
            Assert.Equal(2, connect.Started);
        }

        [Fact]
        public async Task Should_Dispose_A_Socket_A_Losing_Attempt_Opens_Anyway() {
            FakeTimeProvider clock = new();
            ScriptedConnect connect = new();
            HappyEyeballsConnector connector = new(Delay, clock, connect.ConnectAsync);

            ValueTask<Socket> connecting = connector.ConnectAsync([First, Second], 443, Ct);
            await connect.NextStartedAsync();
            clock.Advance(Delay);
            await connect.NextStartedAsync();

            using Socket winner = UnconnectedSocket();
            connect.For(Second).SetResult(winner);
            Assert.Same(winner, await connecting);

            // The first attempt ignores its cancellation and completes afterwards: nobody else holds its socket.
            Socket loser = UnconnectedSocket();
            connect.For(First).SetResult(loser);

            await WaitUntilAsync(() => loser.SafeHandle.IsClosed);
            Assert.False(winner.SafeHandle.IsClosed);
        }

        [Fact]
        public async Task Should_Report_The_Last_Failure_When_Every_Attempt_Fails() {
            ScriptedConnect connect = new();
            HappyEyeballsConnector connector = new(Delay, new FakeTimeProvider(), connect.ConnectAsync);
            connect.For(First).SetException(new SocketException((int)SocketError.NetworkUnreachable));
            connect.For(Second).SetException(new SocketException((int)SocketError.ConnectionRefused));

            SocketException error = await Assert.ThrowsAsync<SocketException>(() => connector.ConnectAsync([First, Second], 443, Ct).AsTask());

            Assert.Equal(SocketError.ConnectionRefused, error.SocketErrorCode);
            Assert.Equal(2, connect.Started);
        }

        [Fact]
        public async Task Should_Cancel_Every_Pending_Attempt_When_The_Caller_Cancels() {
            FakeTimeProvider clock = new();
            ScriptedConnect connect = new();
            HappyEyeballsConnector connector = new(Delay, clock, connect.ConnectAsync);
            using CancellationTokenSource caller = new();

            ValueTask<Socket> connecting = connector.ConnectAsync([First, Second, Third], 443, caller.Token);
            await connect.NextStartedAsync();
            clock.Advance(Delay);
            await connect.NextStartedAsync();

            caller.Cancel();
            connect.For(First).SetCanceled(connect.Attempts[0].Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.AsTask());
            Assert.All(connect.Attempts, attempt => Assert.True(attempt.Token.IsCancellationRequested));

            // No attempt starts after the caller gave up.
            clock.Advance(Delay * 10);
            Assert.Equal(2, connect.Started);
        }

        [Fact]
        public async Task Should_Not_Report_A_Cancellation_As_A_Failure_Of_The_Last_Attempt() {
            ScriptedConnect connect = new();
            HappyEyeballsConnector connector = new(Delay, new FakeTimeProvider(), connect.ConnectAsync);
            using CancellationTokenSource caller = new();

            ValueTask<Socket> connecting = connector.ConnectAsync([First], 443, caller.Token);
            await connect.NextStartedAsync();
            caller.Cancel();
            connect.For(First).SetException(new SocketException((int)SocketError.OperationAborted));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.AsTask());
        }
    }

    public sealed class ThroughTheHandler {
        private static readonly OutboundNetworkPolicy LoopbackAllowed = OutboundNetworkPolicy.PublicOnly with {
            AllowedNetworks = [IPNetwork.Parse("127.0.0.0/8"), IPNetwork.Parse("2001:db8::/32")]
        };

        [Fact]
        public async Task Should_Reach_A_Healthy_Address_Behind_One_That_Never_Answers_Within_The_Connect_Timeout() {
            using LoopbackHttpServer server = new();
            IPAddress blackholed = Ip("2001:db8::dead");
            FakeDnsResolver resolver = new(new Dictionary<string, IPAddress[]> {
                ["dual-stack.test"] = [blackholed, IPAddress.Loopback]
            });
            TaskCompletionSource blackholeCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

            async ValueTask<Socket> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken) {
                if(endpoint.Address.Equals(blackholed)) {
                    // A path that silently drops packets: the attempt only ends when it is given up.
                    await using CancellationTokenRegistration registration = cancellationToken.Register(() => blackholeCancelled.TrySetResult());
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                return await HappyEyeballsConnector.ConnectSocketAsync(endpoint, cancellationToken);
            }

            TimeSpan connectTimeout = TimeSpan.FromSeconds(5);
            SocketsHttpHandler handler = new SocketsHttpHandler { ConnectTimeout = connectTimeout }
                .UseOutboundNetworkPolicy(LoopbackAllowed, resolver, new HappyEyeballsConnector(HappyEyeballsConnector.DefaultAttemptDelay, TimeProvider.System, ConnectAsync));
            using HttpClient client = new(handler);

            Stopwatch elapsed = Stopwatch.StartNew();
            using HttpResponseMessage response = await client.GetAsync($"http://dual-stack.test:{server.Port}/", Ct);
            elapsed.Stop();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, server.Connections);
            Assert.True(elapsed.Elapsed < connectTimeout, $"Connecting took {elapsed.Elapsed}, the whole connect timeout.");
            await blackholeCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }

        [Fact]
        public async Task Should_Never_Attempt_A_Refused_Address() {
            ScriptedConnect connect = new();
            IPAddress allowed = Ip("8.8.8.8");
            FakeDnsResolver resolver = new(new Dictionary<string, IPAddress[]> {
                ["mixed.test"] = [Ip("169.254.169.254"), IPAddress.Loopback, Ip("::1"), allowed]
            });
            connect.For(allowed).SetException(new SocketException((int)SocketError.ConnectionRefused));
            HappyEyeballsConnector connector = new(Delay, new FakeTimeProvider(), connect.ConnectAsync);

            await Assert.ThrowsAsync<SocketException>(() =>
                OutboundNetworkPolicyHandlerExtensions.ConnectAsync(new DnsEndPoint("mixed.test", 443), OutboundNetworkPolicy.PublicOnly, resolver, connector, Ct).AsTask());

            Assert.Equal([allowed], connect.Attempts.Select(attempt => attempt.Endpoint.Address));
        }

        [Fact]
        public async Task Should_Refuse_Without_Attempting_When_No_Address_Is_Allowed() {
            ScriptedConnect connect = new();
            FakeDnsResolver resolver = new(new Dictionary<string, IPAddress[]> {
                ["internal.test"] = [Ip("10.0.0.5"), Ip("fd00::5")]
            });
            HappyEyeballsConnector connector = new(Delay, new FakeTimeProvider(), connect.ConnectAsync);

            await Assert.ThrowsAsync<OutboundNetworkPolicyException>(() =>
                OutboundNetworkPolicyHandlerExtensions.ConnectAsync(new DnsEndPoint("internal.test", 443), OutboundNetworkPolicy.PublicOnly, resolver, connector, Ct).AsTask());

            Assert.Equal(0, connect.Started);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition) {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while(!condition()) {
            await Task.Delay(10, timeout.Token);
        }
    }
}
