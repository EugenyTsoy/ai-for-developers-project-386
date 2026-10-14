namespace CallBooking.Domain;

/// <summary>
/// Canonical slot-grid generation. Each candidate occupies its full
/// duration inside the working window. Trailing partial slots are
/// dropped. The grid is anchored to the opening instant, not to
/// wall-clock "now".
/// </summary>
public static class SlotGrid
{
    /// <summary>
    /// Enumerate the canonical slot starts for the given working window
    /// and step (in minutes).
    /// </summary>
    /// <param name="opening">Opening UTC instant of the work day.</param>
    /// <param name="closing">Closing UTC instant of the work day.</param>
    /// <param name="stepMinutes">Step in minutes; must be strictly positive.</param>
    public static IReadOnlyList<Instant> GenerateOpeningInstants(
        Instant opening,
        Instant closing,
        int stepMinutes)
    {
        if (stepMinutes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stepMinutes),
                "Step must be at least one minute.");
        }

        var span = closing.Utc - opening.Utc;
        var totalMinutes = span.TotalMinutes;
        if (totalMinutes <= 0)
        {
            return Array.Empty<Instant>();
        }

        var fits = (int)(totalMinutes / stepMinutes);
        if (fits <= 0)
        {
            return Array.Empty<Instant>();
        }

        var result = new Instant[fits];
        for (var k = 0; k < fits; k++)
        {
            var delta = TimeSpan.FromMinutes((double)k * stepMinutes);
            result[k] = new Instant(opening.Utc.Add(delta));
        }

        return result;
    }
}
