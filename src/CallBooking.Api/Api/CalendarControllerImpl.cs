using CallBooking.Api.Generated;
using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Api;

/// <summary>
/// Handwritten API-boundary adapter behind the generated
/// <see cref="CalendarController"/>. Implements the generated
/// <see cref="ICalendarController"/> delegating interface. Contains no
/// business logic; responses are placeholders only.
/// </summary>
public sealed class CalendarControllerImpl : ICalendarController
{
    public Task<ActionResult<CalendarInfo>> GetAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<CalendarInfo>>(new CalendarInfo
        {
            Timezone = "UTC"
        });
}