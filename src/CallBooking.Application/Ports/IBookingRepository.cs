using CallBooking.Domain.Entities;

namespace CallBooking.Application.Ports;

/// <summary>
/// Hand-written outbound port for booking persistence. Application
/// services depend on this abstraction; the infrastructure layer
/// owns the concrete implementation. The interface works only with
/// domain types and absolute instants, never with generated
/// transport DTOs or HTTP primitives.
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Persists a freshly-created booking.
    /// </summary>
    void Add(Booking booking);

    /// <summary>
    /// Returns the booking with the given id, or <c>null</c> when
    /// no such booking has been persisted.
    /// </summary>
    Booking? GetById(string id);

    /// <summary>
    /// Returns every persisted booking regardless of start time or
    /// event type. Filtering and ordering are the caller's
    /// responsibility.
    /// </summary>
    IReadOnlyList<Booking> List();
}
