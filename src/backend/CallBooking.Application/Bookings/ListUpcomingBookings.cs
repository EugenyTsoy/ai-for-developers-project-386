using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;

namespace CallBooking.Application.Bookings;

public sealed class ListUpcomingBookings(IBookingRepository bookings, IEventTypeRepository eventTypes, TimeProvider timeProvider)
{
    public IReadOnlyList<(Booking Booking, EventType EventType)> Execute()
    {
        var now = timeProvider.GetUtcNow();
        return bookings.List()
            .Where(booking => booking.Start > now)
            .OrderBy(booking => booking.Start)
            .Select(booking => (booking, eventTypes.GetById(booking.EventTypeId)
                ?? throw new InvalidOperationException("A saved booking must reference a saved event type.")))
            .ToArray();
    }
}
