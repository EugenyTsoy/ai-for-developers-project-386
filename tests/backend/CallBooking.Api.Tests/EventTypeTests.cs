using CallBooking.Domain.Entities;

namespace CallBooking.Api.Tests;

public class EventTypeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Name_is_trimmed_and_positive_duration_has_no_calendar_limit(int duration)
    {
        var type = new EventType("opaque-id", "  Call \t", null, duration);

        Assert.Equal("Call", type.Name);
        Assert.Equal(duration, type.DurationMinutes);
        Assert.Null(type.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    [InlineData(null)]
    public void Missing_or_blank_name_is_rejected(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new EventType("id", name!, null, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_duration_is_rejected(int duration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventType("id", "Call", null, duration));
    }
}
