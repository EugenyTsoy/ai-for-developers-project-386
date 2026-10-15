namespace CallBooking.Domain.Calendar;

/// <summary>A half-open interval of absolute instants.</summary>
public sealed record TimeInterval
{
    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeInterval(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End must be strictly after start.");
        }

        Start = start;
        End = end;
    }

    public bool Overlaps(TimeInterval other) => Start < other.End && other.Start < End;

    public bool Contains(TimeInterval other) => Start <= other.Start && other.End <= End;
}
