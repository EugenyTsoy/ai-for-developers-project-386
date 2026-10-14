namespace CallBooking.Domain;

/// <summary>
/// A date in a calendar's local timezone. Carries year, month and day
/// without a timezone offset; this is a domain concept used as input
/// to <see cref="CalendarLocalTimeResolver"/>.
/// </summary>
public readonly record struct LocalDate
{
    public LocalDate(int year, int month, int day)
    {
        if (year is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                "Year must be in [1, 9999].");
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(month),
                "Month must be in [1, 12].");
        }

        if (day is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(day),
                "Day must be in [1, 31].");
        }

        Year = year;
        Month = month;
        Day = day;
    }

    public int Year { get; }

    public int Month { get; }

    public int Day { get; }
}
