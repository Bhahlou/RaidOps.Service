using FluentAssertions;
using RaidOps.Application.Contracts.Specs.Responses;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot;

public class ApplicationEmojiManifestTests
{
    // ── ClassIcons ────────────────────────────────────────────────────────────

    [Fact]
    public void ClassIcons_ReturnsOneEntryPerWowClass()
    {
        var result = ApplicationEmojiManifest.ClassIcons("https://cdn.example.com/classes/").ToList();

        result.Should().HaveCount(WowClassEmojiNames.ByClassId.Count);
    }

    [Fact]
    public void ClassIcons_BuildsUrlByStrippingTheClassPrefixAndAppendingJpg()
    {
        var result = ApplicationEmojiManifest.ClassIcons("https://cdn.example.com/classes/").ToList();

        result.Should().ContainSingle(e => e.Name == "class_warrior" && e.SourceUrl == "https://cdn.example.com/classes/warrior.jpg");
        result.Should().ContainSingle(e => e.Name == "class_deathknight" && e.SourceUrl == "https://cdn.example.com/classes/deathknight.jpg");
    }

    [Fact]
    public void ClassIcons_NamesMatchWowClassEmojiNamesValues()
    {
        var result = ApplicationEmojiManifest.ClassIcons("https://cdn.example.com/classes/").Select(e => e.Name);

        result.Should().BeEquivalentTo(WowClassEmojiNames.ByClassId.Values);
    }

    // ── SpecIcons ─────────────────────────────────────────────────────────────

    [Fact]
    public void SpecIcons_EmptyInput_ReturnsEmpty()
    {
        var result = ApplicationEmojiManifest.SpecIcons([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SpecIcons_SkipsSpecsWithNoIconUrlSyncedYet()
    {
        var specs = new List<SpecDto>
        {
            new() { Id = 71, Name = "Arms", ClassId = 1, IconUrl = null },
            new() { Id = 72, Name = "Fury", ClassId = 1, IconUrl = "https://cdn.example.com/specs/fury.jpg" },
        };

        var result = ApplicationEmojiManifest.SpecIcons(specs).ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be(WowSpecEmojiNames.GetName(1, "Fury"));
        result[0].SourceUrl.Should().Be("https://cdn.example.com/specs/fury.jpg");
    }

    [Fact]
    public void SpecIcons_NameCombinesClassSlugAndSlugifiedSpecName()
    {
        var specs = new List<SpecDto>
        {
            new() { Id = 253, Name = "Beast Mastery", ClassId = 3, IconUrl = "https://cdn.example.com/specs/bm.jpg" },
        };

        var result = ApplicationEmojiManifest.SpecIcons(specs).ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("spec_hunter_beastmastery");
    }

    [Fact]
    public void SpecIcons_MultipleSyncedSpecs_ReturnsOnePerSpec()
    {
        var specs = new List<SpecDto>
        {
            new() { Id = 71, Name = "Arms", ClassId = 1, IconUrl = "https://cdn.example.com/specs/arms.jpg" },
            new() { Id = 62, Name = "Arcane", ClassId = 8, IconUrl = "https://cdn.example.com/specs/arcane.jpg" },
        };

        var result = ApplicationEmojiManifest.SpecIcons(specs).ToList();

        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Name == "spec_warrior_arms" && e.SourceUrl == "https://cdn.example.com/specs/arms.jpg");
        result.Should().Contain(e => e.Name == "spec_mage_arcane" && e.SourceUrl == "https://cdn.example.com/specs/arcane.jpg");
    }
}
