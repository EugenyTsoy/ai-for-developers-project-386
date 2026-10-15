using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CallBooking.Api.Serialization;

/// <summary>Enforces UTC-Z on the wire before parsing and formats responses as UTC-Z.</summary>
internal sealed class UtcInstantJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String
            || reader.GetString() is not { } value || !value.EndsWith('Z')
            || !reader.TryGetDateTimeOffset(out var instant))
        {
            throw new JsonException("A UTC timestamp with a Z suffix is required.");
        }

        // System.Text.Json truncates fractional digits beyond the seven tick digits.
        // Extra zeroes are exact; any nonzero remainder would change the requested instant.
        var decimalPoint = value.IndexOf('.');
        if (decimalPoint >= 0)
        {
            var fraction = value.AsSpan(decimalPoint + 1, value.Length - decimalPoint - 2);
            if (fraction.IsEmpty)
            {
                throw new JsonException("A timestamp decimal point must be followed by fractional digits.");
            }

            if (fraction.Length > 7 && fraction[7..].IndexOfAnyExcept('0') >= 0)
            {
                throw new JsonException("The UTC timestamp must be representable without loss of precision.");
            }
        }

        return instant;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
}
