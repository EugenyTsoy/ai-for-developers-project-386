using CallBooking.Domain;

namespace CallBooking.Application.Ports;

/// <summary>
/// Outbound port that surfaces the authoritative calendar configuration.
/// Concrete providers live in the Infrastructure layer and read
/// configuration from their host.
/// </summary>
public interface ICalendarConfigProvider
{
    /// <summary>
    /// Read the resolved <see cref="CalendarConfig"/>.
    /// </summary>
    CalendarConfig GetConfig();

    /// <summary>
    /// Resolve the working window for a specific local date in the
    /// calendar's timezone.
    /// </summary>
    (Instant Open, Instant Close)? GetWorkingWindow(
        LocalDate date);
}
