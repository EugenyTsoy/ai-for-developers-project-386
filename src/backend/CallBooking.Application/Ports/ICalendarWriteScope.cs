namespace CallBooking.Application.Ports;

/// <summary>
/// Serializes a synchronous state-dependent operation with all calendar writes.
/// The operation completes, including persistence, before returning. Callbacks
/// must not start asynchronous work or retain the scope after they return.
/// </summary>
public interface ICalendarWriteScope
{
    T Execute<T>(Func<T> operation);
}
