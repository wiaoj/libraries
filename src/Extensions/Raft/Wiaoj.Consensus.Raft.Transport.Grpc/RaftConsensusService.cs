using Grpc.Core;
using Microsoft.Extensions.Logging;
using Wiaoj.Consensus.Raft.Abstractions;

namespace Wiaoj.Consensus.Raft.Transport.Grpc;

public class RaftConsensusService : RaftConsensus.RaftConsensusBase {
    private readonly ILogger<RaftConsensusService> _logger;
    private readonly IRaftEngine _raftEngine;

    public RaftConsensusService(ILogger<RaftConsensusService> logger, IRaftEngine raftEngine) {
        this._logger = logger;
        this._raftEngine = raftEngine;
    }

    public override async Task<RequestVoteResult> RequestVote(RequestVoteArgs request, ServerCallContext context) {
        this._logger.LogDebug("RequestVote RPC çağrısı alındı: Aday={CandidateId}, Dönem={Term}", request.CandidateId, request.Term);

        // 1. Gelen Proto isteğini soyut isteğe çevir
        Abstractions.RequestVoteArgs abstractArgs = request.ToAbstract();
        // 2. Çağrıyı motora delege et
        Abstractions.RequestVoteResult abstractResult = await this._raftEngine.HandleRequestVoteAsync(abstractArgs);
        // 3. Motordan gelen soyut sonucu Proto sonucuna çevir ve döndür
        return new RequestVoteResult {
            Term = abstractResult.Term.Value,
            VoteGranted = abstractResult.VoteGranted
        };
    }

    public override async Task<AppendEntriesResult> AppendEntries(AppendEntriesArgs request, ServerCallContext context) {
        this._logger.LogTrace("AppendEntries RPC çağrısı alındı: Lider={LeaderId}, Dönem={Term}", request.LeaderId, request.Term);

        // Aynı çeviri mantığı burada da geçerli
        Abstractions.AppendEntriesArgs abstractArgs = request.ToAbstract();
        Abstractions.AppendEntriesResult abstractResult = await this._raftEngine.HandleAppendEntriesAsync(abstractArgs);
        return new AppendEntriesResult {
            Term = abstractResult.Term.Value,
            Success = abstractResult.Success
        };
    }

    public override async Task<InstallSnapshotResult> InstallSnapshot(InstallSnapshotArgs request, ServerCallContext context) {
        _logger.LogTrace("InstallSnapshot RPC çağrısı alındı: Lider={LeaderId}, Dönem={Term}", request.LeaderId, request.Term);

        Abstractions.InstallSnapshotArgs abstractArgs = request.ToAbstract();
        Abstractions.InstallSnapshotResult abstractResult = await _raftEngine.HandleInstallSnapshotAsync(abstractArgs);

        return new InstallSnapshotResult {
            Term = abstractResult.Term.Value
        };
    }
}