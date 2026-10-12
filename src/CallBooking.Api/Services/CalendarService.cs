using CallBooking.Api.Generated;
using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Services;

/// <summary>
/// Minimal proof-of-seam implementation for the generated
/// <see cref="ICalendarController"/> delegating interface. Contains no
/// business logic; responses are placeholders only.
/// </summary>
public sealed class CalendarService : ICalendarController
{
    public Task<ActionResult<CalendarInfo>> GetAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<CalendarInfo>>(new CalendarInfo
        {
            Timezone = "UTC"
        });
}