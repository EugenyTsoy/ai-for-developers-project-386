using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;

namespace CallBooking.Application.EventTypes;

public sealed class CreateEventType(IEventTypeRepository eventTypes)
{
    public EventType? Execute(string name, string? description, int durationMinutes)
    {
        if (string.IsNullOrWhiteSpace(name) || durationMinutes < 1)
        {
            return null;
        }

        var saved = new EventType(Guid.NewGuid().ToString("N"), name, description, durationMinutes);
        eventTypes.Add(saved);
        return saved;
    }
}
