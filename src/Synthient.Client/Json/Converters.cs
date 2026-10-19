using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synthient;

// Reads a Unix timestamp, in milliseconds when it is at least 1e12 and in seconds otherwise, or an
// RFC 3339 string. Some streams send milliseconds, and API key creation times are RFC 3339.
internal sealed class TimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.TryGetDateTimeOffset(out var date) ? date : throw new JsonException("Invalid RFC 3339 timestamp.");
        }
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out var value))
        {
            throw new JsonException("Expected a Unix timestamp.");
        }
        try
        {
            return value >= 1_000_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                : DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new JsonException("Unix timestamp out of range.", e);
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.ToUnixTimeSeconds());
}

internal sealed class SecondsConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out var seconds))
        {
            throw new JsonException("Expected a number of seconds.");
        }
        if (seconds > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond || seconds < TimeSpan.MinValue.Ticks / TimeSpan.TicksPerSecond)
        {
            throw new JsonException("Duration out of range.");
        }
        return TimeSpan.FromTicks(seconds * TimeSpan.TicksPerSecond);
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Ticks / TimeSpan.TicksPerSecond);
}

internal sealed class IPAddressConverter : JsonConverter<IPAddress>
{
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && IPAddress.TryParse(reader.GetString(), out var address)
            ? address
            : throw new JsonException("Invalid IP address.");

    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
