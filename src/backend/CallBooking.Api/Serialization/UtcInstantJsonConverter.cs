using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CallBooking.Api.Serialization;

/// <summary>Formats response instants as UTC-Z; leaves default input parsing unchanged.</summary>
internal sealed class UtcInstantJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
}
