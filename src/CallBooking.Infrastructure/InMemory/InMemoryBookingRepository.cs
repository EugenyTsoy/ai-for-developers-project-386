using CallBooking.Application.Ports;
using CallBooking.Domain;
using CallBooking.Infrastructure.Storage;

namespace CallBooking.Infrastructure.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IBookingRepository"/>.
/// The atomic check-and-create algorithm lives in the application
/// layer; this class is a plain CRUD store.
/// </summary>
public sealed class InMemoryBookingRepository
    : IBookingRepository, IResettableStorage
{
    private readonly object _lock = new();
    private readonly List<Booking> _items = new();

    public Task<Booking> AddAsync(
        EventTypeId eventTypeId,
        TimeInterval interval,
        GuestContact guest,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var id = BookingId.FromServer("bkg_" + Guid.NewGuid().ToString("N"));
            var entity = new Booking(id, eventTypeId, interval, guest);
            _items.Add(entity);
            return Task.FromResult(entity);
        }
    }

    public Task<IReadOnlyList<Booking>> ListOverlappingAsync(
        TimeInterval interval,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<Booking> snapshot = _items
                .Where(item => item.Interval.Overlaps(interval))
                .ToArray();
            return Task.FromResult(snapshot);
        }
    }

    public Task<IReadOnlyList<Booking>> ListUpcomingAsync(
        Instant nowUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<Booking> snapshot = _items
                .Where(item => item.Interval.Start.Utc > nowUtc.Utc)
                .OrderBy(item => item.Interval.Start.Utc)
                .ToArray();
            return Task.FromResult(snapshot);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }
}
