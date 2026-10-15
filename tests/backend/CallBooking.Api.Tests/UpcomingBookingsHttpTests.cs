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

public class UpcomingBookingsHttpTests
{
    [Fact]
    public async Task No_bookings_returns_200_empty_array()
    {
        using var host = new UpcomingHost();

        using var body = await ReadUpcoming(host);

        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
        Assert.Empty(body.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Future_booking_returns_saved_event_type_and_guest_with_only_approved_fields_and_UTC_Z_instants()
    {
        using var host = new UpcomingHost();
        host.EventTypes.Add(new EventType("type-id", "Saved consultation", null, 30));
        host.Bookings.Add(new Booking("booking-id", "type-id",
            Instant("2026-10-14T12:00:00+02:00"), Instant("2026-10-14T12:30:00+02:00"),
            "Alice", "alice@example.com"));

        using var body = await ReadUpcoming(host);

        var booking = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("Saved consultation", booking.GetProperty("eventTypeName").GetString());
        Assert.Equal("Alice", booking.GetProperty("guestName").GetString());
        Assert.Equal("alice@example.com", booking.GetProperty("guestEmail").GetString());
        Assert.Equal(Instant("2026-10-14T10:00:00Z"), booking.GetProperty("start").GetDateTimeOffset());
        Assert.Equal(Instant("2026-10-14T10:30:00Z"), booking.GetProperty("end").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("2026-10-14T08:00:00Z", "2026-10-14T08:30:00Z")]
    [InlineData("2026-10-14T07:00:00Z", "2026-10-14T07:30:00Z")]
    [InlineData("2026-10-14T07:59:59Z", "2026-10-14T08:29:59Z")]
    [InlineData("2026-10-14T10:00:00+02:00", "2026-10-14T10:30:00+02:00")]
    [InlineData("2026-10-14T10:30:00+03:00", "2026-10-14T11:00:00+03:00")]
    public async Task Start_at_or_before_now_is_excluded_even_if_end_is_future_or_local_start_looks_later(
        string start, string end)
    {
        using var host = new UpcomingHost();
        host.EventTypes.Add(new EventType("type", "Call", null, 30));
        host.Bookings.Add(new Booking("excluded", "type", Instant(start), Instant(end), "Guest", "guest@example.com"));

        using var body = await ReadUpcoming(host);

        Assert.Empty(body.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Start_one_tick_after_now_is_included_even_if_local_display_looks_earlier()
    {
        using var host = new UpcomingHost();
        host.EventTypes.Add(new EventType("type", "Call", null, 30));
        host.Bookings.Add(new Booking("future", "type", Instant("2026-10-14T07:00:00.0000001-01:00"),
            Instant("2026-10-14T07:30:00.0000001-01:00"), "Guest", "guest@example.com"));

        using var body = await ReadUpcoming(host);

        var booking = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(Instant("2026-10-14T08:00:00.0000001Z"), booking.GetProperty("start").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("012")]
    [InlineData("021")]
    [InlineData("102")]
    [InlineData("120")]
    [InlineData("201")]
    [InlineData("210")]
    public async Task All_event_types_are_sorted_by_absolute_start_independently_of_insertion_order(string insertionOrder)
    {
        using var host = new UpcomingHost();
        host.EventTypes.Add(new EventType("a", "First type", null, 30));
        host.EventTypes.Add(new EventType("b", "Second type", null, 60));
        host.EventTypes.Add(new EventType("unused", "No bookings", null, 15));
        Booking[] bookings =
        [
            new("early", "b", Instant("2026-10-14T12:00:00+03:00"), Instant("2026-10-14T13:00:00+03:00"),
                "Early guest", "early@example.com"),
            new("middle", "a", Instant("2026-10-14T08:00:00-02:00"), Instant("2026-10-14T08:30:00-02:00"),
                "Middle guest", "middle@example.com"),
            new("late", "b", Instant("2026-11-14T11:00:00Z"), Instant("2026-11-14T12:00:00Z"),
                "Late guest", "late@example.com")
        ];
        foreach (var index in insertionOrder)
        {
            host.Bookings.Add(bookings[index - '0']);
        }
        host.Bookings.Add(new Booking("past", "a", Instant("2026-10-14T07:30:00Z"),
            Instant("2026-10-14T08:30:00Z"), "Excluded", "excluded@example.com"));

        using var body = await ReadUpcoming(host);

        var upcoming = body.RootElement.EnumerateArray().ToArray();
        Assert.Equal(new[] { Instant("2026-10-14T09:00:00Z"), Instant("2026-10-14T10:00:00Z"), Instant("2026-11-14T11:00:00Z") },
            upcoming.Select(booking => booking.GetProperty("start").GetDateTimeOffset()));
        Assert.Equal(new[] { "Second type", "First type", "Second type" },
            upcoming.Select(booking => booking.GetProperty("eventTypeName").GetString()));
        Assert.Equal(new[] { "Early guest", "Middle guest", "Late guest" },
            upcoming.Select(booking => booking.GetProperty("guestName").GetString()));
        Assert.Equal(new[] { "early@example.com", "middle@example.com", "late@example.com" },
            upcoming.Select(booking => booking.GetProperty("guestEmail").GetString()));
        Assert.Equal(new[] { Instant("2026-10-14T10:00:00Z"), Instant("2026-10-14T10:30:00Z"), Instant("2026-11-14T12:00:00Z") },
            upcoming.Select(booking => booking.GetProperty("end").GetDateTimeOffset()));
    }

    private static async Task<JsonDocument> ReadUpcoming(UpcomingHost host)
    {
        using var response = await host.Client.GetAsync("/api/bookings/upcoming");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
        foreach (var booking in body.RootElement.EnumerateArray())
        {
            Assert.Equal(new[] { "end", "eventTypeName", "guestEmail", "guestName", "start" },
                booking.EnumerateObject().Select(property => property.Name).Order());
            Assert.All(booking.EnumerateObject(), property => Assert.Equal(JsonValueKind.String, property.Value.ValueKind));
            foreach (var field in new[] { "start", "end" })
            {
                var instant = booking.GetProperty(field);
                Assert.EndsWith("Z", instant.GetString());
                Assert.Equal(TimeSpan.Zero, instant.GetDateTimeOffset().Offset);
            }
        }
        return body;
    }

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value);

    private sealed class UpcomingHost : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly IServiceScope _scope;

        public HttpClient Client { get; }
        public IEventTypeRepository EventTypes => _scope.ServiceProvider.GetRequiredService<IEventTypeRepository>();
        public IBookingRepository Bookings => _scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        public UpcomingHost()
        {
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.Sources.Clear();
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Calendar:Timezone"] = "Etc/UTC"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FrozenTimeProvider());
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

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Instant("2026-10-14T08:00:00Z");
    }
}
