using Microsoft.AspNetCore.Mvc.Testing;

namespace CallBooking.Api.Tests;

public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Api_Starts_And_Responds()
    {
        var response = await _client.GetAsync("/WeatherForecast");

        response.EnsureSuccessStatusCode();
    }
}
