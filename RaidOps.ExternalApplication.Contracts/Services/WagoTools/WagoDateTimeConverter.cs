using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.WagoTools;

/// <summary>
/// wago.tools returns build timestamps as <c>"yyyy-MM-dd HH:mm:ss"</c> (UTC, no offset) rather than
/// ISO 8601, which <see cref="System.Text.Json"/>'s default <see cref="DateTime"/> converter can't
/// parse.
/// </summary>
public class WagoDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-dd HH:mm:ss";

    /// <inheritdoc/>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.SpecifyKind(
            DateTime.ParseExact(reader.GetString()!, Format, CultureInfo.InvariantCulture),
            DateTimeKind.Utc);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
