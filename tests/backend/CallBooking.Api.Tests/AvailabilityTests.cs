using CallBooking.Domain.Calendar;
using CallBooking.Domain.Entities;

namespace CallBooking.Api.Tests;

public class AvailabilityTests
{
    [Theory]
    [InlineData(30, new[] { "09:15", "09:45", "10:15" })]
    [InlineData(45, new[] { "09:15", "09:45" })]
    [InlineData(60, new[] { "09:15", "09:45" })]
    [InlineData(100, new string[] { })]
    [InlineData(int.MaxValue, new string[] { })]
    public void Fixed_thirty_minute_start_grid_uses_authoritative_duration_and_discards_incomplete_candidates(
        int duration, string[] expectedStarts)
    {
        var calendar = Calendar("Etc/UTC", DayOfWeek.Wednesday, "09:15", "10:50");

        var slots = Availability.GetSlotGrid(calendar, new DateOnly(2026, 10, 14), Type(duration));

        Assert.Equal(expectedStarts.Select(time => Instant($"2026-10-14T{time}:00Z")), slots.Select(slot => slot.Start));
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(duration), slot.End - slot.Start));
    }

    [Theory]
    [InlineData("02:15", "04:00")]
    [InlineData("01:00", "02:45")]
    public void Nonexistent_working_boundary_closes_the_date_without_shifting(string open, string close)
    {
        var calendar = Calendar("Europe/Berlin", DayOfWeek.Sunday, open, close);

        Assert.Empty(Availability.GetSlotGrid(calendar, new DateOnly(2026, 3, 29), Type(30)));
        Assert.NotEmpty(Availability.GetSlotGrid(calendar, new DateOnly(2026, 4, 5), Type(30)));
    }

    [Fact]
    public void Spring_forward_grid_and_ends_follow_elapsed_minutes()
    {
        var calendar = Calendar("Europe/Berlin", DayOfWeek.Sunday, "00:00", "05:00");

        var slots = Availability.GetSlotGrid(calendar, new DateOnly(2026, 3, 29), Type(45));

        Assert.Equal(new[]
        {
            "2026-03-28T23:00:00Z", "2026-03-28T23:30:00Z", "2026-03-29T00:00:00Z",
            "2026-03-29T00:30:00Z", "2026-03-29T01:00:00Z", "2026-03-29T01:30:00Z", "2026-03-29T02:00:00Z"
        }.Select(Instant), slots.Select(slot => slot.Start));
        Assert.Equal(new[] { "00:00", "00:30", "01:00", "01:30", "03:00", "03:30", "04:00" },
            slots.Select(slot => TimeZoneInfo.ConvertTime(slot.Start, calendar.TimeZone).ToString("HH:mm")));
        Assert.Equal(Instant("2026-03-29T01:15:00Z"), slots[3].End);
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(45), slot.End - slot.Start));
    }

    [Theory]
    [InlineData("02:00", "03:00", 60, "2026-10-25T00:00:00Z", "2026-10-25T02:00:00Z", 3)]
    [InlineData("01:00", "02:30", 30, "2026-10-24T23:00:00Z", "2026-10-25T01:30:00Z", 5)]
    [InlineData("02:00", "02:30", 30, "2026-10-25T00:00:00Z", "2026-10-25T01:30:00Z", 3)]
    public void Ambiguous_open_is_earliest_and_ambiguous_close_is_latest(
        string open, string close, int duration, string first, string lastEnd, int count)
    {
        var calendar = Calendar("Europe/Berlin", DayOfWeek.Sunday, open, close);

        var slots = Availability.GetSlotGrid(calendar, new DateOnly(2026, 10, 25), Type(duration));

        Assert.Equal(count, slots.Count);
        Assert.Equal(Instant(first), slots[0].Start);
        Assert.Equal(Instant(lastEnd), slots[^1].End);
    }

    [Fact]
    public void Repeated_labels_are_distinct_instants_only_on_the_single_canonical_grid()
    {
        var calendar = Calendar("Europe/Berlin", DayOfWeek.Sunday, "00:00", "05:00");
        var date = new DateOnly(2026, 10, 25);

        var hourly = Availability.GetSlotGrid(calendar, date, Type(60));
        var repeated = hourly.Select(slot => TimeZoneInfo.ConvertTime(slot.Start, calendar.TimeZone))
            .Where(start => start.Hour == 2).ToArray();
        Assert.Equal(new[] { TimeSpan.FromHours(2), TimeSpan.FromHours(2), TimeSpan.FromHours(1), TimeSpan.FromHours(1) },
            repeated.Select(start => start.Offset));
        Assert.Equal(hourly[4].End, hourly[6].Start);

        var fortyFive = Availability.GetSlotGrid(calendar, date, Type(45));
        Assert.Equal(hourly.Select(slot => slot.Start), fortyFive.Select(slot => slot.Start));
        Assert.Equal(11, fortyFive.Count);
        Assert.All(fortyFive, slot => Assert.Equal(TimeSpan.FromMinutes(45), slot.End - slot.Start));
        Assert.DoesNotContain(fortyFive, slot => slot.Start == Instant("2026-10-25T01:15:00Z"));
    }

    [Theory]
    [InlineData("Europe/Berlin", "2026-03-28T12:00:00Z", "2026-03-27T23:00:00Z", "2026-04-10T22:00:00Z", 335)]
    [InlineData("Europe/Berlin", "2026-10-24T12:00:00Z", "2026-10-23T22:00:00Z", "2026-11-06T23:00:00Z", 337)]
    [InlineData("Asia/Tokyo", "2026-10-13T16:00:00Z", "2026-10-13T15:00:00Z", "2026-10-27T15:00:00Z", 336)]
    [InlineData("America/Santiago", "2026-09-06T12:00:00Z", "2026-09-06T04:00:00Z", "2026-09-20T03:00:00Z", 335)]
    [InlineData("America/Santiago", "2026-08-23T12:00:00Z", "2026-08-23T04:00:00Z", "2026-09-06T04:00:00Z", 336)]
    [InlineData("America/Havana", "2026-11-01T12:00:00Z", "2026-11-01T04:00:00Z", "2026-11-15T05:00:00Z", 337)]
    public void Booking_window_uses_calendar_dates_and_first_valid_boundary_instants(
        string timezone, string now, string start, string end, int hours)
    {
        var calendar = new CalendarConfiguration(timezone, new Dictionary<DayOfWeek, WorkingWindow>());

        var window = Availability.GetBookingWindow(calendar, Instant(now));

        Assert.Equal(Instant(start), window.Start);
        Assert.Equal(Instant(end), window.End);
        Assert.Equal(TimeSpan.FromHours(hours), window.End - window.Start);
    }

    [Theory]
    [InlineData("09:40", new[] { "09:45", "10:15" })]
    [InlineData("09:45", new[] { "10:15" })]
    public void Today_is_included_now_is_strict_and_grid_does_not_move(string now, string[] todayStarts)
    {
        var calendar = Calendar("Etc/UTC", DayOfWeek.Wednesday, "09:15", "10:50");

        var slots = Availability.List(calendar, Type(30), Instant($"2026-10-14T{now}:00Z"), []);

        var expected = todayStarts.Select(time => Instant($"2026-10-14T{time}:00Z"))
            .Concat(new[] { "09:15", "09:45", "10:15" }.Select(time => Instant($"2026-10-21T{time}:00Z")));
        Assert.Equal(expected, slots.Select(slot => slot.Start));
        Assert.DoesNotContain(slots, slot => slot.Start >= Instant("2026-10-28T00:00:00Z"));
    }

    [Fact]
    public void Saved_bookings_of_other_types_remove_overlapping_but_not_adjacent_slots()
    {
        var calendar = Calendar("Etc/UTC", DayOfWeek.Wednesday, "09:00", "12:00");
        var booking = new Booking("booking", "other-type", Instant("2026-10-14T10:00:00Z"),
            Instant("2026-10-14T11:00:00Z"), "Guest", "guest@example.com");

        var slots = Availability.List(calendar, Type(30), Instant("2026-10-14T08:00:00Z"), [booking]);

        Assert.Equal(new[] { "09:00", "09:30", "11:00", "11:30" }.Select(time => Instant($"2026-10-14T{time}:00Z")),
            slots.Where(slot => slot.Start.Date == new DateTime(2026, 10, 14)).Select(slot => slot.Start));
    }

    [Fact]
    public void Duration_must_fit_real_working_length_not_wall_clock_labels()
    {
        var calendar = Calendar("Europe/Berlin", DayOfWeek.Sunday, "01:30", "03:30");

        Assert.Empty(Availability.GetSlotGrid(calendar, new DateOnly(2026, 3, 29), Type(90)));
        var exactFit = Assert.Single(Availability.GetSlotGrid(calendar, new DateOnly(2026, 3, 29), Type(60)));
        Assert.Equal(Instant("2026-03-29T00:30:00Z"), exactFit.Start);
        Assert.Equal(Instant("2026-03-29T01:30:00Z"), exactFit.End);
    }

    [Fact]
    public void Booking_one_repeated_label_does_not_block_its_adjacent_distinct_instant()
    {
        var calendar = Calendar("Europe/Berlin", DayOfWeek.Sunday, "02:00", "03:00");
        var booking = new Booking("existing", "other-type", Instant("2026-10-25T02:00:00+02:00"),
            Instant("2026-10-25T02:00:00+01:00"), "Guest", "guest@example.com");

        var slots = Availability.List(calendar, Type(60), Instant("2026-10-24T21:00:00Z"), [booking]);

        Assert.DoesNotContain(slots, slot => slot.Start == Instant("2026-10-25T00:00:00Z"));
        Assert.Contains(slots, slot => slot.Start == Instant("2026-10-25T01:00:00Z"));
    }

    private static CalendarConfiguration Calendar(string timezone, DayOfWeek day, string open, string close) =>
        new(timezone, new Dictionary<DayOfWeek, WorkingWindow>
        {
            [day] = new(TimeOnly.Parse(open), TimeOnly.Parse(close))
        });

    private static EventType Type(int duration) => new("type", "Call", null, duration);

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value);
}
