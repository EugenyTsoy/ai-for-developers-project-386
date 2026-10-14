using System.Collections.Frozen;

namespace CallBooking.Domain.Calendar;

/// <summary>
/// The single owner's configured calendar, independent of transport models.
/// </summary>
public sealed class CalendarConfiguration
{
    public string Timezone { get; }

    public TimeZoneInfo TimeZone { get; }

    /// <summary>Days absent from the dictionary have no working window.</summary>
    public IReadOnlyDictionary<DayOfWeek, WorkingWindow> WorkingWeek { get; }

    public CalendarConfiguration(string timezone, IReadOnlyDictionary<DayOfWeek, WorkingWindow> workingWeek)
    {
        ArgumentNullException.ThrowIfNull(timezone);
        ArgumentNullException.ThrowIfNull(workingWeek);
        foreach (var (day, window) in workingWeek)
        {
            if (!Enum.IsDefined(day) || window is null)
            {
                throw new ArgumentException("A working week must contain valid weekdays and windows.", nameof(workingWeek));
            }
        }

        if (!IsIanaIdentifier(timezone)
            || !TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out var timeZone)
            || !timeZone.HasIanaId)
        {
            throw new ArgumentException("An IANA timezone identifier is required.", nameof(timezone));
        }

        TimeZone = timeZone;
        Timezone = timezone;
        WorkingWeek = workingWeek.ToFrozenDictionary();
    }

    private static bool IsIanaIdentifier(string timezone)
    {
        // Unix lookups also accept paths into alternate tzdata trees and local
        // system files. Those are not IANA identifiers suitable for metadata.
        var components = timezone.Split('/');
        return timezone is not "localtime" and not "posixrules"
            && components[0] is not "posix" and not "right"
            && components.All(component => component.Length > 0
                && component is not "." and not ".."
                && component.All(character => char.IsAsciiLetterOrDigit(character)
                    || character is '_' or '-' or '+' or '.'));
    }
}
