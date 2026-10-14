namespace CallBooking.Domain;

/// <summary>
/// Wall-clock time of day with the hour and a single minute.
/// Domain-local: this type never carries timezone information; the
/// calendar resolves a <see cref="LocalTime"/> combined with a date in
/// a calendar timezone into an absolute UTC instant.
/// </summary>
public readonly record struct LocalTime
{
    public LocalTime(int hour, int minute)
    {
        if (hour is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hour),
                "Hour must be in [0, 23].");
        }

        if (minute is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minute),
                "Minute must be in [0, 59].");
        }

        Hour = hour;
        Minute = minute;
    }

    public int Hour { get; }

    public int Minute { get; }

    public static bool operator <(LocalTime left, LocalTime right)
    {
        return left.Hour < right.Hour
            || (left.Hour == right.Hour && left.Minute < right.Minute);
    }

    public static bool operator >(LocalTime left, LocalTime right)
    {
        return right < left;
    }

    public static bool operator <=(LocalTime left, LocalTime right)
    {
        return left < right || left == right;
    }

    public static bool operator >=(LocalTime left, LocalTime right)
    {
        return right < left || left == right;
    }
}
