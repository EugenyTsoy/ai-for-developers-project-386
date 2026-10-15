using Microsoft.AspNetCore.Mvc;

namespace CallBooking.Api.Api;

internal static class ApiProblems
{
    public static ObjectResult Create(int status, string title) => new(new Generated.ProblemDetails
    {
        Status = status,
        Title = title
    })
    {
        StatusCode = status,
        ContentTypes = { "application/problem+json" }
    };
}
