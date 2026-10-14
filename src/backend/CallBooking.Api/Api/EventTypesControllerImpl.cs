using CallBooking.Api.Generated;
using CallBooking.Application.Calendar;
using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Api;

/// <summary>
/// Handwritten API-boundary adapter behind the generated
/// <see cref="EventTypesController"/>. Implements the generated
/// <see cref="IEventTypesController"/> delegating interface. Contains no
/// business logic. Event type list/create remain placeholders for TK4.
/// </summary>
public sealed class EventTypesControllerImpl(ListAvailableSlots availableSlots) : IEventTypesController
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
    {
        cancellationToken.ThrowIfCancellationRequested();
        var slots = availableSlots.Execute(eventTypeId);
        if (slots is null)
        {
            var problem = new ObjectResult(new Generated.ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Event type not found."
            })
            {
                StatusCode = StatusCodes.Status404NotFound,
                ContentTypes = { "application/problem+json" }
            };
            return Task.FromResult<ActionResult<ICollection<Slot>>>(problem);
        }

        var response = slots.Select(slot => new Slot { Start = slot.Start, End = slot.End }).ToList();
        return Task.FromResult<ActionResult<ICollection<Slot>>>(new OkObjectResult(response));
    }
}
