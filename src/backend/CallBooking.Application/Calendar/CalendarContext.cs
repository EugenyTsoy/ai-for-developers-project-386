using CallBooking.Domain.Calendar;

namespace CallBooking.Application.Calendar;

/// <summary>
/// Authoritative calendar configuration and current instant for application
/// scenarios. Time is read from the injected provider, never captured at startup.
/// </summary>
public sealed class CalendarContext
{
    private readonly TimeProvider _timeProvider;

    public CalendarConfiguration Configuration { get; }

    public CalendarContext(CalendarConfiguration configuration, TimeProvider timeProvider)
    {
        Configuration = configuration;
        _timeProvider = timeProvider;
    }

    public DateTimeOffset GetUtcNow() => _timeProvider.GetUtcNow();
}
