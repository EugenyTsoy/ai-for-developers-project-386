namespace CallBooking.Domain.Tests;

public class CalendarLocalTimeResolverTests
{
    private static TimeZoneInfo FixedOffsetZone(string id, TimeSpan offset)
    {
        return TimeZoneInfo.CreateCustomTimeZone(
            id: id,
            baseUtcOffset: offset,
            displayName: id,
            standardDisplayName: id,
            daylightDisplayName: id,
            adjustmentRules: null,
            disableDaylightSavingTime: true);
    }

    private static TimeZoneInfo SpringForwardZone()
    {
        // Spring-forward at 02:00 local on March 8 every year of the rule;
        // local 02:30 on that date does not exist.
        var spring = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            month: 3,
            day: 8);

        var standard = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 3, 0, 0),
            month: 11,
            day: 1);

        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2025, 1, 1),
            new DateTime(2030, 12, 31),
            TimeSpan.FromHours(1),
            spring,
            standard);

        var rules = new[] { rule };

        return TimeZoneInfo.CreateCustomTimeZone(
            id: "SyntheticSpringForward",
            baseUtcOffset: TimeSpan.Zero,
            displayName: "SyntheticSpringForward",
            standardDisplayName: "Synthetic",
            daylightDisplayName: "Synthetic",
            adjustmentRules: rules,
            disableDaylightSavingTime: false);
    }

    [Fact]
    public void Returns_null_for_day_off()
    {
        var utc = TimeZoneInfo.Utc;
        var date = new LocalDate(2026, 1, 1);

        var window = CalendarLocalTimeResolver.ResolveWorkingWindow(utc, date, schedule: null);

        Assert.Null(window);
    }

    [Fact]
    public void Fixed_offset_zone_resolves_open_and_close_to_correct_utc()
    {
        var plusThree = FixedOffsetZone("FixedPlusThree", TimeSpan.FromHours(3));
        var date = new LocalDate(2026, 6, 15);
        var schedule = new DaySchedule(new LocalTime(9, 0), new LocalTime(18, 0));

        var window = CalendarLocalTimeResolver.ResolveWorkingWindow(plusThree, date, schedule);

        Assert.NotNull(window);
        var (open, close) = window!.Value;

        Assert.Equal(TimeSpan.FromHours(3), open.Utc.Offset);
        Assert.Equal(TimeSpan.FromHours(3), close.Utc.Offset);

        // 09:00 local in +3 == 06:00 UTC.
        Assert.Equal(new DateTime(2026, 6, 15, 6, 0, 0), open.Utc.UtcDateTime);
        // 18:00 local in +3 == 15:00 UTC.
        Assert.Equal(new DateTime(2026, 6, 15, 15, 0, 0), close.Utc.UtcDateTime);
    }

    [Fact]
    public void Spring_forward_skipped_open_is_treated_as_no_window()
    {
        var springForward = SpringForwardZone();
        var date = new LocalDate(2026, 3, 8);
        var schedule = new DaySchedule(new LocalTime(2, 30), new LocalTime(3, 30));

        var window = CalendarLocalTimeResolver.ResolveWorkingWindow(springForward, date, schedule);

        Assert.Null(window);
    }
}
