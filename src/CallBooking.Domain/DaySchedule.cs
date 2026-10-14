namespace CallBooking.Domain;

/// <summary>
/// A single working window inside one local calendar day:
/// <c>open &lt; close</c>. Boundary times are wall-clock values in the
/// calendar's timezone. The resolved UTC opening/closing instants are
/// produced by <see cref="CalendarLocalTimeResolver"/>.
/// </summary>
public sealed record DaySchedule
{
    public DaySchedule(LocalTime open, LocalTime close)
    {
        if (!(open < close))
        {
            throw new ArgumentException(
                "Working window open must be strictly less than close.",
                nameof(close));
        }

        Open = open;
        Close = close;
    }

    public LocalTime Open { get; }

    public LocalTime Close { get; }
}
