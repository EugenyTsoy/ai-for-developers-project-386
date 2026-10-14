using CallBooking.Domain.Calendar;

namespace CallBooking.Api.Tests;

public class WorkingWeekTests
{
    [Theory]
    [InlineData(9, 0, 9, 0)]
    [InlineData(22, 0, 6, 0)]
    [InlineData(9, 0, 0, 0)]
    public void Working_window_rejects_empty_or_overnight_intervals(
        int openHour, int openMinute, int closeHour, int closeMinute)
    {
        Assert.Throws<ArgumentException>(() => new WorkingWindow(
            new TimeOnly(openHour, openMinute), new TimeOnly(closeHour, closeMinute)));
    }

    [Theory]
    [InlineData(0, 0, 23, 59)]
    [InlineData(8, 15, 12, 45)]
    public void Working_window_retains_valid_same_day_local_boundaries(
        int openHour, int openMinute, int closeHour, int closeMinute)
    {
        var window = new WorkingWindow(new TimeOnly(openHour, openMinute), new TimeOnly(closeHour, closeMinute));

        Assert.Equal(new TimeOnly(openHour, openMinute), window.Open);
        Assert.Equal(new TimeOnly(closeHour, closeMinute), window.Close);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Working_week_rejects_days_outside_the_seven_weekdays(int day)
    {
        var windows = new Dictionary<DayOfWeek, WorkingWindow>
        {
            [(DayOfWeek)day] = new WorkingWindow(new TimeOnly(9, 0), new TimeOnly(17, 0))
        };

        Assert.Throws<ArgumentException>(() => new CalendarConfiguration("Europe/Berlin", windows));
    }

    [Fact]
    public void Working_week_is_fixed_even_when_the_source_dictionary_changes()
    {
        var source = new Dictionary<DayOfWeek, WorkingWindow>
        {
            [DayOfWeek.Monday] = new WorkingWindow(new TimeOnly(8, 15), new TimeOnly(12, 45))
        };
        var calendar = new CalendarConfiguration("Europe/Berlin", source);

        source.Clear();
        source[DayOfWeek.Tuesday] = new WorkingWindow(new TimeOnly(14, 0), new TimeOnly(17, 30));

        Assert.Single(calendar.WorkingWeek);
        Assert.Equal(new TimeOnly(8, 15), calendar.WorkingWeek[DayOfWeek.Monday].Open);
        Assert.Equal(new TimeOnly(12, 45), calendar.WorkingWeek[DayOfWeek.Monday].Close);
        Assert.False(calendar.WorkingWeek.ContainsKey(DayOfWeek.Tuesday));
    }

    [Theory]
    [InlineData("Europe/Berlin")]
    [InlineData("America/New_York")]
    [InlineData("Etc/UTC")]
    [InlineData("UTC")]
    [InlineData("Asia/Kathmandu")]
    [InlineData("US/Eastern")]
    [InlineData("Etc/GMT+5")]
    public void Calendar_accepts_IANA_timezones_and_an_entirely_closed_week(string timezone)
    {
        var calendar = new CalendarConfiguration(timezone, new Dictionary<DayOfWeek, WorkingWindow>());

        Assert.Equal(timezone, calendar.Timezone);
        Assert.Empty(calendar.WorkingWeek);
    }

    [TzdataFact]
    public void Calendar_accepts_an_IANA_timezone_without_a_Windows_equivalent()
    {
        var calendar = new CalendarConfiguration("Antarctica/Troll", new Dictionary<DayOfWeek, WorkingWindow>());

        Assert.Equal("Antarctica/Troll", calendar.Timezone);
        Assert.Equal("Antarctica/Troll", calendar.TimeZone.Id);
    }

    [Theory]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Europe/Unknown")]
    [InlineData("")]
    [InlineData("Europe//Berlin")]
    [InlineData("posix/Europe/Berlin")]
    [InlineData("right/Europe/Berlin")]
    [InlineData("localtime")]
    [InlineData("posixrules")]
    public void Calendar_rejects_unknown_or_non_IANA_timezones(string timezone)
    {
        Assert.Throws<ArgumentException>(() => new CalendarConfiguration(
            timezone, new Dictionary<DayOfWeek, WorkingWindow>()));
    }

    private sealed class TzdataFactAttribute : FactAttribute
    {
        public TzdataFactAttribute()
        {
            if (OperatingSystem.IsWindows())
            {
                Skip = "Windows timezone data cannot resolve IANA zones without a Windows equivalent.";
            }
        }
    }
}
