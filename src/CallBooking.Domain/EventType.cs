namespace CallBooking.Domain;

/// <summary>
/// Server-assigned stable opaque string identifier for an <see cref="EventType"/>.
/// </summary>
public readonly record struct EventTypeId(string Value)
{
    public static EventTypeId FromServer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Event type id must not be empty or whitespace.",
                nameof(value));
        }

        return new EventTypeId(value.Trim());
    }
}

/// <summary>
/// Server-assigned stable opaque string identifier for a <see cref="Booking"/>.
/// </summary>
public readonly record struct BookingId(string Value)
{
    public static BookingId FromServer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Booking id must not be empty or whitespace.",
                nameof(value));
        }

        return new BookingId(value.Trim());
    }
}

/// <summary>
/// Duration in minutes. Must be strictly positive.
/// </summary>
public readonly record struct PositiveDuration
{
    public PositiveDuration(int minutes)
    {
        if (minutes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutes),
                "Duration must be at least one minute.");
        }

        Minutes = minutes;
    }

    public int Minutes { get; }

    public TimeSpan AsTimeSpan() => TimeSpan.FromMinutes(Minutes);
}

/// <summary>
/// An event type that guests can book. The server assigns the opaque
/// <see cref="Id"/>. <see cref="Name"/> is non-empty after trim.
/// <see cref="Description"/> is optional and is omitted by the boundary
/// when no value was supplied.
/// </summary>
public sealed record EventType
{
    public EventType(
        EventTypeId id,
        string name,
        string? description,
        PositiveDuration durationMinutes)
    {
        Id = id;
        Name = name;
        Description = description;
        DurationMinutes = durationMinutes;
    }

    public EventTypeId Id { get; }

    public string Name { get; }

    public string? Description { get; }

    public PositiveDuration DurationMinutes { get; }
}
