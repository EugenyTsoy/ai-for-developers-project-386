using CallBooking.Domain;

namespace CallBooking.Application.Ports;

/// <summary>
/// Outbound port for <see cref="Booking"/> storage. Concrete
/// implementations live in the Infrastructure layer.
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Append a new booking. The repository assigns the technical id
    /// and links the booking to its <see cref="EventType"/>.
    /// </summary>
    Task<Booking> AddAsync(
        EventTypeId eventTypeId,
        TimeInterval interval,
        GuestContact guest,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read every stored booking whose interval strictly overlaps
    /// <paramref name="interval"/>. Used by availability and atomic
    /// creation paths.
    /// </summary>
    Task<IReadOnlyList<Booking>> ListOverlappingAsync(
        TimeInterval interval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read upcoming bookings (start strictly after <paramref name="nowUtc"/>),
    /// sorted by start ascending.
    /// </summary>
    Task<IReadOnlyList<Booking>> ListUpcomingAsync(
        Instant nowUtc,
        CancellationToken cancellationToken = default);
}
