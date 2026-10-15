using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;

namespace CallBooking.Infrastructure.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IEventTypeRepository"/>.
/// Routes every call through the shared
/// <see cref="IInMemoryCalendarStore"/> so that callers operating
/// through the same host observe the same persisted state.
/// </summary>
public sealed class InMemoryEventTypeRepository : IEventTypeRepository
{
    private readonly IInMemoryCalendarStore _store;

    public InMemoryEventTypeRepository(IInMemoryCalendarStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Add(EventType eventType)
    {
        if (eventType is null)
        {
            throw new ArgumentNullException(nameof(eventType));
        }

        _store.AddEventType(eventType);
    }

    public EventType? GetById(string id)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        return _store.EventTypes.FirstOrDefault(e => e.Id == id);
    }

    public IReadOnlyList<EventType> List()
    {
        return _store.EventTypes.ToArray();
    }
}
