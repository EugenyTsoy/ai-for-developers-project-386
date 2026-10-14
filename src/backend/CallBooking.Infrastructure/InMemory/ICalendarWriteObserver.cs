namespace CallBooking.Infrastructure.InMemory;

/// <summary>
/// Optional internal test-host instrumentation, never registered by production.
/// Reports actual contention and entry without exposing the synchronization primitive.
/// Contended must not block; Entered may pause the protected operation in tests.
/// Callbacks must not reenter the store.
/// </summary>
internal interface ICalendarWriteObserver
{
    void Contended();

    void Entered();
}
