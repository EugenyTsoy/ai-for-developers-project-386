using CallBooking.Application.Calendar;
using CallBooking.Application.Ports;
using CallBooking.Domain.Calendar;
using CallBooking.Domain.Entities;

namespace CallBooking.Application.Bookings;

public enum BookingCreationOutcome
{
    Created,
    InvalidInput,
    UnknownEventType,
    Unavailable
}

public sealed record BookingCreationResult(BookingCreationOutcome Outcome, Booking? Booking = null);

public sealed class CreateBooking(
    IEventTypeRepository eventTypes, IBookingRepository bookings,
    CalendarContext calendar, ICalendarWriteScope writeScope)
{
    public BookingCreationResult Execute(string eventTypeId, DateTimeOffset start, string name, string email)
    {
        var guest = GuestContact.TryCreate(name, email);
        if (guest is null)
        {
            return new BookingCreationResult(BookingCreationOutcome.InvalidInput);
        }

        return writeScope.Execute(() =>
        {
            var eventType = eventTypes.GetById(eventTypeId);
            if (eventType is null)
            {
                return new BookingCreationResult(BookingCreationOutcome.UnknownEventType);
            }

            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, calendar.Configuration.TimeZone).DateTime);
            // This grid has no current availability filters: stale canonical
            // choices must not become invalid input. Grid end uses elapsed time
            // and the authoritative saved duration, including across DST.
            var slot = Availability.GetSlotGrid(calendar.Configuration, date, eventType)
                .FirstOrDefault(candidate => candidate.Start == start);
            if (slot is null)
            {
                return new BookingCreationResult(BookingCreationOutcome.InvalidInput);
            }

            var now = calendar.GetUtcNow();
            var occupied = bookings.List().Select(booking => new TimeInterval(booking.Start, booking.End)).ToArray();
            if (!Availability.IsCurrentlyAvailable(slot, now,
                Availability.GetBookingWindow(calendar.Configuration, now), occupied))
            {
                return new BookingCreationResult(BookingCreationOutcome.Unavailable);
            }

            var saved = new Booking(Guid.NewGuid().ToString("N"), eventType.Id, start, slot.End, guest.Name, guest.Email);
            bookings.Add(saved);
            return new BookingCreationResult(BookingCreationOutcome.Created, saved);
        });
    }
}
