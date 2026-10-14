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

        return instant;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
}
