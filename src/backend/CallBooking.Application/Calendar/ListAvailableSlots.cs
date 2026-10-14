using CallBooking.Application.Ports;
using CallBooking.Domain.Calendar;

namespace CallBooking.Application.Calendar;

/// <summary>Reads authoritative state without reserving or saving anything.</summary>
public sealed class ListAvailableSlots(
    IEventTypeRepository eventTypes, IBookingRepository bookings, CalendarContext calendar)
{
    /// <returns>Null for an unknown type; otherwise the available intervals, possibly empty.</returns>
    public IReadOnlyList<TimeInterval>? Execute(string eventTypeId)
    {
        var eventType = eventTypes.GetById(eventTypeId);
        return eventType is null
            ? null
            : Availability.List(calendar.Configuration, eventType, calendar.GetUtcNow(), bookings.List());
    }
}
