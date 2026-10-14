namespace CallBooking.Domain.Calendar;

/// <summary>
/// One local, same-day working interval. Resolving its boundaries to instants
/// (including DST rules) is separate from this weekly configuration.
/// </summary>
public sealed class WorkingWindow
{
    public TimeOnly Open { get; }

    public TimeOnly Close { get; }

    public WorkingWindow(TimeOnly open, TimeOnly close)
    {
        if (open >= close)
        {
            throw new ArgumentException("A working window must have local open < close.", nameof(close));
        }

        Open = open;
        Close = close;
    }
}
