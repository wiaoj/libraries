using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Wiaoj.Consensus.Raft.Abstractions; 
using static Wiaoj.Consensus.Raft.RaftConsensus;

namespace Wiaoj.Consensus.Raft.Transport.Grpc;

public sealed class GrpcCluster : IRaftCluster, IDisposable {
    private readonly ILogger<GrpcCluster> _logger;
    private readonly NodeId _selfId;
    private readonly IReadOnlyDictionary<NodeId, GrpcPeer> _peers;
    private readonly List<GrpcChannel> _channels = new();

    public GrpcCluster(RaftNodeOptions options, ILogger<GrpcCluster> logger) {
        _logger = logger;
        _selfId = NodeId.From(options.NodeId);

        var peerDict = new Dictionary<NodeId, GrpcPeer>();
        foreach (var peerAddress in options.Peers) {
            var peerId = NodeId.From(peerAddress);
            if (peerId == _selfId) continue;

            try {
                var channel = GrpcChannel.ForAddress(peerAddress);
                _channels.Add(channel);
                var client = new RaftConsensusClient(channel);
                peerDict[peerId] = new GrpcPeer(peerId, client);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "{PeerAddress} adresi için gRPC kanalı oluşturulamadı.", peerAddress);
            }
        }
        _peers = peerDict;
    }

    public IEnumerable<IRaftPeer> Peers => _peers.Values;
    public int TotalNodes => _peers.Count + 1;

    public void Dispose() {
        foreach (var channel in _channels) {
            channel.Dispose();
        }
    }

    private sealed class GrpcPeer : IRaftPeer {
        private readonly RaftConsensusClient _client;
        public NodeId Id { get; }

        public GrpcPeer(NodeId id, RaftConsensusClient client) {
            Id = id;
            _client = client;
        }

        //public async Task<Abstractions.RequestVoteResult> RequestVoteAsync(Abstractions.RequestVoteArgs args, CancellationToken cancellationToken) {
        //    // 1. Soyut objeyi Proto objesine çevir
        //    var protoArgs = args.ToProto();
        //    // 2. gRPC çağrısını yap
        //    var protoResult = await _client.RequestVoteAsync(protoArgs, cancellationToken: cancellationToken);
        //    // 3. Gelen Proto sonucunu soyut sonuca çevir ve döndür
        //    return protoResult.ToAbstract();
        //}

        //public async Task<Abstractions.AppendEntriesResult> AppendEntriesAsync(Abstractions.AppendEntriesArgs args, CancellationToken cancellationToken) {
        //    // Aynı çeviri mantığı burada da geçerli
        //    var protoArgs = args.ToProto();
        //    var protoResult = await _client.AppendEntriesAsync(protoArgs, cancellationToken: cancellationToken);
        //    return protoResult.ToAbstract();
        //}

        public async Task<Abstractions.RequestVoteResult> RequestVoteAsync(Abstractions.RequestVoteArgs args, CancellationToken cancellationToken) {
            try {
                var protoArgs = args.ToProto();
                var protoResult = await _client.RequestVoteAsync(protoArgs, cancellationToken: cancellationToken);
                return protoResult.ToAbstract();
            }
            // gRPC'ye özgü "İptal Edildi" hatasını yakala...
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) {
                // ...ve onu çekirdek katmanın anlayacağı genel bir OperationCanceledException'a dönüştür.
                // Orijinal hatayı InnerException olarak eklemek, hata ayıklama için iyi bir pratiktir.
                throw new OperationCanceledException("The gRPC call was canceled by the client.", ex);
            }
        }

        public async Task<Abstractions.AppendEntriesResult> AppendEntriesAsync(Abstractions.AppendEntriesArgs args, CancellationToken cancellationToken) {
            try {
                var protoArgs = args.ToProto();
                var protoResult = await _client.AppendEntriesAsync(protoArgs, cancellationToken: cancellationToken);
                return protoResult.ToAbstract();
            }
            // Aynı dönüşüm mantığını buraya da uygula.
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) {
                throw new OperationCanceledException("The gRPC call was canceled by the client.", ex);
            }
        }

        public async Task<Abstractions.InstallSnapshotResult> InstallSnapshotAsync(Abstractions.InstallSnapshotArgs args, CancellationToken cancellationToken) {
            try {
                var protoArgs = args.ToProto();
                var protoResult = await _client.InstallSnapshotAsync(protoArgs, cancellationToken: cancellationToken);
                return protoResult.ToAbstract();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) {
                throw new OperationCanceledException("The gRPC call was canceled by the client.", ex);
            }
        }
    }
}