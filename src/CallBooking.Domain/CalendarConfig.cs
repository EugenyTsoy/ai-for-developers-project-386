namespace CallBooking.Domain;

/// <summary>
/// IANA timezone identifier used by the calendar. Domain accepts an
/// IANA identifier string; conversion failure is surfaced as an
/// exception when an attempt is made to actually use it.
/// </summary>
public readonly record struct IanaTimeZoneId(string Value)
{
    public static IanaTimeZoneId FromConfiguration(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Calendar IANA timezone must not be empty.",
                nameof(value));
        }

        return new IanaTimeZoneId(value.Trim());
    }
}

/// <summary>
/// Authoritative calendar configuration: a single IANA timezone and
/// optional per-day-of-week working windows. A absent entry for a day
/// means that day is closed.
/// </summary>
public sealed record CalendarConfig
{
    public CalendarConfig(
        IanaTimeZoneId timezone,
        IReadOnlyDictionary<DayOfWeek, DaySchedule?> weeklySchedule)
    {
        Timezone = timezone;
        WeeklySchedule = weeklySchedule;
    }

    public IanaTimeZoneId Timezone { get; }

    public IReadOnlyDictionary<DayOfWeek, DaySchedule?> WeeklySchedule { get; }
}
