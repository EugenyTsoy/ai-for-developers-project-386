using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CallBooking.Application.Ports;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CallBooking.Api.Tests;

public class EventTypesHttpTests
{
    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("{\"name\":\"Call\"}")]
    [InlineData("{\"durationMinutes\":30}")]
    [InlineData("{\"name\":null,\"durationMinutes\":30}")]
    [InlineData("{\"name\":\"\",\"durationMinutes\":30}")]
    [InlineData("{\"name\":\" \\t\\r\\n \",\"durationMinutes\":30}")]
    [InlineData("{\"name\":42,\"durationMinutes\":30}")]
    [InlineData("{\"name\":true,\"durationMinutes\":30}")]
    [InlineData("{\"name\":{},\"durationMinutes\":30}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":null}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":\"30\"}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":true}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":[]}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":0}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":-1}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":1.5}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":2147483648}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":-2147483649}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":30,\"description\":null}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":30,\"description\":42}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":30,\"description\":true}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":30,\"description\":{}}")]
    [InlineData("{\"name\":\"Call\",\"durationMinutes\":30,\"description\":[]}")]
    public async Task Invalid_creation_returns_400_problem_without_saving(string json)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/event-types", content);

        await AssertRejected(response, client);
    }

    [Fact]
    public async Task Missing_body_returns_400_problem_without_saving()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/event-types", null);

        await AssertRejected(response, client);
    }

    private static async Task AssertRejected(HttpResponseMessage response, HttpClient client)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
        Assert.All(problem.RootElement.EnumerateObject(), property =>
            Assert.Contains(property.Name, new[] { "status", "title", "detail", "type", "instance" }));
        using var list = await client.GetAsync("/api/event-types");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Empty(body.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Creation_returns_201_and_subsequent_list_returns_saved_trimmed_type_with_stable_id()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/event-types", new
        {
            name = "  Consultation  ",
            description = "Discuss the project",
            durationMinutes = 30
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = created.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("id").GetString()));
        Assert.Equal("Consultation", item.GetProperty("name").GetString());
        Assert.Equal("Discuss the project", item.GetProperty("description").GetString());
        Assert.Equal(30, item.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(new[] { "description", "durationMinutes", "id", "name" },
            item.EnumerateObject().Select(property => property.Name).Order());
        using var scope = factory.Services.CreateScope();
        var persisted = Assert.Single(scope.ServiceProvider.GetRequiredService<IEventTypeRepository>().List());
        Assert.Equal(item.GetProperty("id").GetString(), persisted.Id);
        Assert.Equal("Consultation", persisted.Name);
        Assert.Equal("Discuss the project", persisted.Description);
        Assert.Equal(30, persisted.DurationMinutes);
        using var list = await client.GetAsync("/api/event-types");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("application/json", list.Content.Headers.ContentType?.MediaType);
        using var saved = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(item.GetRawText(), Assert.Single(saved.RootElement.EnumerateArray()).GetRawText());
    }

    [Fact]
    public async Task Empty_storage_returns_200_empty_array()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/event-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(61)]
    [InlineData(int.MaxValue)]
    public async Task Duplicate_names_and_omitted_description_are_allowed_for_every_positive_int32_duration(int duration)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ids = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            using var response = await client.PostAsJsonAsync("/api/event-types", new
            {
                id = "client-chosen",
                name = "Duplicate",
                durationMinutes = duration
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(body.RootElement.TryGetProperty("description", out _));
            Assert.Equal(new[] { "durationMinutes", "id", "name" },
                body.RootElement.EnumerateObject().Select(property => property.Name).Order());
            var id = body.RootElement.GetProperty("id").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.NotEqual("client-chosen", id);
            ids.Add(id);
            if (duration > 60)
            {
                using var slots = await client.GetAsync($"/api/event-types/{id}/slots");
                Assert.Equal(HttpStatusCode.OK, slots.StatusCode);
                Assert.Equal("[]", await slots.Content.ReadAsStringAsync());
            }
        }

        Assert.Equal(2, ids.Distinct().Count());
        for (var read = 0; read < 2; read++)
        {
            using var response = await client.GetAsync("/api/event-types");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = body.RootElement.EnumerateArray().ToArray();
            Assert.Equal(ids.Order(), items.Select(item => item.GetProperty("id").GetString()).Order());
            Assert.All(items, item =>
            {
                Assert.Equal("Duplicate", item.GetProperty("name").GetString());
                Assert.Equal(duration, item.GetProperty("durationMinutes").GetInt32());
                Assert.False(item.TryGetProperty("description", out _));
            });
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Optional description")]
    public async Task Supplied_string_description_is_valid(string description)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/event-types", new
        {
            name = "Call",
            description,
            durationMinutes = 1
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(description, body.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Long_strings_have_no_arbitrary_length_limit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var name = new string('n', 10000);
        var description = new string('d', 20000);
        using var response = await client.PostAsJsonAsync("/api/event-types", new { name, description, durationMinutes = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(name, body.RootElement.GetProperty("name").GetString());
        Assert.Equal(description, body.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Type_remains_listed_after_all_its_available_slots_are_booked()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var creation = await client.PostAsJsonAsync("/api/event-types", new { name = "Call", durationMinutes = 60 });
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        using var created = JsonDocument.Parse(await creation.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetString();
        foreach (var start in new[] { "2026-10-14T09:00:00Z", "2026-10-21T09:00:00Z" })
        {
            using var booking = await client.PostAsJsonAsync("/api/bookings", new
            {
                eventTypeId = id,
                start,
                name = "Guest",
                email = "guest@example.com"
            });
            Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        }

        using var slots = await client.GetAsync($"/api/event-types/{id}/slots");
        Assert.Equal(HttpStatusCode.OK, slots.StatusCode);
        Assert.Equal("[]", await slots.Content.ReadAsStringAsync());
        using var response = await client.GetAsync("/api/event-types");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(created.RootElement.GetRawText(), Assert.Single(body.RootElement.EnumerateArray()).GetRawText());
    }

    private static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Calendar:Timezone"] = "Etc/UTC",
                    ["Calendar:WorkingWeek:Wednesday:Open"] = "09:00",
                    ["Calendar:WorkingWeek:Wednesday:Close"] = "10:00"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FrozenTimeProvider());
            });
        });

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);
    }
}
