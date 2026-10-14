using System.Globalization;
using CallBooking.Domain.Calendar;

namespace CallBooking.Api.Configuration;

internal static class CalendarConfigurationLoader
{
    public static CalendarConfiguration Load(IConfiguration configuration)
    {
        var calendar = configuration.GetRequiredSection("Calendar");
        var timezone = calendar["Timezone"]
            ?? throw new InvalidOperationException("Calendar:Timezone is required.");
        var workingWeek = new Dictionary<DayOfWeek, WorkingWindow>();

        foreach (var day in calendar.GetSection("WorkingWeek").GetChildren())
        {
            if (!Enum.TryParse<DayOfWeek>(day.Key, out var dayOfWeek) || !Enum.IsDefined(dayOfWeek))
            {
                throw new InvalidOperationException($"Unknown working-week day: {day.Path}.");
            }

            workingWeek.Add(dayOfWeek, new WorkingWindow(ReadTime(day, "Open"), ReadTime(day, "Close")));
        }

        return new CalendarConfiguration(timezone, workingWeek);
    }

    private static TimeOnly ReadTime(IConfigurationSection day, string boundary)
    {
        if (!TimeOnly.TryParse(day[boundary], CultureInfo.InvariantCulture, out var time))
        {
            throw new InvalidOperationException($"{day.Path}:{boundary} must be a local time.");
        }

        return time;
    }
}
