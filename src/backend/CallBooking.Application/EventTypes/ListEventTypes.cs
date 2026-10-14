using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;

namespace CallBooking.Application.EventTypes;

public sealed class ListEventTypes(IEventTypeRepository eventTypes)
{
    public IReadOnlyList<EventType> Execute() => eventTypes.List();
}
