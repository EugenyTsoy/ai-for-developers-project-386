using CallBooking.Api.Generated;
using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Services;

/// <summary>
/// Minimal proof-of-seam implementation for the generated
/// <see cref="IEventTypesController"/> delegating interface. Contains no
/// business logic; responses are placeholders only.
/// </summary>
public sealed class EventTypesService : IEventTypesController
{
    public Task<ActionResult<ICollection<EventType>>> ListAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<ICollection<EventType>>>(new List<EventType>());

    public Task<ActionResult<EventType>> CreateAsync(
        CreateEventTypeRequest body,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<EventType>>(new EventType
        {
            Id = "placeholder",
            Name = "placeholder",
            DurationMinutes = 30
        });

    public Task<ActionResult<ICollection<Slot>>> SlotsAsync(
        string eventTypeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<ICollection<Slot>>>(new List<Slot>());
}