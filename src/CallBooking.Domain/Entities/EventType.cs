namespace CallBooking.Domain.Entities;

/// <summary>
/// An event type that guests can book. Holds the data the storage
/// layer retains for the lifetime of a successful creation.
/// </summary>
/// <remarks>
/// Storage invariants preserved here:
/// <list type="bullet">
/// <item>Server-assigned <see cref="Id"/> is opaque, stable and never reused.</item>
/// <item><see cref="Name"/> is required and non-empty after trim.</item>
/// <item><see cref="Description"/> is optional and, when present, trimmed.</item>
/// <item><see cref="DurationMinutes"/> is a positive integer.</item>
/// <item>Storage does not enforce uniqueness on <see cref="Name"/>.</item>
/// </list>
/// Stronger business validation (regex/length checks, slot grid,
/// atomic creation) lives in higher layers per the approved
/// implementation decisions; this entity only guarantees the
/// shape the storage layer is required to preserve.
/// </remarks>
public sealed class EventType
{
    public string Id { get; }

    public string Name { get; }

    public string? Description { get; }

    public int DurationMinutes { get; }

    public EventType(
        string id,
        string name,
        string? description,
        int durationMinutes)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (durationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                durationMinutes,
                "DurationMinutes must be a positive integer.");
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException(
                "Name must not be blank after trim.",
                nameof(name));
        }

        Id = id;
        Name = trimmedName;
        Description = description is null ? null : description.Trim();
        DurationMinutes = durationMinutes;
    }
}
