namespace CallBooking.Domain;

/// <summary>
/// A persisted booking. The interval is derived from the chosen
/// <see cref="EventType"/> duration at creation time and lives as
/// absolute UTC instants committed to storage.
/// </summary>
public sealed record Booking
{
    public Booking(
        BookingId id,
        EventTypeId eventTypeId,
        TimeInterval interval,
        GuestContact guest)
    {
        Id = id;
        EventTypeId = eventTypeId;
        Interval = interval;
        Guest = guest;
    }

    public BookingId Id { get; }

    public EventTypeId EventTypeId { get; }

    public TimeInterval Interval { get; }

    public GuestContact Guest { get; }
}
