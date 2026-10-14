namespace CallBooking.Domain;

/// <summary>
/// A point on the timeline expressed as a UTC <see cref="DateTimeOffset"/>.
/// Domain code only deals with absolute UTC instants; presentation-layer
/// serialization (such as the wire string with a "Z" suffix) belongs to
/// the API boundary, never here.
/// </summary>
public readonly record struct Instant(DateTimeOffset Utc)
{
    /// <summary>
    /// Normalize any <see cref="DateTimeOffset"/> to a UTC instant.
    /// </summary>
    public static Instant FromUtc(DateTimeOffset value)
    {
        return new Instant(value.ToUniversalTime());
    }
}
