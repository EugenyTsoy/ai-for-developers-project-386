namespace CallBooking.Domain;

/// <summary>
/// A date in a calendar's local timezone. Carries year, month and day
/// without a timezone offset; this is a domain concept used as input
/// to <see cref="CalendarLocalTimeResolver"/>. The constructor
/// rejects structural impossibilities (e.g. February 30).
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

        var daysInMonth = DateTime.DaysInMonth(year, month);
        if (day < 1 || day > daysInMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(day),
                $"Day must be in [1, {daysInMonth}] for {year}-{month:D2}.");
        }

        Year = year;
        Month = month;
        Day = day;
    }

    public int Year { get; }

    public int Month { get; }

    public int Day { get; }
}
