using System.Net;
using System.Text.Json;
using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CallBooking.Api.Tests;

public class SlotsHttpTests
{
    [Fact]
    public async Task Known_type_returns_fixed_start_grid_with_saved_duration_as_UTC_Z_slots()
    {
        using var host = new SlotsHost("Etc/UTC", "2026-10-14T09:40:00Z", DayOfWeek.Wednesday, "09:15", "10:50");
        host.EventTypes.Add(new EventType("saved-type", "Call", null, 30));

        var slots = await ReadSlots(host.Client, "saved-type");

        Assert.Equal(new[]
        {
            "2026-10-14T09:45:00Z", "2026-10-14T10:15:00Z", "2026-10-21T09:15:00Z",
            "2026-10-21T09:45:00Z", "2026-10-21T10:15:00Z"
        }.Select(Instant), slots.Select(slot => slot.Start));
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(30), slot.End - slot.Start));
    }

    [Fact]
    public async Task Unknown_type_returns_contract_404_problem_without_framework_extension_fields()
    {
        using var host = new SlotsHost("Etc/UTC", "2026-10-14T08:00:00Z");

        using var response = await host.Client.GetAsync("/api/event-types/unknown/slots");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("title").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.All(body.RootElement.EnumerateObject(), property =>
            Assert.Contains(property.Name, new[] { "status", "title", "detail", "type", "instance" }));
        Assert.Empty(host.Bookings.List());
    }

    [Theory]
    [InlineData(30, new[] { "09:15", "09:45", "10:15" })]
    [InlineData(45, new[] { "09:15", "09:45" })]
    [InlineData(60, new[] { "09:15", "09:45" })]
    [InlineData(100, new string[] { })]
    [InlineData(int.MaxValue, new string[] { })]
    public async Task Fixed_start_interval_is_independent_of_duration_and_long_duration_is_a_successful_empty_result(
        int duration, string[] expected)
    {
        using var host = new SlotsHost("Etc/UTC", "2026-10-14T08:00:00Z", DayOfWeek.Wednesday, "09:15", "10:50");
        host.EventTypes.Add(new EventType("selected", "Call", null, duration));
        host.EventTypes.Add(new EventType("another", "Call", null, 15));

        var slots = await ReadSlots(host.Client, "selected");

        Assert.Equal(expected.Select(time => Instant($"2026-10-14T{time}:00Z"))
            .Concat(expected.Select(time => Instant($"2026-10-21T{time}:00Z"))), slots.Select(slot => slot.Start));
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(duration), slot.End - slot.Start));
        Assert.Equal(duration, host.EventTypes.GetById("selected")?.DurationMinutes);
        Assert.Equal(2, host.EventTypes.List().Count);
    }

    [Fact]
    public async Task Reads_do_not_reserve_and_see_new_bookings_of_any_type_in_the_same_saved_storage()
    {
        using var host = new SlotsHost("Etc/UTC", "2026-10-14T08:00:00Z", DayOfWeek.Wednesday);
        host.EventTypes.Add(new EventType("half-hour", "Short", null, 30));
        host.EventTypes.Add(new EventType("hour", "Long", null, 60));

        var initial = await ReadSlots(host.Client, "half-hour");
        Assert.Equal(initial, await ReadSlots(host.Client, "half-hour"));
        Assert.Empty(host.Bookings.List());

        var booking = new Booking("existing", "hour", Instant("2026-10-14T10:00:00Z"),
            Instant("2026-10-14T11:00:00Z"), "Guest", "guest@example.com");
        host.Bookings.Add(booking);
        var afterBooking = await ReadSlots(host.Client, "half-hour");

        Assert.Equal(new[] { "09:00", "09:30", "11:00", "11:30" }.Select(time => Instant($"2026-10-14T{time}:00Z")),
            afterBooking.Where(slot => slot.Start.Date == new DateTime(2026, 10, 14)).Select(slot => slot.Start));
        Assert.Equal(afterBooking, await ReadSlots(host.Client, "half-hour"));
        Assert.Same(booking, Assert.Single(host.Bookings.List()));
    }

    [Theory]
    [InlineData("2026-10-14T09:00:00Z", "2026-10-14T09:15:00Z", new[] { "09:30", "10:00", "10:30", "11:00", "11:30" })]
    [InlineData("2026-10-14T09:15:00Z", "2026-10-14T09:45:00Z", new[] { "10:00", "10:30", "11:00", "11:30" })]
    [InlineData("2026-10-13T23:45:00Z", "2026-10-14T09:15:00Z", new[] { "09:30", "10:00", "10:30", "11:00", "11:30" })]
    public async Task General_and_cross_midnight_bookings_block_all_absolute_overlaps(
        string start, string end, string[] expected)
    {
        using var host = new SlotsHost("Etc/UTC", "2026-10-14T08:00:00Z", DayOfWeek.Wednesday);
        host.EventTypes.Add(new EventType("selected", "Short", null, 30));
        host.Bookings.Add(new Booking("existing", "other-type", Instant(start), Instant(end), "Guest", "guest@example.com"));

        var slots = await ReadSlots(host.Client, "selected");

        Assert.Equal(expected.Select(time => Instant($"2026-10-14T{time}:00Z")),
            slots.Where(slot => slot.Start.Date == new DateTime(2026, 10, 14)).Select(slot => slot.Start));
    }

    [Fact]
    public async Task No_working_days_and_fully_occupied_working_days_are_successful_empty_arrays()
    {
        using var closed = new SlotsHost("Etc/UTC", "2026-10-14T08:00:00Z");
        closed.EventTypes.Add(new EventType("known", "Call", null, 30));
        Assert.Empty(await ReadSlots(closed.Client, "known"));

        using var occupied = new SlotsHost("Etc/UTC", "2026-10-14T08:00:00Z", DayOfWeek.Wednesday);
        occupied.EventTypes.Add(new EventType("known", "Call", null, 30));
        occupied.Bookings.Add(new Booking("existing", "other-type", Instant("2026-10-14T09:00:00Z"),
            Instant("2026-10-28T12:00:00Z"), "Guest", "guest@example.com"));
        Assert.Empty(await ReadSlots(occupied.Client, "known"));
    }

    [Fact]
    public async Task Current_time_is_read_per_request_and_equal_start_is_unavailable_without_reanchoring()
    {
        using var host = new SlotsHost("Etc/UTC", "2026-10-14T09:40:00Z", DayOfWeek.Wednesday, "09:15", "10:50");
        host.EventTypes.Add(new EventType("known", "Call", null, 30));
        Assert.Equal(Instant("2026-10-14T09:45:00Z"), (await ReadSlots(host.Client, "known"))[0].Start);

        host.Clock.UtcNow = Instant("2026-10-14T09:45:00Z");
        Assert.Equal(Instant("2026-10-14T10:15:00Z"), (await ReadSlots(host.Client, "known"))[0].Start);
    }

    [Fact]
    public async Task Today_and_fourteen_date_cutoff_use_calendar_timezone_not_UTC_dates()
    {
        using var host = new SlotsHost("Asia/Tokyo", "2026-10-13T16:00:00Z", DayOfWeek.Wednesday, "01:30", "02:30");
        host.EventTypes.Add(new EventType("known", "Call", null, 30));

        var slots = await ReadSlots(host.Client, "known");

        Assert.Equal(new[]
        {
            "2026-10-13T16:30:00Z", "2026-10-13T17:00:00Z", "2026-10-20T16:30:00Z", "2026-10-20T17:00:00Z"
        }.Select(Instant), slots.Select(slot => slot.Start));
    }

    [Fact]
    public async Task Fall_back_booking_window_includes_last_date_slots_beyond_336_elapsed_hours()
    {
        using var host = new SlotsHost("Europe/Berlin", "2026-10-24T12:00:00Z", DayOfWeek.Friday, "23:00", "23:59");
        host.EventTypes.Add(new EventType("known", "Call", null, 30));

        var slots = await ReadSlots(host.Client, "known");

        Assert.Equal(new[] { "2026-10-30T22:00:00Z", "2026-11-06T22:00:00Z" }.Select(Instant), slots.Select(slot => slot.Start));
        Assert.Equal(Instant("2026-11-06T22:30:00Z"), slots[^1].End);
    }

    [Theory]
    [InlineData("2026-03-28T22:00:00Z", "00:00", "05:00", 45, new[]
    {
        "2026-03-28T23:00:00Z", "2026-03-28T23:30:00Z", "2026-03-29T00:00:00Z",
        "2026-03-29T00:30:00Z", "2026-03-29T01:00:00Z", "2026-03-29T01:30:00Z", "2026-03-29T02:00:00Z"
    })]
    [InlineData("2026-10-24T21:00:00Z", "02:00", "03:00", 60, new[] { "2026-10-25T00:00:00Z", "2026-10-25T00:30:00Z", "2026-10-25T01:00:00Z" })]
    [InlineData("2026-10-24T21:00:00Z", "01:00", "02:30", 30, new[]
    {
        "2026-10-24T23:00:00Z", "2026-10-24T23:30:00Z", "2026-10-25T00:00:00Z",
        "2026-10-25T00:30:00Z", "2026-10-25T01:00:00Z"
    })]
    [InlineData("2026-03-28T22:00:00Z", "02:15", "04:00", 30, new string[] { })]
    [InlineData("2026-03-28T22:00:00Z", "01:00", "02:45", 30, new string[] { })]
    public async Task DST_rules_are_applied_through_real_HTTP_and_saved_types(
        string now, string open, string close, int duration, string[] expected)
    {
        using var host = new SlotsHost("Europe/Berlin", now, DayOfWeek.Sunday, open, close);
        host.EventTypes.Add(new EventType("known", "Call", null, duration));

        var slots = await ReadSlots(host.Client, "known");

        var beforeFollowingWeek = Instant(now).AddDays(2);
        Assert.Equal(expected.Select(Instant), slots.Where(slot => slot.Start < beforeFollowingWeek).Select(slot => slot.Start));
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(duration), slot.End - slot.Start));
        Assert.Contains(slots, slot => slot.Start >= beforeFollowingWeek);
    }

    private static async Task<(DateTimeOffset Start, DateTimeOffset End)[]> ReadSlots(HttpClient client, string id)
    {
        using var response = await client.GetAsync($"/api/event-types/{id}/slots");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
        return body.RootElement.EnumerateArray().Select(slot =>
        {
            Assert.Equal(new[] { "end", "start" }, slot.EnumerateObject().Select(property => property.Name).Order());
            var start = slot.GetProperty("start");
            var end = slot.GetProperty("end");
            Assert.Equal(JsonValueKind.String, start.ValueKind);
            Assert.Equal(JsonValueKind.String, end.ValueKind);
            Assert.EndsWith("Z", start.GetString());
            Assert.EndsWith("Z", end.GetString());
            return (start.GetDateTimeOffset(), end.GetDateTimeOffset());
        }).ToArray();
    }

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value);

    private sealed class SlotsHost : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly IServiceScope _scope;

        public HttpClient Client { get; }
        public FrozenTimeProvider Clock { get; }
        public IEventTypeRepository EventTypes => _scope.ServiceProvider.GetRequiredService<IEventTypeRepository>();
        public IBookingRepository Bookings => _scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        public SlotsHost(string timezone, string now, DayOfWeek? day = null, string open = "09:00", string close = "12:00")
        {
            Clock = new FrozenTimeProvider(Instant(now));
            var settings = new Dictionary<string, string?> { ["Calendar:Timezone"] = timezone };
            if (day is not null)
            {
                settings[$"Calendar:WorkingWeek:{day}:Open"] = open;
                settings[$"Calendar:WorkingWeek:{day}:Close"] = close;
            }

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.Sources.Clear();
                    configuration.AddInMemoryCollection(settings);
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(Clock);
                });
            });
            _scope = _factory.Services.CreateScope();
            Client = _factory.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            _scope.Dispose();
            _factory.Dispose();
        }
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
