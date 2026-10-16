using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CallBooking.Api.Tests;

public class BookingsHttpTests
{
    [Theory]
    [InlineData(30, "11:15")]
    [InlineData(45, "10:45")]
    [InlineData(60, "10:45")]
    public async Task Thirty_minute_start_interval_is_anchored_at_open_not_duration_or_clock_hour(int duration, string incompleteStart)
    {
        using var host = new BookingsHost(open: "09:15", close: "11:15", duration: duration);
        Assert.Contains(Instant("2026-10-14T09:45:00Z"), await ReadSlotStarts(host));
        using var offGrid = await host.Post("known", "2026-10-14T10:00:00Z");
        await AssertProblem(offGrid, HttpStatusCode.BadRequest);
        using var incomplete = await host.Post("known", $"2026-10-14T{incompleteStart}:00Z");
        await AssertProblem(incomplete, HttpStatusCode.BadRequest);
        Assert.Empty(host.Bookings.List());

        using var accepted = await host.Post("known", "2026-10-14T09:45:00Z");
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(Instant("2026-10-14T09:45:00Z").AddMinutes(duration), Assert.Single(host.Bookings.List()).End);
        using var occupied = await host.Post("known", "2026-10-14T09:45:00Z");
        await AssertProblem(occupied, HttpStatusCode.Conflict);
        Assert.Single(host.Bookings.List());
    }

    [Fact]
    public async Task Forty_five_minute_booking_blocks_intersecting_grid_starts_without_reanchoring_at_its_end()
    {
        using var host = new BookingsHost(duration: 45);
        using var first = await host.Post("known", "2026-10-14T09:00:00Z");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var overlap = await host.Post("known", "2026-10-14T09:30:00Z");
        await AssertProblem(overlap, HttpStatusCode.Conflict);
        using var endOffGrid = await host.Post("known", "2026-10-14T09:45:00Z");
        await AssertProblem(endOffGrid, HttpStatusCode.BadRequest);
        using var next = await host.Post("known", "2026-10-14T10:00:00Z");
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        Assert.Equal(2, host.Bookings.List().Count);
    }

