namespace CallBooking.Domain.Tests;

public class EventTypeTests
{
    [Fact]
    public void EventTypeId_rejects_blank_value()
    {
        Assert.Throws<ArgumentException>(() => EventTypeId.FromServer("   "));
    }

    [Fact]
    public void EventTypeId_trims_surrounding_whitespace()
    {
        var id = EventTypeId.FromServer("  evt_1  ");
        Assert.Equal("evt_1", id.Value);
    }

    [Fact]
    public void PositiveDuration_rejects_non_positive_minutes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PositiveDuration(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PositiveDuration(-5));
    }

    [Fact]
    public void EventType_accepts_required_name_and_valid_duration()
    {
        var entity = new EventType(
            EventTypeId.FromServer("evt_1"),
            "Discovery call",
            description: null,
            durationMinutes: new PositiveDuration(30));

        Assert.Equal("Discovery call", entity.Name);
        Assert.Null(entity.Description);
        Assert.Equal(30, entity.DurationMinutes.Minutes);
    }

    [Fact]
    public void EventType_keeps_optional_description_when_supplied()
    {
        var entity = new EventType(
            EventTypeId.FromServer("evt_1"),
            "Discovery call",
            description: "First conversation",
            durationMinutes: new PositiveDuration(45));

        Assert.Equal("First conversation", entity.Description);
    }

    [Fact]
    public void Duplicate_names_are_allowed_as_values()
    {
        var first = new EventType(
            EventTypeId.FromServer("evt_1"),
            "Discovery",
            description: null,
            durationMinutes: new PositiveDuration(15));
        var second = new EventType(
            EventTypeId.FromServer("evt_2"),
            "Discovery",
            description: null,
            durationMinutes: new PositiveDuration(15));

        Assert.Equal(first.Name, second.Name);
        Assert.NotEqual(first.Id, second.Id);
    }
}
