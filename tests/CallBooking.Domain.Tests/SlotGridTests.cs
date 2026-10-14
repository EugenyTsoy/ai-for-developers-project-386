namespace CallBooking.Domain.Tests;

public class SlotGridTests
{
    private static Instant At(string utc) =>
        Instant.FromUtc(DateTimeOffset.Parse(utc));

    [Fact]
    public void Generate_opening_instants_anchors_on_opening_not_now()
    {
        // 09:00–18:00 with 30-min step → 18 starts.
        var opening = At("2026-01-01T09:00:00Z");
        var closing = At("2026-01-01T18:00:00Z");

        var grid = SlotGrid.GenerateOpeningInstants(opening, closing, 30);

        Assert.Equal(18, grid.Count);
        Assert.Equal(opening.Utc, grid[0].Utc);
        Assert.Equal(opening.Utc.AddMinutes(17 * 30), grid[17].Utc);
    }

    [Fact]
    public void Generate_drops_trailing_partial_slot()
    {
        // 09:00–18:35 → only full 30-min slots fit (35-minute remainder dropped).
        var opening = At("2026-01-01T09:00:00Z");
        var closing = At("2026-01-01T18:35:00Z");

        var grid = SlotGrid.GenerateOpeningInstants(opening, closing, 30);

        // 09:30, 10:00 ... 18:00 = 19 full slots fit, 18:30 would not end by 18:35.
        Assert.Equal(19, grid.Count);
    }

    [Fact]
    public void Generate_returns_empty_when_duration_is_strictly_greater_than_window()
    {
        // 09:00–13:00 (4 hours = 240 min). With a 250-min duration,
        // the first slot would not fit, so the grid must be empty.
        var opening = At("2026-01-01T09:00:00Z");
        var closing = At("2026-01-01T13:00:00Z");

        var grid = SlotGrid.GenerateOpeningInstants(opening, closing, 250);

        Assert.Empty(grid);
    }

    [Fact]
    public void Generate_rejects_non_positive_step()
    {
        var opening = At("2026-01-01T09:00:00Z");
        var closing = At("2026-01-01T18:00:00Z");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SlotGrid.GenerateOpeningInstants(opening, closing, 0));
    }
}
