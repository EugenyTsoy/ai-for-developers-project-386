namespace CallBooking.Domain;

/// <summary>
/// Resolves a calendar's local working window into UTC opening/closing
/// instants for a given <see cref="LocalDate"/>. DST handling follows
/// the approved semantics:
/// <list type="bullet">
///   <item>Non-existent local boundaries &rarr; no working window that day.</item>
///   <item>Ambiguous open boundary &rarr; the earlier UTC instant.</item>
///   <item>Ambiguous close boundary &rarr; the later UTC instant.</item>
/// </list>
/// </summary>
public static class CalendarLocalTimeResolver
{
    private enum AmbiguityPreference
    {
        Earlier,
        Later,
    }

    /// <summary>
    /// Resolve the UTC opening/closing instants for the given local date.
    /// </summary>
    /// <param name="timezone">The calendar's IANA timezone, resolved up-front by the caller.</param>
    /// <param name="date">Local calendar date in the calendar's timezone.</param>
    /// <param name="schedule">Working window, or <c>null</c> for a day off.</param>
    /// <returns>
    /// <c>null</c> when the day has no working window (day off or any
    /// boundary time is non-existent due to DST); otherwise the resolved
    /// opening and closing UTC instants.
    /// </returns>
    public static (Instant Open, Instant Close)? ResolveWorkingWindow(
        TimeZoneInfo timezone,
        LocalDate date,
        DaySchedule? schedule)
    {
        if (schedule is null)
        {
            return null;
        }

        var openLocal = new DateTime(
            date.Year,
            date.Month,
            date.Day,
            schedule.Open.Hour,
            schedule.Open.Minute,
            0,
            DateTimeKind.Unspecified);

        var closeLocal = new DateTime(
            date.Year,
            date.Month,
            date.Day,
            schedule.Close.Hour,
            schedule.Close.Minute,
            0,
            DateTimeKind.Unspecified);

        var openOffset = ResolveOffset(timezone, openLocal, AmbiguityPreference.Earlier);
        var closeOffset = ResolveOffset(timezone, closeLocal, AmbiguityPreference.Later);

        if (openOffset is null || closeOffset is null)
        {
            return null;
        }

        return (
            new Instant(new DateTimeOffset(openLocal, openOffset.Value)),
            new Instant(new DateTimeOffset(closeLocal, closeOffset.Value)));
    }

    private static TimeSpan? ResolveOffset(
        TimeZoneInfo timezone,
        DateTime local,
        AmbiguityPreference preference)
    {
        if (timezone.IsInvalidTime(local))
        {
            return null;
        }

        if (!timezone.IsAmbiguousTime(local))
        {
            return timezone.GetUtcOffset(local);
        }

        var offsets = timezone.GetAmbiguousTimeOffsets(local);
        return preference == AmbiguityPreference.Earlier ? offsets[0] : offsets[offsets.Length - 1];
    }
}
