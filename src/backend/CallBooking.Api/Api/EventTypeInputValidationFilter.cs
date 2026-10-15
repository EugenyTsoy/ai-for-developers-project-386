using CallBooking.Api.Generated;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallBooking.Api.Api;

/// <summary>
/// Enforces binding errors and generated validation metadata before delegation.
/// The generated controller does not enable automatic ApiController validation.
/// </summary>
internal sealed class EventTypeInputValidationFilter : IActionFilter, IOrderedFilter
{
    // Before UnsupportedContentTypeFilter, so an absent required body is 400.
    public int Order => -3001;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionDescriptor.Parameters.Any(parameter => parameter.ParameterType == typeof(CreateEventTypeRequest))
            && !context.ModelState.IsValid)
        {
            context.Result = ApiProblems.Create(StatusCodes.Status400BadRequest, "Invalid event type request.");
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
