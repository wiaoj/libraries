namespace Wiaoj.Security;

/// <summary>
/// Outcome of a <c>MasterKeyRewrapService.RewrapAllAsync</c> invocation.
/// </summary>
/// <param name="Total">Total number of key records examined.</param>
/// <param name="Rewrapped">Records successfully unwrapped with the previous master key and re-wrapped with the current one.</param>
/// <param name="AlreadyCurrent">Records that already unwrapped with the current master key (no work needed).</param>
/// <param name="Failed">Records that could not be unwrapped with either master key — manual recovery required.</param>
/// <param name="Duration">Wall-clock duration of the rewrap cycle.</param>
public readonly record struct MasterKeyRewrapResult(
    int Total,
    int Rewrapped,
    int AlreadyCurrent,
    int Failed,
    TimeSpan Duration) {

    /// <summary>True when every record is now wrapped with the current master key.</summary>
    public bool IsComplete => Failed == 0;
}
