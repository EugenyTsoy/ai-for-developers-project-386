namespace CallBooking.Domain.Tests;

public class TimeIntervalTests
{
    private static Instant At(string utc) =>
        Instant.FromUtc(DateTimeOffset.Parse(utc));

    [Fact]
    public void Constructor_rejects_equal_or_inverted_endpoints()
    {
        var start = At("2026-01-01T09:00:00Z");
        var end = At("2026-01-01T09:00:00Z");

        Assert.Throws<ArgumentException>(() => new TimeInterval(start, end));
    }

    [Fact]
    public void Overlaps_rejects_touching_intervals()
    {
        var a = new TimeInterval(At("2026-01-01T09:00:00Z"), At("2026-01-01T10:00:00Z"));
        var b = new TimeInterval(At("2026-01-01T10:00:00Z"), At("2026-01-01T11:00:00Z"));

        Assert.False(a.Overlaps(b));
        Assert.False(b.Overlaps(a));
    }

    [Fact]
    public void Overlaps_conflicts_on_identical_starts()
    {
        var a = new TimeInterval(At("2026-01-01T09:00:00Z"), At("2026-01-01T10:30:00Z"));
        var b = new TimeInterval(At("2026-01-01T09:00:00Z"), At("2026-01-01T09:45:00Z"));

        Assert.True(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_conflicts_when_chain_crosses_midnight()
    {
        var before = new TimeInterval(At("2026-01-01T23:30:00Z"), At("2026-01-02T00:30:00Z"));
        var after = new TimeInterval(At("2026-01-02T00:00:00Z"), At("2026-01-02T01:00:00Z"));

        Assert.True(before.Overlaps(after));
    }

    [Fact]
    public void Overlaps_returns_true_for_interior_overlap()
    {
        var outer = new TimeInterval(At("2026-01-01T09:00:00Z"), At("2026-01-01T11:00:00Z"));
        var inside = new TimeInterval(At("2026-01-01T10:00:00Z"), At("2026-01-01T10:30:00Z"));

        Assert.True(outer.Overlaps(inside));
        Assert.True(inside.Overlaps(outer));
    }
}
