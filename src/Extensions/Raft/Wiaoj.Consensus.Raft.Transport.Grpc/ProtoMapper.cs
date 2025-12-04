using Google.Protobuf;
using AbstractionsDomain = Wiaoj.Consensus.Raft.Abstractions;

namespace Wiaoj.Consensus.Raft.Transport.Grpc;

/// <summary>
/// A static helper class to map between the gRPC generated proto types and the
/// clean, technology-agnostic types defined in the Abstractions project.
/// </summary>
internal static class ProtoMapper {
    // Abstraction -> Proto
    public static RequestVoteArgs ToProto(this AbstractionsDomain.RequestVoteArgs args) {
        return new RequestVoteArgs {
            Term = args.Term.Value,
            CandidateId = args.CandidateId.Value,
            LastLogIndex = args.LastLogIndex.Value,
            LastLogTerm = args.LastLogTerm.Value
        };
    }

    // Abstraction -> Proto
    public static AppendEntriesArgs ToProto(this AbstractionsDomain.AppendEntriesArgs args) {
        AppendEntriesArgs protoArgs = new() {
            Term = args.Term.Value,
            LeaderId = args.LeaderId.Value,
            PrevLogIndex = args.PrevLogIndex.Value,
            PrevLogTerm = args.PrevLogTerm.Value,
            LeaderCommitIndex = args.LeaderCommitIndex.Value
        };
        // IReadOnlyList<LogEntry> -> RepeatedField<Grpc.LogEntry>
        foreach (var entry in args.Entries) {
            protoArgs.Entries.Add(new LogEntry {
                Term = entry.Term,
                Command = ByteString.CopyFrom(entry.Command)
            });
        }
        return protoArgs;
    }

    // Proto -> Abstraction
    public static AbstractionsDomain.RequestVoteResult ToAbstract(this RequestVoteResult result) {
        return new AbstractionsDomain.RequestVoteResult {
            Term = new AbstractionsDomain.Term(result.Term),
            VoteGranted = result.VoteGranted
        };
    }

    // Proto -> Abstraction
    public static AbstractionsDomain.AppendEntriesResult ToAbstract(this AppendEntriesResult result) {
        return new AbstractionsDomain.AppendEntriesResult {
            Term = new AbstractionsDomain.Term(result.Term),
            Success = result.Success
        };
    }

    // Proto -> Abstraction
    public static AbstractionsDomain.RequestVoteArgs ToAbstract(this RequestVoteArgs args) {
        return new AbstractionsDomain.RequestVoteArgs {
            Term = new AbstractionsDomain.Term(args.Term),
            CandidateId = new AbstractionsDomain.NodeId(args.CandidateId),
            LastLogIndex = new AbstractionsDomain.LogIndex(args.LastLogIndex),
            LastLogTerm = new AbstractionsDomain.Term(args.LastLogTerm)
        };
    }

    // Proto -> Abstraction
    public static AbstractionsDomain.AppendEntriesArgs ToAbstract(this AppendEntriesArgs args) {
        return new AbstractionsDomain.AppendEntriesArgs {
            Term = new AbstractionsDomain.Term(args.Term),
            LeaderId = new AbstractionsDomain.NodeId(args.LeaderId),
            PrevLogIndex = new AbstractionsDomain.LogIndex(args.PrevLogIndex),
            PrevLogTerm = new AbstractionsDomain.Term(args.PrevLogTerm),
            LeaderCommitIndex = new AbstractionsDomain.LogIndex(args.LeaderCommitIndex),
            // RepeatedField<Grpc.LogEntry> -> IReadOnlyList<LogEntry>
            Entries = args.Entries.Select(e => new Abstractions.LogEntry {
                Term = e.Term,
                Command = e.Command.ToByteArray()
            }).ToList().AsReadOnly()
        };
    }

    // Abstraction -> Proto (InstallSnapshot)
    public static InstallSnapshotArgs ToProto(this AbstractionsDomain.InstallSnapshotArgs args) {
        return new InstallSnapshotArgs {
            Term = args.Term.Value,
            LeaderId = args.LeaderId.Value,
            LastIncludedIndex = args.LastIncludedIndex.Value,
            LastIncludedTerm = args.LastIncludedTerm.Value,
            Data = ByteString.CopyFrom(args.Data)
        };
    }

    // Proto -> Abstraction (InstallSnapshotResult)
    public static AbstractionsDomain.InstallSnapshotResult ToAbstract(this InstallSnapshotResult result) {
        return new AbstractionsDomain.InstallSnapshotResult {
            Term = new AbstractionsDomain.Term(result.Term)
        };
    }

    // Proto -> Abstraction (InstallSnapshotArgs)
    public static AbstractionsDomain.InstallSnapshotArgs ToAbstract(this InstallSnapshotArgs args) {
        return new AbstractionsDomain.InstallSnapshotArgs {
            Term = new AbstractionsDomain.Term(args.Term),
            LeaderId = new AbstractionsDomain.NodeId(args.LeaderId),
            LastIncludedIndex = new AbstractionsDomain.LogIndex(args.LastIncludedIndex),
            LastIncludedTerm = new AbstractionsDomain.Term(args.LastIncludedTerm),
            Data = args.Data.ToByteArray()
        };
    }
}