    [Fact]
    public async Task Valid_booking_returns_201_only_start_end_without_location_after_saving_trimmed_guest()
    {
        using var host = new BookingsHost();

        using var response = await host.Post("known", "2026-10-14T09:00:00Z", "  Alice  ", "  alice@example.com  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("Location"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "end", "start" }, body.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.EndsWith("Z", body.RootElement.GetProperty("start").GetString());
        Assert.EndsWith("Z", body.RootElement.GetProperty("end").GetString());
        Assert.Equal(Instant("2026-10-14T09:00:00Z"), body.RootElement.GetProperty("start").GetDateTimeOffset());
        Assert.Equal(Instant("2026-10-14T09:30:00Z"), body.RootElement.GetProperty("end").GetDateTimeOffset());
        var saved = Assert.Single(host.Bookings.List());
        Assert.False(string.IsNullOrWhiteSpace(saved.Id));
        Assert.Equal("known", saved.EventTypeId);
        Assert.Equal(Instant("2026-10-14T09:00:00Z"), saved.Start);
        Assert.Equal(Instant("2026-10-14T09:30:00Z"), saved.End);
        Assert.Equal("Alice", saved.GuestName);
        Assert.Equal("alice@example.com", saved.GuestEmail);
        Assert.Same(saved, host.Bookings.GetById(saved.Id));
        using var subsequentRead = await host.Client.GetAsync("/api/event-types/known/slots");
        using var slots = JsonDocument.Parse(await subsequentRead.Content.ReadAsStringAsync());
        Assert.DoesNotContain(slots.RootElement.EnumerateArray(), slot =>
            slot.GetProperty("start").GetDateTimeOffset() == saved.Start);
        using var upcomingResponse = await host.Client.GetAsync("/api/bookings/upcoming");
        Assert.Equal(HttpStatusCode.OK, upcomingResponse.StatusCode);
        Assert.Equal("application/json", upcomingResponse.Content.Headers.ContentType?.MediaType);
        using var upcoming = JsonDocument.Parse(await upcomingResponse.Content.ReadAsStringAsync());
        var entry = Assert.Single(upcoming.RootElement.EnumerateArray());
        Assert.Equal("Call", entry.GetProperty("eventTypeName").GetString());
        Assert.Equal("Alice", entry.GetProperty("guestName").GetString());
        Assert.Equal("alice@example.com", entry.GetProperty("guestEmail").GetString());
        Assert.Equal(body.RootElement.GetProperty("start").GetString(), entry.GetProperty("start").GetString());
        Assert.Equal(body.RootElement.GetProperty("end").GetString(), entry.GetProperty("end").GetString());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("{\"start\":\"2026-10-14T09:00:00Z\",\"name\":\"Guest\",\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"name\":\"Guest\",\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"start\":\"2026-10-14T09:00:00Z\",\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"start\":\"2026-10-14T09:00:00Z\",\"name\":\"Guest\"}")]
    [InlineData("{\"eventTypeId\":null,\"start\":\"2026-10-14T09:00:00Z\",\"name\":\"Guest\",\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"start\":null,\"name\":\"Guest\",\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"start\":\"2026-10-14T09:00:00Z\",\"name\":null,\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"start\":\"2026-10-14T09:00:00Z\",\"name\":\"Guest\",\"email\":null}")]
    [InlineData("{\"eventTypeId\":17,\"start\":\"2026-10-14T09:00:00Z\",\"name\":\"Guest\",\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"start\":17,\"name\":\"Guest\",\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"start\":\"2026-10-14T09:00:00Z\",\"name\":true,\"email\":\"guest@example.com\"}")]
    [InlineData("{\"eventTypeId\":\"known\",\"start\":\"2026-10-14T09:00:00Z\",\"name\":\"Guest\",\"email\":{}}")]
    public async Task Malformed_or_schema_invalid_body_returns_400_problem_and_saves_nothing(string json)
    {
        using var host = new BookingsHost();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await host.Client.PostAsync("/api/bookings", content);

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Empty(host.Bookings.List());
    }

    [Theory]
    [InlineData("not-a-timestamp")]
    [InlineData("2026-10-14T09:00:00+00:00")]
    [InlineData("2026-10-14T09:00:00-00:00")]
    [InlineData("2026-10-14T11:00:00+02:00")]
    [InlineData("2026-10-14T09:00:00")]
    [InlineData("2026-10-14")]
    [InlineData("2026-10-14T09:00:00z")]
    public async Task Unparseable_or_non_Z_wire_timestamp_returns_400_without_saving(string start)
    {
        using var host = new BookingsHost();

        using var response = await host.Post("known", start);

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Empty(host.Bookings.List());
    }

    [Fact]
    public async Task Missing_body_returns_400_problem_and_saves_nothing()
    {
        using var host = new BookingsHost();

        using var response = await host.Client.PostAsync("/api/bookings", null);

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Empty(host.Bookings.List());
    }

    [Theory]
    [InlineData("", "guest@example.com")]
    [InlineData(" \t\r\n ", "guest@example.com")]
    [InlineData("Guest", "")]
    [InlineData("Guest", " \t ")]
    [InlineData("Guest", "not-an-email")]
    [InlineData("Guest", "@example.com")]
    [InlineData("Guest", "guest@")]
    [InlineData("Guest", "guest@@example.com")]
    public async Task Invalid_guest_returns_400_and_saves_nothing(string name, string email)
    {
        using var host = new BookingsHost();

        using var response = await host.Post("known", "2026-10-14T09:00:00Z", name, email);

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Empty(host.Bookings.List());
    }

    [Theory]
    [InlineData("2026-10-14T09:15:00Z")]
    [InlineData("2026-10-14T09:00:00.0000001Z")]
    [InlineData("2026-10-14T09:00:00.0000001000000000Z")]
    [InlineData("2026-10-14T08:30:00Z")]
    [InlineData("2026-10-14T12:00:00Z")]
    [InlineData("2026-10-15T09:00:00Z")]
    public async Task Off_grid_start_is_400_independently_of_current_availability(string start)
    {
        using var host = new BookingsHost();

        using var response = await host.Post("known", start);

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Empty(host.Bookings.List());
    }

    [Fact]
    public async Task Empty_timestamp_fraction_returns_400_problem_without_saving_or_consuming_slot()
    {
        using var host = new BookingsHost();
        var start = (await ReadSlotStarts(host))[0];
        var timestamp = start.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

        using var response = await host.Post("known", $"{timestamp}.Z");

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Empty(host.Bookings.List());
        Assert.Contains(start, await ReadSlotStarts(host));
    }

    [Theory]
    [InlineData("00000001")]
    [InlineData("000000001")]
    [InlineData("0000000000000001")]
    public async Task Sub_tick_start_returns_400_problem_without_saving_or_consuming_slot(string fraction)
    {
        using var host = new BookingsHost();
        var start = (await ReadSlotStarts(host))[0];
        var timestamp = start.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

        using var response = await host.Post("known", $"{timestamp}.{fraction}Z");

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Empty(host.Bookings.List());
        Assert.Contains(start, await ReadSlotStarts(host));
    }

    [Theory]
    [InlineData("00000000")]
    [InlineData("000000000")]
    [InlineData("0000000000000000")]
    public async Task Exactly_representable_start_with_extra_zero_digits_is_accepted(string fraction)
    {
        using var host = new BookingsHost();

        using var response = await host.Post("known", $"2026-10-14T09:00:00.{fraction}Z");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Instant("2026-10-14T09:00:00Z"), Assert.Single(host.Bookings.List()).Start);
    }

