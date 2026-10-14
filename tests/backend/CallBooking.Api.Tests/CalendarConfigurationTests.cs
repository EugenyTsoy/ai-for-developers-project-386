using System.Net;
using System.Text.Json;
using CallBooking.Application.Calendar;
using CallBooking.Domain.Calendar;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CallBooking.Api.Tests;

public class CalendarConfigurationTests
{
    [Theory]
    [InlineData("Europe/Berlin")]
    [InlineData("Asia/Tokyo")]
    public async Task Calendar_metadata_returns_only_the_configured_IANA_timezone(string timezone)
    {
        using var factory = CreateHost(new Dictionary<string, string?>
        {
            ["Calendar:Timezone"] = timezone
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/calendar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var property = Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal("timezone", property.Name);
        Assert.Equal(JsonValueKind.String, property.Value.ValueKind);
        Assert.Equal(timezone, property.Value.GetString());
    }

    [Fact]
    public void Test_host_schedule_uses_configured_local_windows_and_closed_days()
    {
        using var factory = CreateHost(new Dictionary<string, string?>
        {
            ["Calendar:Timezone"] = "Europe/Berlin",
            ["Calendar:WorkingWeek:Monday:Open"] = "08:15",
            ["Calendar:WorkingWeek:Monday:Close"] = "12:45",
            ["Calendar:WorkingWeek:Sunday:Open"] = "14:00",
            ["Calendar:WorkingWeek:Sunday:Close"] = "17:30"
        });

        var calendar = factory.Services.GetRequiredService<CalendarConfiguration>();

        Assert.Equal("Europe/Berlin", calendar.Timezone);
        Assert.Equal("Europe/Berlin", calendar.TimeZone.Id);
        Assert.Equal(2, calendar.WorkingWeek.Count);
        Assert.Equal(new TimeOnly(8, 15), calendar.WorkingWeek[DayOfWeek.Monday].Open);
        Assert.Equal(new TimeOnly(12, 45), calendar.WorkingWeek[DayOfWeek.Monday].Close);
        Assert.Equal(new TimeOnly(14, 0), calendar.WorkingWeek[DayOfWeek.Sunday].Open);
        Assert.Equal(new TimeOnly(17, 30), calendar.WorkingWeek[DayOfWeek.Sunday].Close);
        Assert.False(calendar.WorkingWeek.ContainsKey(DayOfWeek.Tuesday));
    }

    [Fact]
    public async Task Separate_hosts_keep_their_calendar_and_frozen_now_isolated()
    {
        var berlinClock = new FrozenTimeProvider(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero));
        var tokyoClock = new FrozenTimeProvider(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero));
        using var berlin = CreateHost(new Dictionary<string, string?>
        {
            ["Calendar:Timezone"] = "Europe/Berlin",
            ["Calendar:WorkingWeek:Sunday:Open"] = "01:30",
            ["Calendar:WorkingWeek:Sunday:Close"] = "04:00"
        }, berlinClock);
        using var tokyo = CreateHost(new Dictionary<string, string?>
        {
            ["Calendar:Timezone"] = "Asia/Tokyo"
        }, tokyoClock);
        using var berlinClient = berlin.CreateClient();
        using var tokyoClient = tokyo.CreateClient();

        var berlinCalendar = berlin.Services.GetRequiredService<CalendarContext>();
        var tokyoCalendar = tokyo.Services.GetRequiredService<CalendarContext>();

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), berlinCalendar.GetUtcNow());
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), berlinCalendar.GetUtcNow());
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), tokyoCalendar.GetUtcNow());
        Assert.Single(berlinCalendar.Configuration.WorkingWeek);
        Assert.Empty(tokyoCalendar.Configuration.WorkingWeek);

        berlinClock.UtcNow = new DateTimeOffset(2026, 3, 30, 7, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 3, 30, 7, 0, 0, TimeSpan.Zero), berlinCalendar.GetUtcNow());
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), tokyoCalendar.GetUtcNow());

        using var berlinBody = JsonDocument.Parse(await berlinClient.GetStringAsync("/api/calendar"));
        using var tokyoBody = JsonDocument.Parse(await tokyoClient.GetStringAsync("/api/calendar"));
        Assert.Equal("Europe/Berlin", berlinBody.RootElement.GetProperty("timezone").GetString());
        Assert.Equal("Asia/Tokyo", tokyoBody.RootElement.GetProperty("timezone").GetString());
    }

    [Theory]
    [InlineData("Calendar:Timezone", "W. Europe Standard Time", typeof(ArgumentException))]
    [InlineData("Calendar:Timezone", "Europe/Unknown", typeof(ArgumentException))]
    [InlineData("Calendar:Timezone", "Europe//Berlin", typeof(ArgumentException))]
    [InlineData("Calendar:Timezone", "posix/Europe/Berlin", typeof(ArgumentException))]
    [InlineData("Calendar:Timezone", null, typeof(InvalidOperationException))]
    [InlineData("Calendar:WorkingWeek:Monday:Open", "22:00", typeof(ArgumentException))]
    [InlineData("Calendar:WorkingWeek:Monday:Close", "09:00", typeof(ArgumentException))]
    [InlineData("Calendar:WorkingWeek:Monday:Close", "06:00", typeof(ArgumentException))]
    [InlineData("Calendar:WorkingWeek:Monday:Open", "24:00", typeof(InvalidOperationException))]
    [InlineData("Calendar:WorkingWeek:Monday:Close", "not-a-time", typeof(InvalidOperationException))]
    [InlineData("Calendar:WorkingWeek:Monday:Close", null, typeof(InvalidOperationException))]
    [InlineData("Calendar:WorkingWeek:Funday:Open", "09:00", typeof(InvalidOperationException))]
    public void Invalid_calendar_configuration_prevents_host_startup(
        string key, string? value, Type exceptionType)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Calendar:Timezone"] = "Europe/Berlin",
            ["Calendar:WorkingWeek:Monday:Open"] = "09:00",
            ["Calendar:WorkingWeek:Monday:Close"] = "17:00",
            [key] = value
        };
        using var factory = CreateHost(settings);

        Assert.Throws(exceptionType, () => factory.CreateClient());
    }

    private static WebApplicationFactory<Program> CreateHost(
        Dictionary<string, string?> settings,
        TimeProvider? timeProvider = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(settings);
            });
            if (timeProvider is not null)
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(timeProvider);
                });
            }
        });
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
