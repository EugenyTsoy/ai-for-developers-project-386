using CallBooking.Api.Generated;
using CallBooking.Application.Calendar;
using CallBooking.Application.EventTypes;
using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Api;

/// <summary>
/// Handwritten API-boundary adapter behind the generated
/// <see cref="EventTypesController"/>. Implements the generated
/// <see cref="IEventTypesController"/> delegating interface. Contains no
/// business logic.
/// </summary>
public sealed class EventTypesControllerImpl(
    ListAvailableSlots availableSlots, CreateEventType createEventType, ListEventTypes listEventTypes) : IEventTypesController
{
    public Task<ActionResult<ICollection<EventType>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = listEventTypes.Execute().Select(Map).ToList();
        return Task.FromResult<ActionResult<ICollection<EventType>>>(new OkObjectResult(response));
    }

    public Task<ActionResult<EventType>> CreateAsync(
        CreateEventTypeRequest body,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var saved = createEventType.Execute(body.Name, body.Description, body.DurationMinutes);
        return Task.FromResult<ActionResult<EventType>>(saved is null
            ? ApiProblems.Create(StatusCodes.Status400BadRequest, "Invalid event type request.")
            : new ObjectResult(Map(saved)) { StatusCode = StatusCodes.Status201Created });
    }

    private static EventType Map(CallBooking.Domain.Entities.EventType saved) => new()
    {
        Id = saved.Id,
        Name = saved.Name,
        Description = saved.Description,
        DurationMinutes = saved.DurationMinutes
    };

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
