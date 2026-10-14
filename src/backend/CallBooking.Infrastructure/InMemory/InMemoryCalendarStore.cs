using System.Collections.Concurrent;
using CallBooking.Domain.Entities;

namespace CallBooking.Infrastructure.InMemory;

/// <summary>
/// Singleton thread-safe in-memory calendar store. A single instance
/// is shared by every <see cref="IEventTypeRepository"/> and
/// <see cref="IBookingRepository"/> inside one host, so that saved
/// entities become available to subsequent reads through the same
/// application instance without falling back to independent
/// placeholder collections.
/// </summary>
/// <remarks>
/// <para>
/// TK2 does not require durability or restart survival; the ticket
/// explicitly leaves storage technology free of choice and the
/// approved scope does not include restart-recovery.
/// </para>
/// <para>
/// DateTimeOffset values are stored verbatim with their offset. The
/// approved spec guarantees that an absolute UTC instant is not
/// re-interpreted under any later timezone change, and
/// <see cref="DateTimeOffset"/> carries its offset inside the value
/// so no ambient timezone affects it.
/// </para>
/// </remarks>
public sealed class InMemoryCalendarStore : IInMemoryCalendarStore
{
    // One owner-wide gate shared by every request scope and every event type.
    // Repository writes and reset participate, so none can interleave with
    // authoritative check-and-create. Monitor is reentrant for callback writes.
    private readonly object _writeGate = new();
    private readonly ICalendarWriteObserver? _writeObserver;

    public InMemoryCalendarStore()
    {
    }

    internal InMemoryCalendarStore(ICalendarWriteObserver writeObserver)
    {
        _writeObserver = writeObserver;
    }

    public T Execute<T>(Func<T> operation)
    {
        // Keep the same owner-wide, reentrant gate. A failed immediate entry
        // lets internal test-host instrumentation observe real contention,
        // rather than merely observing requests just before acquisition.
        var entered = Monitor.TryEnter(_writeGate);
        try
        {
            if (!entered)
            {
                _writeObserver?.Contended();
                Monitor.Enter(_writeGate, ref entered);
            }
            _writeObserver?.Entered();
            return operation();
        }
        finally
        {
            if (entered)
            {
                Monitor.Exit(_writeGate);
            }
        }
    }

    private readonly ConcurrentDictionary<string, EventType> _eventTypes =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, Booking> _bookings =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<EventType> EventTypes =>
        _eventTypes.Values.ToArray();

    public IReadOnlyCollection<Booking> Bookings =>
        _bookings.Values.ToArray();

    public void AddEventType(EventType eventType)
    {
        if (eventType is null)
        {
            throw new ArgumentNullException(nameof(eventType));
        }

        lock (_writeGate)
        {
            _eventTypes[eventType.Id] = eventType;
        }
    }

    public void AddBooking(Booking booking)
    {
        if (booking is null)
        {
            throw new ArgumentNullException(nameof(booking));
        }

        lock (_writeGate)
        {
            _bookings[booking.Id] = booking;
        }
    }

    public void Reset()
    {
        lock (_writeGate)
        {
            _eventTypes.Clear();
            _bookings.Clear();
        }
    }
}