    [Fact]
    public async Task Unknown_event_type_returns_404_problem_and_saves_nothing()
    {
        using var host = new BookingsHost();

        using var response = await host.Post("unknown", "2026-10-14T09:00:00Z");

        await AssertProblem(response, HttpStatusCode.NotFound);
        Assert.Empty(host.Bookings.List());
    }

    [Theory]
    [InlineData("2026-10-14T09:00:00Z", "2026-10-14T09:00:00Z")]
    [InlineData("2026-10-14T09:01:00Z", "2026-10-14T09:00:00Z")]
    [InlineData("2026-10-14T08:00:00Z", "2026-10-07T09:00:00Z")]
    [InlineData("2026-10-14T08:00:00Z", "2026-10-28T09:00:00Z")]
    [InlineData("2026-10-14T08:00:00Z", "2026-11-04T09:00:00Z")]
    public async Task Canonical_start_at_or_before_now_or_outside_booking_window_is_409(string now, string start)
    {
        using var host = new BookingsHost(now: now);

        using var response = await host.Post("known", start);

        await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Empty(host.Bookings.List());
    }

    [Theory]
    [InlineData("2026-10-14T09:00:00Z")]
    [InlineData("2026-10-14T09:01:00Z")]
    public async Task Formerly_listed_slot_is_rechecked_against_submission_time(string now)
    {
        using var host = new BookingsHost();
        Assert.Contains(Instant("2026-10-14T09:00:00Z"), await ReadSlotStarts(host));
        host.Clock.UtcNow = Instant(now);

        using var response = await host.Post("known", "2026-10-14T09:00:00Z");

        await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Empty(host.Bookings.List());
    }

    [Fact]
    public async Task Formerly_listed_slot_is_rechecked_against_the_current_booking_window()
    {
        using var host = new BookingsHost();
        Assert.Contains(Instant("2026-10-21T09:00:00Z"), await ReadSlotStarts(host));
        host.Clock.UtcNow = Instant("2026-10-01T08:00:00Z");

        using var response = await host.Post("known", "2026-10-21T09:00:00Z");

        await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Empty(host.Bookings.List());
    }

    [Theory]
    [InlineData("known", "2026-10-14T10:00:00Z", "known", "2026-10-14T10:00:00Z")]
    [InlineData("hour", "2026-10-14T10:00:00Z", "known", "2026-10-14T10:00:00Z")]
    [InlineData("hour", "2026-10-14T10:00:00Z", "known", "2026-10-14T10:30:00Z")]
    [InlineData("known", "2026-10-14T10:30:00Z", "hour", "2026-10-14T10:00:00Z")]
    public async Task Sequential_overlap_of_any_type_rejects_formerly_listed_selection_without_writing(
        string firstType, string firstStart, string secondType, string secondStart)
    {
        using var host = new BookingsHost();
        host.EventTypes.Add(new EventType("hour", "Long call", null, 60));
        Assert.Contains(Instant(secondStart), await ReadSlotStarts(host, secondType));
        using var first = await host.Post(firstType, firstStart);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var before = host.Bookings.List();

        using var rejected = await host.Post(secondType, secondStart);

        await AssertProblem(rejected, HttpStatusCode.Conflict);
        Assert.Equal(before, host.Bookings.List());
        Assert.Single(host.Bookings.List());
        Assert.DoesNotContain(Instant(secondStart), await ReadSlotStarts(host, secondType));
    }

