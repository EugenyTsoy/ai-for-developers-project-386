namespace CallBooking.Domain.Tests;

public class InstantTests
{
    [Fact]
    public void FromUtc_normalises_non_utc_offset()
    {
        var plusOne = new DateTimeOffset(
            new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified),
            TimeSpan.FromHours(1));

        var instant = Instant.FromUtc(plusOne);

        Assert.Equal(TimeSpan.Zero, instant.Utc.Offset);
        Assert.Equal(9, instant.Utc.Hour);
    }

    [Fact]
    public void Two_instants_with_same_utc_value_are_equal()
    {
        var a = Instant.FromUtc(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var b = Instant.FromUtc(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(a, b);
    }
}
