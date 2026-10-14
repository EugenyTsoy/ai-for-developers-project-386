using CallBooking.Domain;

namespace CallBooking.Application.Ports;

/// <summary>
/// Outbound port for <see cref="EventType"/> storage. Concrete
/// implementations live in the Infrastructure layer; the application
/// only depends on this contract.
/// </summary>
public interface IEventTypeRepository
{
    /// <summary>
    /// List every event type the system has ever stored.
    /// </summary>
    Task<IReadOnlyList<EventType>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up an event type by its server-assigned identifier.
    /// </summary>
    Task<EventType?> FindAsync(EventTypeId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Append a new event type. The repository assigns the identifier,
    /// so the value carried in <paramref name="eventType"/> is replaced
    /// with the server-assigned id before being returned.
    /// </summary>
    Task<EventType> AddAsync(EventTypeDraft draft, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pre-persistence view of an event type: the caller-supplied name,
/// optional description, and duration. The repository is responsible
/// for assigning the id.
/// </summary>
public sealed record EventTypeDraft(string Name, string? Description, PositiveDuration DurationMinutes);