    [Fact]
    public async Task Adjacent_intervals_of_different_types_are_allowed_on_both_sides()
    {
        using var host = new BookingsHost();
        host.EventTypes.Add(new EventType("hour", "Long call", null, 60));
        using var middle = await host.Post("hour", "2026-10-14T10:00:00Z");
        using var before = await host.Post("known", "2026-10-14T09:30:00Z");
        using var after = await host.Post("known", "2026-10-14T11:00:00Z");

        Assert.Equal(HttpStatusCode.Created, middle.StatusCode);
        Assert.Equal(HttpStatusCode.Created, before.StatusCode);
        Assert.Equal(HttpStatusCode.Created, after.StatusCode);
        var saved = host.Bookings.List().OrderBy(booking => booking.Start).ToArray();
        Assert.Equal(3, saved.Length);
        Assert.Equal(3, saved.Select(booking => booking.Id).Distinct().Count());
        Assert.Equal(saved[0].End, saved[1].Start);
        Assert.Equal(saved[1].End, saved[2].Start);
    }

    [Theory]
    [InlineData("2026-03-28T22:00:00Z", "00:00", "05:00", 45, "2026-03-28T23:30:00Z", "2026-03-29T00:15:00Z")]
    [InlineData("2026-03-28T22:00:00Z", "00:00", "05:00", 45, "2026-03-29T00:30:00Z", "2026-03-29T01:15:00Z")]
    [InlineData("2026-10-24T21:00:00Z", "02:00", "03:00", 60, "2026-10-25T00:30:00Z", "2026-10-25T01:30:00Z")]
    [InlineData("2026-10-24T21:00:00Z", "02:00", "03:00", 60, "2026-10-25T00:00:00Z", "2026-10-25T01:00:00Z")]
    [InlineData("2026-10-24T21:00:00Z", "02:00", "03:00", 60, "2026-10-25T01:00:00Z", "2026-10-25T02:00:00Z")]
    public async Task Authoritative_duration_derives_end_on_elapsed_timeline_across_DST(
        string now, string open, string close, int duration, string start, string end)
    {
        using var host = new BookingsHost("Europe/Berlin", now, DayOfWeek.Sunday, open, close, duration);

        using var response = await host.Post("known", start);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(Instant(end), body.RootElement.GetProperty("end").GetDateTimeOffset());
        Assert.Equal(Instant(end), Assert.Single(host.Bookings.List()).End);
    }

    private static async Task<DateTimeOffset[]> ReadSlotStarts(BookingsHost host, string type = "known")
    {
        using var response = await host.Client.GetAsync($"/api/event-types/{type}/slots");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.EnumerateArray().Select(slot => slot.GetProperty("start").GetDateTimeOffset()).ToArray();
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("title").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.All(body.RootElement.EnumerateObject(), property =>
            Assert.Contains(property.Name, new[] { "status", "title", "detail", "type", "instance" }));
    }

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value);

    private sealed class BookingsHost : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly IServiceScope _scope;

        public HttpClient Client { get; }
        public FrozenTimeProvider Clock { get; }
        public IEventTypeRepository EventTypes => _scope.ServiceProvider.GetRequiredService<IEventTypeRepository>();
        public IBookingRepository Bookings => _scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        public BookingsHost(string timezone = "Etc/UTC", string now = "2026-10-14T08:00:00Z",
            DayOfWeek day = DayOfWeek.Wednesday, string open = "09:00", string close = "12:00", int duration = 30)
        {
            Clock = new FrozenTimeProvider(Instant(now));
            var settings = new Dictionary<string, string?>
            {
                ["Calendar:Timezone"] = timezone,
                [$"Calendar:WorkingWeek:{day}:Open"] = open,
                [$"Calendar:WorkingWeek:{day}:Close"] = close
            };
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
            EventTypes.Add(new EventType("known", "Call", null, duration));
        }

        public Task<HttpResponseMessage> Post(string type, string start, string name = "Guest", string email = "guest@example.com")
            => Client.PostAsJsonAsync("/api/bookings", new { eventTypeId = type, start, name, email });

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
