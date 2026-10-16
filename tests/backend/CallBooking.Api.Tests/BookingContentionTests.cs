using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;
using CallBooking.Infrastructure.InMemory;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CallBooking.Api.Tests;

public class BookingContentionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Same_event_type_and_slot_under_contention_commit_exactly_one_booking()
    {
        await AssertContendedBookings([
            new Selection("half", "2026-10-14T10:00:00Z", "2026-10-14T10:30:00Z"),
            new Selection("half", "2026-10-14T10:00:00Z", "2026-10-14T10:30:00Z"),
            new Selection("half", "2026-10-14T10:00:00Z", "2026-10-14T10:30:00Z"),
            new Selection("half", "2026-10-14T10:00:00Z", "2026-10-14T10:30:00Z")
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Different_event_types_with_same_start_under_contention_commit_exactly_one_booking(bool reverseRequests)
    {
        Selection[] selections = [
            new("forty-five", "2026-10-14T10:00:00Z", "2026-10-14T10:45:00Z"),
            new("half", "2026-10-14T10:00:00Z", "2026-10-14T10:30:00Z"),
            new("hour", "2026-10-14T10:00:00Z", "2026-10-14T11:00:00Z"),
            new("half", "2026-10-14T10:00:00Z", "2026-10-14T10:30:00Z")
        ];
        await AssertContendedBookings(reverseRequests ? selections.Reverse().ToArray() : selections);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Different_event_types_with_different_starts_and_overlapping_intervals_commit_exactly_one_booking(bool reverseRequests)
    {
        // 10:00–11:00 and 10:30–11:00 are both canonical and initially free.
        // Same-start uniqueness or an event-type-local gate cannot protect this case.
        Selection[] selections = [
            new("hour", "2026-10-14T10:00:00Z", "2026-10-14T11:00:00Z"),
            new("half", "2026-10-14T10:30:00Z", "2026-10-14T11:00:00Z"),
            new("hour", "2026-10-14T10:00:00Z", "2026-10-14T11:00:00Z"),
            new("half", "2026-10-14T10:30:00Z", "2026-10-14T11:00:00Z")
        ];
        await AssertContendedBookings(reverseRequests ? selections.Reverse().ToArray() : selections);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Forty_five_minute_and_half_hour_selections_with_different_starts_contend_for_same_time(bool reverseRequests)
    {
        Selection[] selections = [
            new("forty-five", "2026-10-14T09:30:00Z", "2026-10-14T10:15:00Z"),
            new("half", "2026-10-14T10:00:00Z", "2026-10-14T10:30:00Z")
        ];
        await AssertContendedBookings(reverseRequests ? selections.Reverse().ToArray() : selections);
    }

    private static async Task AssertContendedBookings(Selection[] selections)
    {
        using var contention = new ContentionProbe(selections.Length - 1);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Calendar:Timezone"] = "Etc/UTC",
                    ["Calendar:WorkingWeek:Wednesday:Open"] = "09:00",
                    ["Calendar:WorkingWeek:Wednesday:Close"] = "12:00"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FrozenTimeProvider());
                // The production repositories and write-scope registration are untouched.
                // Only construct the real store with its internal observer enabled.
                services.RemoveAll<IInMemoryCalendarStore>();
                services.AddSingleton<IInMemoryCalendarStore>(new InMemoryCalendarStore(contention));
            });
        });
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var types = scope.ServiceProvider.GetRequiredService<IEventTypeRepository>();
            types.Add(new EventType("half", "Short call", null, 30));
            types.Add(new EventType("forty-five", "Medium call", null, 45));
            types.Add(new EventType("hour", "Long call", null, 60));
        }
        var store = factory.Services.GetRequiredService<IInMemoryCalendarStore>();
        Assert.Empty(store.Bookings);
        foreach (var selection in selections.Distinct())
        {
            using var available = await client.GetAsync($"/api/event-types/{selection.Type}/slots");
            Assert.Equal(HttpStatusCode.OK, available.StatusCode);
            using var slots = JsonDocument.Parse(await available.Content.ReadAsStringAsync());
            Assert.Contains(slots.RootElement.EnumerateArray(), slot =>
                slot.GetProperty("start").GetDateTimeOffset() == Instant(selection.Start) &&
                slot.GetProperty("end").GetDateTimeOffset() == Instant(selection.End));
        }

        var requests = new List<Task<HttpResponseMessage>>();
        Exception? testFailure = null;
        try
        {
            // Hold one real HTTP operation inside the production protected section.
            // Start competitors only after entry, then require actual failed entry
            // attempts while it is held: launching Task.WhenAll alone proves nothing.
            requests.Add(Post(0));
            await contention.FirstEntered.WaitAsync(Timeout);
            for (var index = 1; index < selections.Length; index++)
            {
                requests.Add(Post(index));
            }
            await contention.AllCompetitorsContended.WaitAsync(Timeout);
            Assert.All(requests, request => Assert.False(request.IsCompleted));
            Assert.Empty(store.Bookings);
            contention.Release();

            var responses = await Task.WhenAll(requests).WaitAsync(Timeout);
            // Match each response to its request, never to completion order or a
            // predetermined winner. This also rules out persistence by rejected guests.
            var winner = Assert.Single(Enumerable.Range(0, responses.Length),
                index => responses[index].StatusCode == HttpStatusCode.Created);
            Assert.Equal(selections.Length - 1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            for (var index = 0; index < responses.Length; index++)
            {
                using var body = JsonDocument.Parse(await responses[index].Content.ReadAsStringAsync());
                if (index == winner)
                {
                    Assert.Equal("application/json", responses[index].Content.Headers.ContentType?.MediaType);
                    Assert.False(responses[index].Headers.Contains("Location"));
                    Assert.Equal(new[] { "end", "start" }, body.RootElement.EnumerateObject().Select(property => property.Name).Order());
                    Assert.EndsWith("Z", body.RootElement.GetProperty("start").GetString());
                    Assert.EndsWith("Z", body.RootElement.GetProperty("end").GetString());
                    Assert.Equal(Instant(selections[index].Start), body.RootElement.GetProperty("start").GetDateTimeOffset());
                    Assert.Equal(Instant(selections[index].End), body.RootElement.GetProperty("end").GetDateTimeOffset());
                }
                else
                {
                    Assert.Equal(HttpStatusCode.Conflict, responses[index].StatusCode);
                    Assert.Equal("application/problem+json", responses[index].Content.Headers.ContentType?.MediaType);
                    Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
                    Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
                    Assert.All(body.RootElement.EnumerateObject(), property =>
                        Assert.Contains(property.Name, new[] { "status", "title", "detail", "type", "instance" }));
                }
            }

            var committed = store.Bookings.ToArray();
            for (var left = 0; left < committed.Length; left++)
            {
                for (var right = left + 1; right < committed.Length; right++)
                {
                    Assert.False(committed[left].Start < committed[right].End && committed[right].Start < committed[left].End,
                        "Committed owner-wide bookings must not overlap.");
                }
            }
            var saved = Assert.Single(committed);
            Assert.False(string.IsNullOrWhiteSpace(saved.Id));
            Assert.Equal(selections[winner].Type, saved.EventTypeId);
            Assert.Equal(Instant(selections[winner].Start), saved.Start);
            Assert.Equal(Instant(selections[winner].End), saved.End);
            Assert.Equal($"Guest {winner}", saved.GuestName);
            Assert.Equal($"guest{winner}@example.com", saved.GuestEmail);
            using var readScope = factory.Services.CreateScope();
            var persisted = readScope.ServiceProvider.GetRequiredService<IBookingRepository>().List();
            Assert.Equal(committed, persisted);
        }
        catch (Exception error)
        {
            testFailure = error;
            throw;
        }
        finally
        {
            // Always unblock the server before host/probe disposal, even on failure.
            contention.Release();
            try
            {
                await Task.WhenAll(requests).WaitAsync(Timeout);
            }
            catch (Exception) when (testFailure is not null)
            {
                // A failed request or cleanup timeout must not hide the original
                // assertion/contending-operation failure. Dispose every response below.
            }
            finally
            {
                foreach (var request in requests)
                {
                    // Also dispose responses that arrive after a cleanup timeout.
                    _ = request.ContinueWith(completed =>
                    {
                        if (completed.IsCompletedSuccessfully)
                        {
                            completed.Result.Dispose();
                        }
                        else
                        {
                            _ = completed.Exception;
                        }
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
        }

        Task<HttpResponseMessage> Post(int index) => client.PostAsJsonAsync("/api/bookings", new
        {
            eventTypeId = selections[index].Type,
            start = selections[index].Start,
            name = $"Guest {index}",
            email = $"guest{index}@example.com"
        });
    }

    private sealed record Selection(string Type, string Start, string End);

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value);

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Instant("2026-10-14T08:00:00Z");
    }

    private sealed class ContentionProbe(int competitors) : ICalendarWriteObserver, IDisposable
    {
        private readonly TaskCompletionSource _firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allContended = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _release = new();
        private int _entries;
        private int _contentions;

        public Task FirstEntered => _firstEntered.Task;
        public Task AllCompetitorsContended => _allContended.Task;

        public void Contended()
        {
            if (Interlocked.Increment(ref _contentions) == competitors)
            {
                _allContended.TrySetResult();
            }
        }

        public void Entered()
        {
            if (Interlocked.Increment(ref _entries) == 1)
            {
                _firstEntered.TrySetResult();
                if (!_release.Wait(Timeout))
                {
                    throw new TimeoutException("Test did not release the protected booking operation.");
                }
            }
        }

        public void Release() => _release.Set();

        public void Dispose() => _release.Dispose();
    }
}
