using CallBooking.Application.Ports;
using CallBooking.Domain.Entities;
using CallBooking.Infrastructure.InMemory;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CallBooking.Api.Tests;

/// <summary>
/// Storage seam behaviour tests for TK2. Proves that the in-memory
/// calendar store:
/// <list type="bullet">
/// <item>persists data and makes it visible to subsequent reads
/// through the same host;</item>
/// <item>assigns and keeps stable ids across multiple reads, including
/// for entities with identical names;</item>
/// <item>keeps absolute UTC instants verbatim regardless of any
/// later timezone change;</item>
/// <item>does not leak state between tests (no shared mutable state).</item>
/// </list>
/// Per the approved ticket the assertions are made through the
/// test-host internal seam (IServiceProvider) and through the
/// application-layer repository ports, never against any internal
/// storage schema or call-order invariant.
/// </summary>
public class StorageSeamTests
{
    [Fact]
    [Trait("Category", "Storage")]
    public void Save_then_read_returns_same_event_type_with_stable_id()
    {
        using var host = new StorageHost();
        var saved = new EventType(
            id: "et-001",
            name: "Intro call",
            description: "First conversation",
            durationMinutes: 30);

        host.EventTypes.Add(saved);

        Assert.Same(saved, host.EventTypes.GetById("et-001"));
        Assert.Same(saved, host.Store.EventTypes.Single());
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void EventType_with_same_name_keeps_distinct_stable_ids()
    {
        using var host = new StorageHost();

        var first = new EventType(
            id: "et-001",
            name: "Discovery",
            description: null,
            durationMinutes: 30);

        var second = new EventType(
            id: "et-002",
            name: "Discovery",
            description: "With description",
            durationMinutes: 45);

        host.EventTypes.Add(first);
        host.EventTypes.Add(second);

        var all = host.EventTypes.List();
        Assert.Equal(2, all.Count);
        Assert.Contains(first, all);
        Assert.Contains(second, all);

        Assert.Same(first, host.EventTypes.GetById("et-001"));
        Assert.Same(second, host.EventTypes.GetById("et-002"));
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void Save_then_read_returns_same_booking_with_stable_id()
    {
        using var host = new StorageHost();
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var saved = new Booking(
            id: "bk-001",
            eventTypeId: "et-001",
            start: start,
            end: start.AddMinutes(30),
            guestName: "Alice",
            guestEmail: "alice@example.com");

        host.Bookings.Add(saved);

        Assert.Same(saved, host.Bookings.GetById("bk-001"));
        Assert.Same(saved, host.Bookings.List().Single());
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void Booking_preserves_trimmed_guest_contact_after_storage()
    {
        using var host = new StorageHost();
        var start = new DateTimeOffset(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
        var saved = new Booking(
            id: "bk-002",
            eventTypeId: "et-001",
            start: start,
            end: start.AddMinutes(60),
            guestName: "  Alice Liddell  ",
            guestEmail: "  alice@example.com  ");

        host.Bookings.Add(saved);

        var read = host.Bookings.GetById("bk-002");
        Assert.NotNull(read);
        Assert.Equal("Alice Liddell", read!.GuestName);
        Assert.Equal("alice@example.com", read.GuestEmail);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void Saved_instant_is_read_back_unchanged_regardless_of_offset()
    {
        using var host = new StorageHost();
        var stored = new DateTimeOffset(
            new DateTime(2026, 7, 4, 17, 30, 0, DateTimeKind.Utc),
            TimeSpan.Zero);
        var saved = new Booking(
            id: "bk-003",
            eventTypeId: "et-001",
            start: stored,
            end: stored.AddMinutes(15),
            guestName: "Bob",
            guestEmail: "bob@example.com");

        host.Bookings.Add(saved);

        var read = host.Bookings.GetById("bk-003");
        Assert.NotNull(read);
        Assert.Equal(stored, read!.Start);
        Assert.Equal(stored.Offset, read.Start.Offset);
        Assert.Equal(stored.UtcDateTime, read.Start.UtcDateTime);

        // Confirm directly via the storage seam: the persisted value
        // equals the original absolute instant byte-for-byte.
        var fromStore = host.Store.Bookings.Single();
        Assert.Equal(stored.UtcDateTime, fromStore.Start.UtcDateTime);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void Reading_via_test_host_after_save_returns_persisted_data()
    {
        using var host = new StorageHost();
        var saved = new EventType(
            id: "et-003",
            name: "Follow-up",
            description: "Continuing an earlier conversation",
            durationMinutes: 15);
        host.EventTypes.Add(saved);

        var reread = host.EventTypes.GetById("et-003");
        Assert.NotNull(reread);
        Assert.Equal("et-003", reread!.Id);
        Assert.Equal("Follow-up", reread.Name);
        Assert.Equal("Continuing an earlier conversation", reread.Description);
        Assert.Equal(15, reread.DurationMinutes);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void Separate_host_instances_do_not_share_persisted_state()
    {
        using var first = new StorageHost();
        using var second = new StorageHost();

        // Sanity: distinct host instances must resolve distinct singletons.
        Assert.NotSame(first.Store, second.Store);

        first.EventTypes.Add(new EventType(
            id: "et-001",
            name: "First",
            description: null,
            durationMinutes: 30));

        // First host keeps its data, second host stays empty.
        Assert.Single(first.EventTypes.List());
        Assert.Empty(second.EventTypes.List());

        // Second host's writes must not bleed into the first host.
        second.EventTypes.Add(new EventType(
            id: "et-002",
            name: "Second",
            description: null,
            durationMinutes: 45));

        // Each host must keep its own data only: first doesn't see
        // second's write, second doesn't see first's data.
        Assert.Single(first.EventTypes.List());
        Assert.Equal("et-001", first.EventTypes.List().Single().Id);
        Assert.Single(second.EventTypes.List());
        Assert.Equal("et-002", second.EventTypes.List().Single().Id);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void Reset_seam_clears_both_event_types_and_bookings()
    {
        using var host = new StorageHost();
        var start = new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero);
        host.EventTypes.Add(new EventType(
            id: "et-100",
            name: "Demo",
            description: null,
            durationMinutes: 30));
        host.Bookings.Add(new Booking(
            id: "bk-100",
            eventTypeId: "et-100",
            start: start,
            end: start.AddMinutes(30),
            guestName: "Carol",
            guestEmail: "carol@example.com"));

        Assert.Single(host.EventTypes.List());
        Assert.Single(host.Bookings.List());

        host.Store.Reset();

        Assert.Empty(host.EventTypes.List());
        Assert.Empty(host.Bookings.List());
        Assert.Empty(host.Store.EventTypes);
        Assert.Empty(host.Store.Bookings);
    }

    [Fact]
    [Trait("Category", "Storage")]
    public void Saved_data_survives_subsequent_reads_in_same_host()
    {
        using var host = new StorageHost();
        var start = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
        host.Bookings.Add(new Booking(
            id: "bk-stable",
            eventTypeId: "et-stable",
            start: start,
            end: start.AddMinutes(45),
            guestName: "Dana",
            guestEmail: "dana@example.com"));

        var first = host.Bookings.GetById("bk-stable");
        var second = host.Bookings.GetById("bk-stable");
        var listRead = host.Bookings.List().Single();

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Same(first, listRead);
    }

    /// <summary>
    /// Per-test storage seam. Builds a fresh
    /// <see cref="WebApplicationFactory{TEntryPoint}"/> so that
    /// singleton state is never shared between tests, and creates a
    /// request scope for scoped repository access.
    /// </summary>
    private sealed class StorageHost : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly IServiceScope _scope;

        public StorageHost()
        {
            _factory = new WebApplicationFactory<Program>();
            _scope = _factory.Services.CreateScope();
        }

        public IInMemoryCalendarStore Store =>
            _factory.Services.GetRequiredService<IInMemoryCalendarStore>();

        public IEventTypeRepository EventTypes =>
            _scope.ServiceProvider.GetRequiredService<IEventTypeRepository>();

        public IBookingRepository Bookings =>
            _scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        public void Dispose()
        {
            _scope.Dispose();
            _factory.Dispose();
        }
    }
}
