using CallBooking.Domain.Calendar;

namespace CallBooking.Api.Tests;

public class TimeIntervalTests
{
    [Theory]
    [InlineData(0, 30, true)]
    [InlineData(15, 45, true)]
    [InlineData(-15, 15, true)]
    [InlineData(-15, 45, true)]
    [InlineData(30, 60, false)]
    [InlineData(-30, 0, false)]
    public void Half_open_intervals_overlap_but_adjacency_is_allowed(int start, int end, bool overlaps)
    {
        var origin = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
        var slot = new TimeInterval(origin, origin.AddMinutes(30));
        var other = new TimeInterval(origin.AddMinutes(start), origin.AddMinutes(end));

        Assert.Equal(overlaps, slot.Overlaps(other));
        Assert.Equal(overlaps, other.Overlaps(slot));
    }

    [Theory]
    [InlineData(0, 30, true)]
    [InlineData(15, 30, true)]
    [InlineData(-1, 30, false)]
    [InlineData(0, 31, false)]
    [InlineData(30, 31, false)]
    public void Containment_checks_both_boundaries_and_allows_end_at_upper_boundary(int start, int end, bool contained)
    {
        var origin = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
        var window = new TimeInterval(origin, origin.AddMinutes(30));

        Assert.Equal(contained, window.Contains(new TimeInterval(origin.AddMinutes(start), origin.AddMinutes(end))));
    }

    [Fact]
    public void Midnight_and_different_offsets_do_not_split_or_reinterpret_intervals()
    {
        var crossing = new TimeInterval(DateTimeOffset.Parse("2026-10-14T23:45:00Z"), DateTimeOffset.Parse("2026-10-15T00:15:00Z"));
        var overlap = new TimeInterval(DateTimeOffset.Parse("2026-10-15T02:00:00+02:00"), DateTimeOffset.Parse("2026-10-15T02:30:00+02:00"));
        var adjacent = new TimeInterval(DateTimeOffset.Parse("2026-10-15T00:15:00Z"), DateTimeOffset.Parse("2026-10-15T00:45:00Z"));

        Assert.True(crossing.Overlaps(overlap));
        Assert.False(crossing.Overlaps(adjacent));
    }
}
