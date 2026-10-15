namespace CallBooking.Domain.Entities;

/// <summary>
/// A confirmed booking of one event-type slot. Holds the data the
/// storage layer retains for the lifetime of a successful creation.
/// </summary>
/// <remarks>
/// Storage invariants preserved here:
/// <list type="bullet">
/// <item>Server-assigned <see cref="Id"/> is opaque, stable and never reused.</item>
/// <item><see cref="EventTypeId"/> links the booking to an event type.</item>
/// <item><see cref="Start"/> and <see cref="End"/> are absolute <see cref="DateTimeOffset"/>
/// instants with offset preserved; they are not reinterpreted against any
/// ambient timezone.</item>
/// <item><see cref="GuestName"/> and <see cref="GuestEmail"/> are trimmed and
/// non-empty after trim. The trim is performed defensively here on top of
/// any normalising the application scenario has already applied, so the
/// stored entity never observes untrimmed values.</item>
/// </list>
/// Atomic creation, overlap and slot-grid validation do not live here.
/// They are guarded in higher layers per the approved implementation
/// decisions.
/// </remarks>
public sealed class Booking
{
    public string Id { get; }

    public string EventTypeId { get; }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public string GuestName { get; }

    public string GuestEmail { get; }

    public Booking(
        string id,
        string eventTypeId,
        DateTimeOffset start,
        DateTimeOffset end,
        string guestName,
        string guestEmail)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (eventTypeId is null)
        {
            throw new ArgumentNullException(nameof(eventTypeId));
        }

        if (guestName is null)
        {
            throw new ArgumentNullException(nameof(guestName));
        }

        if (guestEmail is null)
        {
            throw new ArgumentNullException(nameof(guestEmail));
        }

        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(end),
                end,
                "End must be strictly after Start.");
        }

        var trimmedName = guestName.Trim();
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException(
                "GuestName must not be blank after trim.",
                nameof(guestName));
        }

        var trimmedEmail = guestEmail.Trim();
        if (trimmedEmail.Length == 0)
        {
            throw new ArgumentException(
                "GuestEmail must not be blank after trim.",
                nameof(guestEmail));
        }

        Id = id;
        EventTypeId = eventTypeId;
        Start = start;
        End = end;
        GuestName = trimmedName;
        GuestEmail = trimmedEmail;
    }
}
