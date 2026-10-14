using CallBooking.Application.Ports;
using CallBooking.Domain;
using CallBooking.Infrastructure.Storage;

namespace CallBooking.Infrastructure.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IEventTypeRepository"/>.
/// This is a deliberate placeholder for now: the persistence ticket
/// replaces it with the chosen real store.
/// </summary>
public sealed class InMemoryEventTypeRepository
    : IEventTypeRepository, IResettableStorage
{
    private readonly object _lock = new();
    private readonly List<EventType> _items = new();

    public Task<IReadOnlyList<EventType>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<EventType>>(_items.ToArray());
        }
    }

    public Task<EventType?> FindAsync(EventTypeId id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.Id == id));
        }
    }

    public Task<EventType> AddAsync(EventTypeDraft draft, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var id = EventTypeId.FromServer("evt_" + Guid.NewGuid().ToString("N"));
            var entity = new EventType(
                id,
                draft.Name,
                draft.Description,
                draft.DurationMinutes);
            _items.Add(entity);
            return Task.FromResult(entity);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }
}
