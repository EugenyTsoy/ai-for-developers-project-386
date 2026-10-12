using CallBooking.Api.Generated;
using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Services;

/// <summary>
/// Minimal proof-of-seam implementation for the generated
/// <see cref="IBookingsController"/> delegating interface. Contains no
/// business logic; responses are placeholders only.
/// </summary>
public sealed class BookingsService : IBookingsController
{
    public Task<ActionResult<BookingConfirmation>> CreateAsync(
        CreateBookingRequest body,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<BookingConfirmation>>(new BookingConfirmation
        {
            Start = DateTimeOffset.UnixEpoch,
            End = DateTimeOffset.UnixEpoch
        });

    public Task<ActionResult<ICollection<UpcomingBooking>>> UpcomingAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<ICollection<UpcomingBooking>>>(new List<UpcomingBooking>());
}