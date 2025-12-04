//using Wiaoj.Abstractions;

//namespace Wiaoj.Consensus.Raft.Persistence.Faster;

///// <summary>
///// Creates and asynchronously initializes a FasterState instance.
///// This factory is the key to solving the startup race condition.
///// </summary>
//public sealed class FasterStateFactory(RaftNodeOptions options) : IAsyncFactory<FasterState> {
//    public async Task<FasterState> CreateAsync(CancellationToken cancellationToken = default) {
//        // Ensure the persistence directory exists.
//        Directory.CreateDirectory(options.PersistencePath);

//        string logPath = Path.Combine(options.PersistencePath, "raft.log");
//        string metadataPath = Path.Combine(options.PersistencePath, "raft.meta.log");

//        // 1. Create the object synchronously.
//        FasterState fasterState = new(logPath, metadataPath);

//        try {
//            // 2. Await its asynchronous initialization.
//            // This guarantees the object is fully loaded from disk before being returned.
//            await fasterState.InitializeAsync();
//        }
//        catch (Exception) {
//            // If initialization fails, ensure resources are cleaned up.
//            fasterState.Dispose();
//            throw;
//        }

//        // 3. Return the fully initialized, ready-to-use object.
//        return fasterState;
//    }
//}