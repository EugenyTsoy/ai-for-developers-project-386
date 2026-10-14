namespace CallBooking.Domain;

/// <summary>
/// A half-open interval <c>[start, end)</c> between two absolute UTC
/// instants. Domain intervals never wrap midnight: a working window
/// that would cross midnight is represented by a single interval whose
/// half-open semantics govern overlap checks.
/// </summary>
public readonly record struct TimeInterval
{
    public TimeInterval(Instant start, Instant end)
    {
        if (end.Utc <= start.Utc)
        {
            throw new ArgumentException(
                "Interval end must be strictly greater than start.",
                nameof(end));
        }

        Start = start;
        End = end;
    }

    public Instant Start { get; }

    public Instant End { get; }

    /// <summary>
    /// Two intervals overlap iff each starts strictly before the other
    /// ends. Identical starts conflict; touching boundaries do not.
    /// </summary>
    public bool Overlaps(TimeInterval other)
    {
        return Start.Utc < other.End.Utc && other.Start.Utc < End.Utc;
    }

    public TimeSpan Duration => End.Utc - Start.Utc;
}
