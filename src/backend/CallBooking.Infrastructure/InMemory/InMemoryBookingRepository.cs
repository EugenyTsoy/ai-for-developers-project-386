using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;

namespace CallBooking.Infrastructure.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IBookingRepository"/>.
/// Routes every call through the shared
/// <see cref="IInMemoryCalendarStore"/> so that callers operating
/// through the same host observe the same persisted state.
/// </summary>
public sealed class InMemoryBookingRepository : IBookingRepository
{
    private readonly IInMemoryCalendarStore _store;

    public InMemoryBookingRepository(IInMemoryCalendarStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Add(Booking booking)
    {
        if (booking is null)
        {
            throw new ArgumentNullException(nameof(booking));
        }

        _store.AddBooking(booking);
    }

    public Booking? GetById(string id)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        return _store.Bookings.FirstOrDefault(b => b.Id == id);
    }

    public IReadOnlyList<Booking> List()
    {
        return _store.Bookings.ToArray();
    }
}
