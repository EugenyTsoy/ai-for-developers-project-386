namespace CallBooking.Domain.Tests;

public class LocalTimeTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(24, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 60)]
    public void LocalTime_rejects_out_of_range_hour_or_minute(int hour, int minute)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalTime(hour, minute));
    }

    [Fact]
    public void Ordering_compares_hour_first_then_minute()
    {
        var nine = new LocalTime(9, 0);
        var nine_thirty = new LocalTime(9, 30);
        var ten = new LocalTime(10, 0);

        Assert.True(nine < nine_thirty);
        Assert.True(nine_thirty < ten);
        Assert.True(nine < ten);
    }

    [Fact]
    public void DaySchedule_rejects_inverted_or_equal_window()
    {
        var nine = new LocalTime(9, 0);
        var ten = new LocalTime(10, 0);

        Assert.Throws<ArgumentException>(() => new DaySchedule(ten, nine));
        Assert.Throws<ArgumentException>(() => new DaySchedule(nine, nine));
    }

    [Fact]
    public void DaySchedule_accepts_strictly_ordered_window()
    {
        var nine = new LocalTime(9, 0);
        var eighteen = new LocalTime(18, 0);

        var schedule = new DaySchedule(nine, eighteen);

        Assert.Equal(nine, schedule.Open);
        Assert.Equal(eighteen, schedule.Close);
    }
}
