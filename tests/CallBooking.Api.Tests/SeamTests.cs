using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CallBooking.Api.Tests;

public class SeamTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SeamTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // These assertions only prove that the generated controllers activate and
    // delegate to the handwritten interface implementations registered in DI.
    // They are intentionally not business-behaviour tests.

    [Fact]
    public async Task Calendar_Get_Delegates_To_Seam()
    {
        var response = await _client.GetAsync("/api/calendar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EventTypes_List_Delegates_To_Seam()
    {
        var response = await _client.GetAsync("/api/event-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bookings_Upcoming_Delegates_To_Seam()
    {
        var response = await _client.GetAsync("/api/bookings/upcoming");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
