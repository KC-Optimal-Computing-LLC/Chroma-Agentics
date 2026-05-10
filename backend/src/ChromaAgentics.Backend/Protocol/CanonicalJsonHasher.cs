using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChromaAgentics.Backend.Protocol;

public static class CanonicalJsonHasher
{
    public static string ComputeSha256(JsonElement payload)
    {
        var canonicalJson = ToCanonicalJson(payload);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ToCanonicalJson(JsonElement payload)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalValue(writer, payload);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalValue(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;

            case JsonValueKind.Number:
                WriteCanonicalNumber(writer, value);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                throw new InvalidOperationException("Unsupported JSON value kind for canonical hashing.");
        }
    }

    private static void WriteCanonicalNumber(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.TryGetInt64(out var integer))
        {
            writer.WriteNumberValue(integer);
            return;
        }

        if (value.TryGetDecimal(out var decimalValue))
        {
            writer.WriteRawValue(decimalValue.ToString("G29", CultureInfo.InvariantCulture), skipInputValidation: true);
            return;
        }

        if (value.TryGetDouble(out var doubleValue) && !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue))
        {
            writer.WriteRawValue(doubleValue.ToString("G17", CultureInfo.InvariantCulture), skipInputValidation: true);
            return;
        }

        throw new InvalidOperationException("Unsupported JSON number for canonical hashing.");
    }
}
