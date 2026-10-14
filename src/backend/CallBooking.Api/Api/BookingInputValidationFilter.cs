using CallBooking.Api.Generated;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallBooking.Api.Api;

/// <summary>
/// Enforces generated binding/validation metadata before the generated POST
/// delegates. This controller is not an ApiController; its model-state errors
/// otherwise do not stop execution. No parallel request schema is maintained.
/// </summary>
internal sealed class BookingInputValidationFilter : IActionFilter, IOrderedFilter
{
    // Run before MVC's UnsupportedContentTypeFilter (-3000), including when
    // the required body is absent and therefore has no Content-Type header.
    public int Order => -3001;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionDescriptor.Parameters.Any(parameter => parameter.ParameterType == typeof(CreateBookingRequest))
            && !context.ModelState.IsValid)
        {
            context.Result = ApiProblems.Create(StatusCodes.Status400BadRequest, "Invalid booking request.");
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
