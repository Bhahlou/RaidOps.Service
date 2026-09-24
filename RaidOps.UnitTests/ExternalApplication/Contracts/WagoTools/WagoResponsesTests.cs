using FluentAssertions;
using RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;
using System.Text.Json;

namespace RaidOps.UnitTests.ExternalApplication.Contracts.WagoTools;

/// <summary>Unit tests for the wago.tools response DTOs' JSON contract.</summary>
public class WagoResponsesTests
{
    // Real shape of one /api/builds/latest entry, including keys the app doesn't model.
    private const string BuildJson = """
        {
          "product": "wow_classic_beta",
          "version": "1.60.1.69977",
          "created_at": "2026-09-22 14:18:01",
          "build_config": "abcdef",
          "cdn_config": "123456",
          "is_bad": false
        }
        """;

    [Fact]
    public void WagoBuildInfo_RealApiShape_DeserializesModeledKeysAndIgnoresExtras()
    {
        var build = JsonSerializer.Deserialize<WagoBuildInfo>(BuildJson)!;

        build.Product.Should().Be("wow_classic_beta");
        build.Version.Should().Be("1.60.1.69977");
        build.CreatedAt.Should().Be(new DateTime(2026, 9, 22, 14, 18, 1, DateTimeKind.Utc));
        build.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void WagoBuildInfo_Serialized_UsesWagoKeysAndDateFormat()
    {
        var json = JsonSerializer.Serialize(new WagoBuildInfo
        {
            Product = "wow",
            Version = "12.0.1.1",
            CreatedAt = new DateTime(2026, 9, 22, 14, 18, 1, DateTimeKind.Utc),
        });

        json.Should().Contain("\"product\":\"wow\"")
            .And.Contain("\"version\":\"12.0.1.1\"")
            .And.Contain("\"created_at\":\"2026-09-22 14:18:01\"");
    }

    [Fact]
    public void WagoBuildInfo_Defaults_AreEmpty()
    {
        var build = new WagoBuildInfo();

        build.Product.Should().BeEmpty();
        build.Version.Should().BeEmpty();
    }

    [Fact]
    public void WagoFileInfoResponse_Deserializes_FilenameKey()
    {
        var info = JsonSerializer.Deserialize<WagoFileInfoResponse>("""{"filename":"interface/icons/inv_misc_food_59.blp","other":1}""")!;

        info.Filename.Should().Be("interface/icons/inv_misc_food_59.blp");
        new WagoFileInfoResponse().Filename.Should().BeEmpty();
    }

    [Fact]
    public void WagoSpellName_HoldsIdAndName()
    {
        var spell = new WagoSpellName { Id = 133, Name = "Fireball" };

        spell.Id.Should().Be(133);
        spell.Name.Should().Be("Fireball");
    }
}
