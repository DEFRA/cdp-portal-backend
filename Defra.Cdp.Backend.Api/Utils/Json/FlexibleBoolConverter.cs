namespace Defra.Cdp.Backend.Api.Utils.Json;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/**
 * Handles reading booleans that might be quoted strings instead of json booleans. 
 */
public class FlexibleBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True)
            return true;
        if (reader.TokenType == JsonTokenType.False)
            return false;

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (bool.TryParse(stringValue, out var result))
                return result;
        }

        throw new JsonException($"Unable to convert value to bool. TokenType: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}