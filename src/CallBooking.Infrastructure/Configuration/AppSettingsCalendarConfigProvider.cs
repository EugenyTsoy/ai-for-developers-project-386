using CallBooking.Application.Ports;
using CallBooking.Domain;
using Microsoft.Extensions.Configuration;

namespace CallBooking.Infrastructure.Configuration;

/// <summary>
/// Reads the authoritative calendar configuration from application
/// configuration. The configuration must include a <c>Calendar</c>
/// section with <c>Timezone</c> and an optional <c>WeeklySchedule</c>
/// mapping from day-of-week names to <c>HH:mm-HH:mm</c> strings or
/// <c>null</c> for a closed day.
/// </summary>
public sealed class AppSettingsCalendarConfigProvider : ICalendarConfigProvider
{
    private readonly Lazy<CalendarConfig> _config;
    private readonly Lazy<TimeZoneInfo> _timezone;

    public AppSettingsCalendarConfigProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _config = new Lazy<CalendarConfig>(() => ReadConfig(configuration));
        _timezone = new Lazy<TimeZoneInfo>(() =>
            TimeZoneInfo.FindSystemTimeZoneById(_config.Value.Timezone.Value));
    }

    public CalendarConfig GetConfig() => _config.Value;

    public (Instant Open, Instant Close)? GetWorkingWindow(LocalDate date)
    {
        var config = _config.Value;
        config.WeeklySchedule.TryGetValue(LookupDayOfWeek(date), out var schedule);
        return CalendarLocalTimeResolver.ResolveWorkingWindow(_timezone.Value, date, schedule);
    }

    private static DayOfWeek LookupDayOfWeek(LocalDate date)
    {
        var probe = new DateTime(date.Year, date.Month, date.Day);
        return probe.DayOfWeek;
    }

    private static CalendarConfig ReadConfig(IConfiguration configuration)
    {
        var section = configuration.GetSection("Calendar");
        var timezoneId = IanaTimeZoneId.FromConfiguration(
            section["Timezone"]
                ?? throw new InvalidOperationException(
                    "Missing required configuration value 'Calendar:Timezone'."));

        var weekly = new Dictionary<DayOfWeek, DaySchedule?>();
        var scheduleSection = section.GetSection("WeeklySchedule");
        foreach (var dayName in Enum.GetNames<DayOfWeek>())
        {
            var daySection = scheduleSection.GetSection(dayName);
            if (!daySection.Exists())
            {
                continue;
            }

            var raw = daySection.Value;
            if (string.IsNullOrEmpty(raw))
            {
                weekly[Enum.Parse<DayOfWeek>(dayName)] = null;
                continue;
            }

            var (open, close) = ParseRange(raw);
            weekly[Enum.Parse<DayOfWeek>(dayName)] = new DaySchedule(open, close);
        }

        return new CalendarConfig(timezoneId, weekly);
    }

    private static (LocalTime Open, LocalTime Close) ParseRange(string raw)
    {
        var parts = raw.Split('-');
        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                $"Calendar weekly schedule range '{raw}' must be of the form 'HH:mm-HH:mm'.");
        }

        return (ParseTime(parts[0], raw), ParseTime(parts[1], raw));
    }

    private static LocalTime ParseTime(string input, string raw)
    {
        var parts = input.Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var hour)
            || !int.TryParse(parts[1], out var minute))
        {
            throw new InvalidOperationException(
                $"Calendar weekly schedule range '{raw}' must be of the form 'HH:mm-HH:mm'.");
        }

        return new LocalTime(hour, minute);
    }
}
