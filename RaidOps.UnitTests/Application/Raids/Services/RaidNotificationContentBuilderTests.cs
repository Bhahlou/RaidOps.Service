using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidNotificationContentBuilderTests
{
    private readonly Mock<IGuildsRepository> _guilds = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IEmojiService> _emojiService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly RaidNotificationContentBuilder _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "42";

    private static readonly DateTime EventStartsAtUtc = new(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc);

    public RaidNotificationContentBuilderTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _discordBotService.Setup(d => d.Emojis).Returns(_emojiService.Object);
        _sut = new RaidNotificationContentBuilder(_guilds.Object, _discordBotService.Object);

        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Language = "en", Timezone = "Europe/Paris" });
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Returns((NetCord.GuildUser?)null);
    }

    private static RaidEvent MakeEvent() => new() { Id = 5, GuildId = GuildId, Name = "Split 1", StartsAtUtc = EventStartsAtUtc };

    // ── GetGuildLanguageAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetGuildLanguageAsync_GuildHasLanguage_ReturnsIt()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Language = "fr" });

        var result = await _sut.GetGuildLanguageAsync(GuildId);

        result.Should().Be("fr");
    }

    [Fact]
    public async Task GetGuildLanguageAsync_GuildLanguageUnset_ReturnsEnglish()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Language = null });

        var result = await _sut.GetGuildLanguageAsync(GuildId);

        result.Should().Be("en");
    }

    [Fact]
    public async Task GetGuildLanguageAsync_GuildNotFound_ReturnsEnglish()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.GetGuildLanguageAsync(GuildId);

        result.Should().Be("en");
    }

    // ── BuildPublishedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task BuildPublishedAsync_ReturnsLocalizedTitleColorDescriptionAndStartsField()
    {
        var embed = await _sut.BuildPublishedAsync(GuildId, RequesterId, MakeEvent());

        embed.Title.Should().Be("Raid published");
        embed.ColorHex.Should().Be(0x57F287);
        embed.Description.Should().Be("<@42> published **Split 1**.");
        embed.Fields.Should().ContainSingle(f => f.Name == "Starts" && f.Value == "2/1/2026 at 21:00");
    }

    [Fact]
    public async Task BuildPublishedAsync_MemberFound_PopulatesAuthor()
    {
        var member = NetCordTestHelpers.MakeGuildUser(42, 1, [], username: "arthas", nickname: "Le Roi Liche", guildAvatarHash: "hash123");
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Returns(member);

        var embed = await _sut.BuildPublishedAsync(GuildId, RequesterId, MakeEvent());

        embed.Author.Should().NotBeNull();
        embed.Author!.Name.Should().Be("Le Roi Liche");
        embed.Author.IconUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildPublishedAsync_GuildServiceThrowsInvalidOperationException_AuthorIsNull()
    {
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Throws<InvalidOperationException>();

        var embed = await _sut.BuildPublishedAsync(GuildId, RequesterId, MakeEvent());

        embed.Author.Should().BeNull();
    }

    [Fact]
    public async Task BuildPublishedAsync_GuildNotFound_FallsBackToEnglishAndUnshiftedUtcTime()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var embed = await _sut.BuildPublishedAsync(GuildId, RequesterId, MakeEvent());

        embed.Title.Should().Be("Raid published");
        embed.Description.Should().Be("<@42> published **Split 1**.");
        embed.Fields.Should().ContainSingle(f => f.Name == "Starts" && f.Value == "2/1/2026 at 20:00");
    }

    [Fact]
    public async Task BuildPublishedAsync_GuildTimezoneUnset_FormatsStartsFieldAsUnshiftedUtcTime()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Language = "en", Timezone = null });

        var embed = await _sut.BuildPublishedAsync(GuildId, RequesterId, MakeEvent());

        embed.Fields.Should().ContainSingle(f => f.Name == "Starts" && f.Value == "2/1/2026 at 20:00");
    }

    // ── BuildCancelledAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task BuildCancelledAsync_ReturnsLocalizedTitleColorAndDescriptionNoFields()
    {
        var embed = await _sut.BuildCancelledAsync(GuildId, RequesterId, MakeEvent());

        embed.Title.Should().Be("Raid cancelled");
        embed.ColorHex.Should().Be(0xED4245);
        embed.Description.Should().Be("<@42> cancelled **Split 1**.");
        embed.Fields.Should().BeNull();
    }

    // ── BuildRescheduledAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task BuildRescheduledAsync_ReturnsDescriptionWithOldAndNewLocalTimes()
    {
        var oldStartsAtUtc = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc);

        var embed = await _sut.BuildRescheduledAsync(GuildId, RequesterId, MakeEvent(), oldStartsAtUtc);

        embed.Title.Should().Be("Raid rescheduled");
        embed.ColorHex.Should().Be(0xFEE75C);
        embed.Description.Should().Be("<@42> rescheduled **Split 1**: 1/15/2026 at 19:00 → 2/1/2026 at 21:00.");
    }

    [Fact]
    public async Task BuildRescheduledAsync_GuildTimezoneUnset_FormatsBothTimesAsUnshiftedUtc()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Language = "en", Timezone = null });
        var oldStartsAtUtc = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc);

        var embed = await _sut.BuildRescheduledAsync(GuildId, RequesterId, MakeEvent(), oldStartsAtUtc);

        embed.Description.Should().Be("<@42> rescheduled **Split 1**: 1/15/2026 at 18:00 → 2/1/2026 at 20:00.");
    }

    // ── BuildSlotAssignedAsync ────────────────────────────────────────────────

    [Fact]
    public async Task BuildSlotAssignedAsync_CharacterWithClassAndSpec_IncludesBothIconsInDescription()
    {
        _emojiService.Setup(e => e.GetMarkdown("class_deathknight")).Returns("<:class_deathknight:1>");
        _emojiService.Setup(e => e.GetMarkdown("spec_deathknight_blood")).Returns("<:spec_deathknight_blood:2>");
        var character = new RaidCharacterRef("Arthas", 6, "Blood");

        var embed = await _sut.BuildSlotAssignedAsync(GuildId, RequesterId, MakeEvent(), character, new SlotCoordinate(2, 3));

        embed.Title.Should().Be("Character added");
        embed.ColorHex.Should().Be(0x5865F2);
        embed.Description.Should().Be("<@42> assigned <:class_deathknight:1><:spec_deathknight_blood:2> **Arthas** (group 2, slot 3) in **Split 1**.");
    }

    [Fact]
    public async Task BuildSlotAssignedAsync_CharacterWithUnsyncedIcons_FallsBackToPlainBoldName()
    {
        // ClassId/SpecName resolvable but IEmojiService.GetMarkdown returns null (not synced yet).
        var character = new RaidCharacterRef("Arthas", 6, "Blood");

        var embed = await _sut.BuildSlotAssignedAsync(GuildId, RequesterId, MakeEvent(), character, new SlotCoordinate(2, 3));

        embed.Description.Should().Be("<@42> assigned **Arthas** (group 2, slot 3) in **Split 1**.");
    }

    [Fact]
    public async Task BuildSlotAssignedAsync_CharacterWithNoClassId_OmitsBothIcons()
    {
        var character = new RaidCharacterRef("Unknown", null, "Blood");

        var embed = await _sut.BuildSlotAssignedAsync(GuildId, RequesterId, MakeEvent(), character, new SlotCoordinate(1, 1));

        embed.Description.Should().Be("<@42> assigned **Unknown** (group 1, slot 1) in **Split 1**.");
    }

    // ── BuildSlotUnassignedAsync ──────────────────────────────────────────────

    [Fact]
    public async Task BuildSlotUnassignedAsync_ReturnsLocalizedTitleAndDescription()
    {
        var character = new RaidCharacterRef("Arthas", null);

        var embed = await _sut.BuildSlotUnassignedAsync(GuildId, RequesterId, MakeEvent(), character, new SlotCoordinate(4, 5));

        embed.Title.Should().Be("Character removed");
        embed.Description.Should().Be("<@42> unassigned **Arthas** (group 4, slot 5) from **Split 1**.");
    }

    // ── BuildSlotsSwappedAsync ────────────────────────────────────────────────

    [Fact]
    public async Task BuildSlotsSwappedAsync_ReturnsLocalizedTitleAndDescriptionWithBothCharacters()
    {
        var characterA = new RaidCharacterRef("Arthas", null);
        var characterB = new RaidCharacterRef("Jaina", null);

        var embed = await _sut.BuildSlotsSwappedAsync(GuildId, RequesterId, MakeEvent(), characterA, characterB);

        embed.Title.Should().Be("Characters swapped");
        embed.Description.Should().Be("<@42> swapped **Arthas** and **Jaina** in **Split 1**.");
    }

    // ── BuildSlotSpecChangedAsync ─────────────────────────────────────────────

    [Fact]
    public async Task BuildSlotSpecChangedAsync_CharacterShowsOnlyClassIcon_SpecsShowOwnSpecIcons()
    {
        _emojiService.Setup(e => e.GetMarkdown("class_deathknight")).Returns("<:class_deathknight:1>");
        _emojiService.Setup(e => e.GetMarkdown("spec_deathknight_blood")).Returns("<:spec_deathknight_blood:2>");
        _emojiService.Setup(e => e.GetMarkdown("spec_deathknight_frost")).Returns("<:spec_deathknight_frost:3>");
        // SpecName deliberately left null — the character mention shows only the class icon.
        var character = new RaidCharacterRef("Arthas", 6);

        var embed = await _sut.BuildSlotSpecChangedAsync(GuildId, RequesterId, MakeEvent(), character, "Blood", "Frost");

        embed.Title.Should().Be("Slot spec changed");
        embed.Description.Should().Be(
            "<@42> changed <:class_deathknight:1> **Arthas**'s spec from <:spec_deathknight_blood:2> **Blood** to <:spec_deathknight_frost:3> **Frost** in **Split 1**.");
    }

    [Fact]
    public async Task BuildSlotSpecChangedAsync_CharacterWithNoClassId_SpecLabelsHaveNoIconEither()
    {
        var character = new RaidCharacterRef("Unknown", null);

        var embed = await _sut.BuildSlotSpecChangedAsync(GuildId, RequesterId, MakeEvent(), character, "Blood", "Frost");

        embed.Description.Should().Be("<@42> changed **Unknown**'s spec from **Blood** to **Frost** in **Split 1**.");
    }
}
