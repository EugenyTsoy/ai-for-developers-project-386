using CallBooking.Api.Generated;
using CallBooking.Application.Calendar;
using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Api;

/// <summary>
/// Handwritten API-boundary adapter behind the generated
/// <see cref="CalendarController"/>. Implements the generated
/// <see cref="ICalendarController"/> delegating interface. Contains no
/// business logic; maps the authoritative configuration to the transport DTO.
/// </summary>
public sealed class CalendarControllerImpl : ICalendarController
{
    private readonly CalendarContext _calendar;

    public CalendarControllerImpl(CalendarContext calendar)
    {
        _calendar = calendar;
    }

    public Task<ActionResult<CalendarInfo>> GetAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<ActionResult<CalendarInfo>>(new CalendarInfo
        {
            Timezone = _calendar.Configuration.Timezone
        });
}
