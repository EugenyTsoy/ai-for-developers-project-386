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
    /// and step duration.
    /// </summary>
    /// <param name="opening">Opening UTC instant of the work day.</param>
    /// <param name="closing">Closing UTC instant of the work day.</param>
    /// <param name="step">Step anchored to <paramref name="opening"/>.</param>
    public static IReadOnlyList<Instant> GenerateOpeningInstants(
        Instant opening,
        Instant closing,
        PositiveDuration step)
    {
        var span = closing.Utc - opening.Utc;
        var totalMinutes = span.TotalMinutes;
        if (totalMinutes <= 0)
        {
            return Array.Empty<Instant>();
        }

        var fits = (int)(totalMinutes / step.Minutes);
        if (fits <= 0)
        {
            return Array.Empty<Instant>();
        }

        var result = new Instant[fits];
        var stride = step.AsTimeSpan();
        for (var k = 0; k < fits; k++)
        {
            result[k] = new Instant(opening.Utc.Add(stride * k));
        }

        return result;
    }
}
