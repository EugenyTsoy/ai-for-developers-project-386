namespace CallBooking.Infrastructure.Storage;

/// <summary>
/// Test-only seam that drops every in-memory state. Concrete
/// repositories implement it so integration tests can reset state
/// without any new public API. Production code must not call it.
/// </summary>
public interface IResettableStorage
{
    /// <summary>
    /// Drop every in-memory state. Tests only; never invoked from
    /// production request paths.
    /// </summary>
    void Reset();
}
