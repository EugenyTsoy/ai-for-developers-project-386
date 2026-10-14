using CallBooking.Api.Generated;
using CallBooking.Application.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Api;

/// <summary>
/// Handwritten API-boundary adapter behind the generated
/// <see cref="BookingsController"/>. Implements the generated
/// <see cref="IBookingsController"/> delegating interface. Contains no
/// business logic. Upcoming remains a placeholder for its own ticket.
/// </summary>
public sealed class BookingsControllerImpl(CreateBooking createBooking) : IBookingsController
{
    public Task<ActionResult<BookingConfirmation>> CreateAsync(
        CreateBookingRequest body,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = createBooking.Execute(body.EventTypeId, body.Start, body.Name, body.Email);
        if (result.Outcome == BookingCreationOutcome.Created)
        {
            return Task.FromResult<ActionResult<BookingConfirmation>>(new ObjectResult(new BookingConfirmation
            {
                Start = result.Booking!.Start,
                End = result.Booking.End
            })
            { StatusCode = StatusCodes.Status201Created });
        }

        var (status, title) = result.Outcome switch
        {
            BookingCreationOutcome.UnknownEventType => (StatusCodes.Status404NotFound, "Event type not found."),
            BookingCreationOutcome.Unavailable => (StatusCodes.Status409Conflict, "Slot is no longer available."),
            _ => (StatusCodes.Status400BadRequest, "Invalid booking request.")
        };
        return Task.FromResult<ActionResult<BookingConfirmation>>(ApiProblems.Create(status, title));
    }

    public Task<ActionResult<ICollection<UpcomingBooking>>> UpcomingAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<ICollection<UpcomingBooking>>>(new List<UpcomingBooking>());
}
