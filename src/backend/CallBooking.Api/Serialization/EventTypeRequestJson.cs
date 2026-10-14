using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CallBooking.Api.Generated;

namespace CallBooking.Api.Serialization;

internal static class EventTypeRequestJson
{
    public static void Configure(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(CreateEventTypeRequest))
        {
            return;
        }

        // Derive wire types and required-field handling from generated metadata,
        // not a second field-name/schema table. MVC's web defaults otherwise
        // accept quoted numbers, and nullable strings accept explicit JSON null.
        typeInfo.NumberHandling = JsonNumberHandling.Strict;
        foreach (var property in typeInfo.Properties.Where(property => property.PropertyType == typeof(string)))
        {
            property.CustomConverter = new SuppliedStringConverter();
        }
    }

    private sealed class SuppliedStringConverter : JsonConverter<string>
    {
        public override bool HandleNull => true;

        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("A supplied string property must be a JSON string.");
            }

            return reader.GetString()!;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
