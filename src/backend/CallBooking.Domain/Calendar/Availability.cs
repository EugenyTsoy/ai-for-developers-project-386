using CallBooking.Domain.Entities;

namespace CallBooking.Domain.Calendar;

/// <summary>Calendar rules on the elapsed-time timeline, independent of persistence.</summary>
public static class Availability
{
    private static readonly TimeSpan StartInterval = TimeSpan.FromMinutes(30);

    public static IReadOnlyList<TimeInterval> List(
        CalendarConfiguration calendar, EventType eventType, DateTimeOffset now, IReadOnlyList<Booking> bookings)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, calendar.TimeZone).DateTime);
        var bookingWindow = GetBookingWindow(calendar, now);
        var occupied = bookings.Select(booking => new TimeInterval(booking.Start, booking.End)).ToArray();
        List<TimeInterval> available = [];
        for (var day = 0; day < 14; day++)
        {
            foreach (var slot in GetSlotGrid(calendar, today.AddDays(day), eventType))
            {
                if (IsCurrentlyAvailable(slot, now, bookingWindow, occupied))
                {
                    available.Add(slot);
                }
            }
        }

        return available;
    }

    /// <summary>Current-state filters only; canonical grid membership is checked separately.</summary>
    public static bool IsCurrentlyAvailable(
        TimeInterval slot, DateTimeOffset now, TimeInterval bookingWindow, IReadOnlyList<TimeInterval> occupied)
        => slot.Start > now && bookingWindow.Contains(slot) && !occupied.Any(slot.Overlaps);

    public static TimeInterval GetBookingWindow(CalendarConfiguration calendar, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, calendar.TimeZone).DateTime);
        return new TimeInterval(StartOfDate(today, calendar.TimeZone), StartOfDate(today.AddDays(14), calendar.TimeZone));
    }

    /// <summary>Canonical grid, without current-time, booking-window or occupancy filters.</summary>
    public static IReadOnlyList<TimeInterval> GetSlotGrid(
        CalendarConfiguration calendar, DateOnly date, EventType eventType)
    {
        if (!calendar.WorkingWeek.TryGetValue(date.DayOfWeek, out var window))
        {
            return [];
        }

        var localOpen = date.ToDateTime(window.Open);
        var localClose = date.ToDateTime(window.Close);
        if (calendar.TimeZone.IsInvalidTime(localOpen) || calendar.TimeZone.IsInvalidTime(localClose))
        {
            return [];
        }

        var open = ResolveBoundary(localOpen, calendar.TimeZone, earlier: true);
        var close = ResolveBoundary(localClose, calendar.TimeZone, earlier: false);
        var duration = TimeSpan.FromMinutes(eventType.DurationMinutes);
        List<TimeInterval> slots = [];
        for (var start = open; close - start >= duration; start += StartInterval)
        {
            slots.Add(new TimeInterval(start, start + duration));
        }

        return slots;
    }

    private static DateTimeOffset StartOfDate(DateOnly date, TimeZoneInfo timeZone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        if (!timeZone.IsInvalidTime(midnight))
        {
            return ResolveBoundary(midnight, timeZone, earlier: true);
        }

        // IANA transitions have whole-second precision. Search within the last
        // invalid minute at that precision (platform rules can use .999 edges).
        // Working-window boundaries are never shifted.
        var invalid = midnight;
        var valid = midnight.AddMinutes(1);
        while (timeZone.IsInvalidTime(valid))
        {
            invalid = valid;
            valid = valid.AddMinutes(1);
        }

        while (valid - invalid > TimeSpan.FromSeconds(1))
        {
            var middle = invalid.AddSeconds((long)(valid - invalid).TotalSeconds / 2);
            if (timeZone.IsInvalidTime(middle))
            {
                invalid = middle;
            }
            else
            {
                valid = middle;
            }
        }

        return ResolveBoundary(valid, timeZone, earlier: true);
    }

    private static DateTimeOffset ResolveBoundary(DateTime local, TimeZoneInfo timeZone, bool earlier)
    {
        if (timeZone.IsAmbiguousTime(local))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(local);
            // Larger offset gives the earlier absolute instant.
            return new DateTimeOffset(local, earlier ? offsets.Max() : offsets.Min()).ToUniversalTime();
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone));
    }
}
