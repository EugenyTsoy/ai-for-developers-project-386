using CallBooking.Domain.Entities;

namespace CallBooking.Infrastructure.InMemory;

/// <summary>
/// In-memory calendar store seam. Holds the persisted event types
/// and bookings for the lifetime of a single host and exposes them
/// to the repository implementations in <c>InMemory</c>.
/// </summary>
/// <remarks>
/// <para>
/// This interface is intentionally part of Infrastructure, not
/// Domain or Application: the storage shape is an implementation
/// detail. Application services depend on
/// <see cref="CallBooking.Application.Ports.IEventTypeRepository"/>
/// and <see cref="CallBooking.Application.Ports.IBookingRepository"/>,
/// not on this seam directly.
/// </para>
/// <para>
/// <see cref="Reset"/> is the test-host isolation seam called out in
/// issue #20 (TK2): it lets the storage seam tests drop every
/// persisted entity between tests so that mutable state is never
/// shared. Production code never calls it. It is exposed only via
/// <see cref="IServiceProvider"/>, never via any HTTP endpoint or
/// response DTO.
/// </para>
/// </remarks>
public interface IInMemoryCalendarStore : CallBooking.Application.Ports.ICalendarWriteScope
{
    void AddEventType(EventType eventType);

    void AddBooking(Booking booking);

    IReadOnlyCollection<EventType> EventTypes { get; }

    IReadOnlyCollection<Booking> Bookings { get; }

    /// <summary>
    /// Drops every persisted event type and booking. Test-host
    /// isolation seam only; never invoked from request handling.
    /// </summary>
    void Reset();
}
