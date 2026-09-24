using FluentAssertions;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools;
using System.Text.Json;

namespace RaidOps.UnitTests.ExternalApplication.Contracts.WagoTools;

/// <summary>Unit tests for <see cref="WagoDateTimeConverter"/>.</summary>
public class WagoDateTimeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new WagoDateTimeConverter() } };

    [Fact]
    public void Read_WagoFormat_ReturnsUtcDateTime()
    {
        var result = JsonSerializer.Deserialize<DateTime>("\"2026-09-22 14:18:01\"", Options);

        result.Should().Be(new DateTime(2026, 9, 22, 14, 18, 1, DateTimeKind.Utc));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Read_Iso8601Format_ThrowsFormatException()
    {
        var act = () => JsonSerializer.Deserialize<DateTime>("\"2026-09-22T14:18:01Z\"", Options);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Write_UtcDateTime_WritesWagoFormat()
    {
        var json = JsonSerializer.Serialize(new DateTime(2026, 9, 22, 14, 18, 1, DateTimeKind.Utc), Options);

        json.Should().Be("\"2026-09-22 14:18:01\"");
    }

    [Fact]
    public void WriteThenRead_RoundTripsTheSameInstant()
    {
        var original = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var roundTripped = JsonSerializer.Deserialize<DateTime>(JsonSerializer.Serialize(original, Options), Options);

        roundTripped.Should().Be(original);
        roundTripped.Kind.Should().Be(DateTimeKind.Utc);
    }
}
