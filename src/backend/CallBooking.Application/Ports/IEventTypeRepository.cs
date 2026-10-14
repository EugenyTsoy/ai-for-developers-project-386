using CallBooking.Domain.Entities;

namespace CallBooking.Application.Ports;

/// <summary>
/// Hand-written outbound port for event-type persistence. Application
/// services depend on this abstraction; the infrastructure layer
/// owns the concrete implementation. The interface does not expose
/// generated transport DTOs, HTTP primitives or any
/// ASP.NET-specific type.
/// </summary>
public interface IEventTypeRepository
{
    /// <summary>
    /// Persists a freshly-created event type. The repository does not
    /// enforce uniqueness on <see cref="EventType.Name"/>; same-named
    /// types may coexist with distinct stable ids.
    /// </summary>
    void Add(EventType eventType);

    /// <summary>
    /// Returns the event type with the given id, or <c>null</c> when
    /// no such event type has been persisted.
    /// </summary>
    EventType? GetById(string id);

    /// <summary>
    /// Returns every persisted event type, including types that have
    /// no available slots. Order is implementation-defined.
    /// </summary>
    IReadOnlyList<EventType> List();
}
