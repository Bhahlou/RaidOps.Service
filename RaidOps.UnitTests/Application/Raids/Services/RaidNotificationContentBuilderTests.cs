using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidNotificationContentBuilderTests
{
    private readonly Mock<IGuildsRepository> _guilds = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IBranchRepository> _branchRepository = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IEmojiService> _emojiService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly RaidNotificationContentBuilder _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "42";

    private static readonly DateTime EventStartsAtUtc = new(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc);

    public RaidNotificationContentBuilderTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _discordBotService.Setup(d => d.Emojis).Returns(_emojiService.Object);
        _sut = new RaidNotificationContentBuilder(_guilds.Object, _guildBranchesRepository.Object, _branchRepository.Object, _discordBotService.Object, _configuration.Object);

        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Language = "en", Timezone = "Europe/Paris" });
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Returns((NetCord.GuildUser?)null);
    }

    private static RaidEvent MakeEvent() => new() { Id = 5, GuildId = GuildId, Name = "Split 1", StartsAtUtc = EventStartsAtUtc };

    private static RaidEvent MakeCompositionEvent(int groupCount, int slotsPerGroup) => new()
    {
        Id = 5,
        GuildId = GuildId,
        GuildBranchId = 10,
        Name = "Split 1",
        StartsAtUtc = EventStartsAtUtc,
        GroupCount = groupCount,
        SlotsPerGroup = slotsPerGroup,
    };

    private static RaidSlotAssignment MakeAssignment(int groupNumber, int slotNumber, string characterName, int classId, string specName) => new()
    {
        GroupNumber = groupNumber,
        SlotNumber = slotNumber,
        Character = new Character { Id = 1, Name = characterName, ClassId = classId },
        Spec = new Spec { Name = specName },
    };

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
        embed.Fields.Should().ContainSingle(f => f.Name == "Starts" && f.Value == RaidNotificationText.DiscordTimestamp(EventStartsAtUtc));
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
    public async Task BuildPublishedAsync_GuildNotFound_FallsBackToEnglish()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var embed = await _sut.BuildPublishedAsync(GuildId, RequesterId, MakeEvent());

        embed.Title.Should().Be("Raid published");
        embed.Description.Should().Be("<@42> published **Split 1**.");
        embed.Fields.Should().ContainSingle(f => f.Name == "Starts" && f.Value == RaidNotificationText.DiscordTimestamp(EventStartsAtUtc));
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
    public async Task BuildRescheduledAsync_ReturnsDescriptionWithOldAndNewTimestamps()
    {
        var oldStartsAtUtc = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc);

        var embed = await _sut.BuildRescheduledAsync(GuildId, RequesterId, MakeEvent(), oldStartsAtUtc);

        embed.Title.Should().Be("Raid rescheduled");
        embed.ColorHex.Should().Be(0xFEE75C);
        embed.Description.Should().Be(
            $"<@42> rescheduled **Split 1**: {RaidNotificationText.DiscordTimestamp(oldStartsAtUtc)} → {RaidNotificationText.DiscordTimestamp(EventStartsAtUtc)}.");
    }

    [Fact]
    public async Task BuildRescheduledAsync_GuildNotFound_FallsBackToEnglish()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);
        var oldStartsAtUtc = new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc);

        var embed = await _sut.BuildRescheduledAsync(GuildId, RequesterId, MakeEvent(), oldStartsAtUtc);

        embed.Title.Should().Be("Raid rescheduled");
        embed.Description.Should().Be(
            $"<@42> rescheduled **Split 1**: {RaidNotificationText.DiscordTimestamp(oldStartsAtUtc)} → {RaidNotificationText.DiscordTimestamp(EventStartsAtUtc)}.");
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

    // ── BuildCompositionAnnouncementAsync ─────────────────────────────────────

    [Fact]
    public async Task BuildCompositionAnnouncementAsync_FillsAssignedSlotsAndDashesEmptyOnesPaddedToThreeFields()
    {
        _emojiService.Setup(e => e.GetMarkdown("spec_deathknight_blood")).Returns("<:spec_deathknight_blood:2>");
        var raidEvent = MakeCompositionEvent(groupCount: 2, slotsPerGroup: 2);
        var assignments = new[] { MakeAssignment(1, 1, "Arthas", 6, "Blood") };

        var embed = await _sut.BuildCompositionAnnouncementAsync(GuildId, raidEvent, assignments);

        embed.Title.Should().Be("Split 1");
        embed.ColorHex.Should().Be(0x5865F2);
        embed.Description.Should().Be(RaidNotificationText.GetCompositionAnnouncementDescription(RaidNotificationText.DiscordTimestamp(EventStartsAtUtc), "en"));
        embed.Fields.Should().HaveCount(3); // 2 groups + 1 padding field to reach a multiple of 3
        embed.Fields![0].Name.Should().Be("Group 1:");
        embed.Fields[0].Value.Should().Be("<:spec_deathknight_blood:2> **Arthas**\n-");
        embed.Fields[1].Name.Should().Be("Group 2:");
        embed.Fields[1].Value.Should().Be("-\n-");
        embed.Fields[2].Name.Should().Be("​");
        embed.Fields[2].Value.Should().Be("​");
    }

    [Fact]
    public async Task BuildCompositionAnnouncementAsync_FieldCountAlreadyMultipleOfThree_NoPadding()
    {
        var raidEvent = MakeCompositionEvent(groupCount: 3, slotsPerGroup: 1);

        var embed = await _sut.BuildCompositionAnnouncementAsync(GuildId, raidEvent, []);

        embed.Fields.Should().HaveCount(3);
        embed.Fields.Should().OnlyContain(f => f.Name != "​");
    }

    [Fact]
    public async Task BuildCompositionAnnouncementAsync_FrontendUrlConfigured_UrlDeepLinksToEvent()
    {
        _configuration.Setup(c => c["FrontendUrl"]).Returns("https://app");
        var sut = new RaidNotificationContentBuilder(_guilds.Object, _guildBranchesRepository.Object, _branchRepository.Object, _discordBotService.Object, _configuration.Object);
        var raidEvent = MakeCompositionEvent(groupCount: 1, slotsPerGroup: 1);

        var embed = await sut.BuildCompositionAnnouncementAsync(GuildId, raidEvent, []);

        embed.Url.Should().Be($"https://app/guilds/{GuildId}/10/raids/5");
    }

    [Fact]
    public async Task BuildCompositionAnnouncementAsync_FrontendUrlUnconfigured_UrlIsNull()
    {
        var raidEvent = MakeCompositionEvent(groupCount: 1, slotsPerGroup: 1);

        var embed = await _sut.BuildCompositionAnnouncementAsync(GuildId, raidEvent, []);

        embed.Url.Should().BeNull();
    }

    // ── BuildSignupCallAsync ──────────────────────────────────────────────────

    private static RaidSignupResponse MakeSignup(string userId, SignupStatus? status, string? characterName = null, int? classId = null, string? specName = null, string? playerName = null) => new()
    {
        UserDiscordId = userId,
        PlayerName = playerName,
        Status = status,
        CharacterName = characterName,
        ClassId = classId,
        SpecName = specName,
    };

    [Fact]
    public async Task BuildSignupCallAsync_CountsEachStatusInDescription()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[]
        {
            MakeSignup("1", SignupStatus.Accepted, "Arthas", 1, "Arms"),
            MakeSignup("2", SignupStatus.Accepted, "Jaina", 8, "Frost"),
            MakeSignup("3", SignupStatus.Tentative, "Sylvanas", 3, "Marksmanship"),
            MakeSignup("4", SignupStatus.Declined),
        };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        embed.Description.Should().Contain("✅ 2 · ❓ 1 · ❌ 1");
    }

    [Fact]
    public async Task BuildSignupCallAsync_AcceptedSignupsAppearUnderTheirClassFieldSortedByName()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[]
        {
            MakeSignup("1", SignupStatus.Accepted, "zaela", 1, "Arms"),
            MakeSignup("2", SignupStatus.Accepted, "Anduin", 1, "Fury"),
        };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        var warriorField = embed.Fields!.Single(f => f.Name.Contains("Warrior"));
        warriorField.Value.Should().Be("Anduin\nzaela");
    }

    [Fact]
    public async Task BuildSignupCallAsync_AcceptedCharacterWithNoResolvedSpecIcon_ShowsPlainName()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[] { MakeSignup("1", SignupStatus.Accepted, "Arthas", 6, "Blood") };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        var dkField = embed.Fields!.Single(f => f.Name.Contains("Death Knight"));
        dkField.Value.Should().Be("Arthas");
    }

    [Fact]
    public async Task BuildSignupCallAsync_AcceptedCharacterWithResolvedSpecIcon_PrefixesTheIcon()
    {
        _emojiService.Setup(e => e.GetMarkdown("spec_deathknight_blood")).Returns("<:spec_deathknight_blood:2>");
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[] { MakeSignup("1", SignupStatus.Accepted, "Arthas", 6, "Blood") };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        var dkField = embed.Fields!.Single(f => f.Name.Contains("Death Knight"));
        dkField.Value.Should().Be("<:spec_deathknight_blood:2> Arthas");
    }

    [Fact]
    public async Task BuildSignupCallAsync_AcceptedWithNoCharacterResolved_FallsBackToPlayerNameThenDiscordId()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[] { MakeSignup("1", SignupStatus.Accepted, characterName: null, classId: 1, playerName: "Bhahlou") };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        var warriorField = embed.Fields!.Single(f => f.Name.Contains("Warrior"));
        warriorField.Value.Should().Be("Bhahlou");
    }

    [Fact]
    public async Task BuildSignupCallAsync_AcceptedWithNoCharacterOrPlayerName_FallsBackToDiscordId()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[] { MakeSignup("99", SignupStatus.Accepted, characterName: null, classId: 1, playerName: null) };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        var warriorField = embed.Fields!.Single(f => f.Name.Contains("Warrior"));
        warriorField.Value.Should().Be("99");
    }

    [Fact]
    public async Task BuildSignupCallAsync_TentativeWithNoCharacterName_FallsBackToPlayerName()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[] { MakeSignup("1", SignupStatus.Tentative, characterName: null, playerName: "Bhahlou") };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        embed.Fields!.Should().Contain(f => f.Name.StartsWith("Tentative (1)") && f.Value == "Bhahlou");
    }

    [Fact]
    public async Task BuildSignupCallAsync_TentativeWithNoCharacterOrPlayerName_FallsBackToDiscordId()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[] { MakeSignup("99", SignupStatus.Tentative, characterName: null, playerName: null) };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        embed.Fields!.Should().Contain(f => f.Name.StartsWith("Tentative (1)") && f.Value == "99");
    }

    [Fact]
    public async Task BuildSignupCallAsync_ClassWithNoAcceptedSignups_FieldValueIsDash()
    {
        var raidEvent = MakeCompositionEvent(1, 1);

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, []);

        var warriorField = embed.Fields!.First(f => f.Name.Contains("Warrior"));
        warriorField.Value.Should().Be("-");
    }

    [Fact]
    public async Task BuildSignupCallAsync_ClassEmojiResolved_PrefixesFieldTitle()
    {
        _emojiService.Setup(e => e.GetMarkdown("class_warrior")).Returns("<:class_warrior:1>");
        var raidEvent = MakeCompositionEvent(1, 1);

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, []);

        embed.Fields!.Should().Contain(f => f.Name == "<:class_warrior:1> Warrior");
    }

    [Fact]
    public async Task BuildSignupCallAsync_GuildBranchOnClassicExpansion_OmitsLaterClasses()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(10, default)).ReturnsAsync(new GuildBranch { Id = 10, GuildId = GuildId, BranchId = 1 });
        _branchRepository.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Branch { Id = 1, CurrentExpansionId = 1 });
        var raidEvent = MakeCompositionEvent(1, 1);

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, []);

        embed.Fields!.Should().NotContain(f => f.Name.Contains("Death Knight"));
        embed.Fields!.Should().NotContain(f => f.Name.Contains("Monk"));
        embed.Fields!.Should().Contain(f => f.Name.Contains("Warrior"));
    }

    [Fact]
    public async Task BuildSignupCallAsync_GuildBranchNotFound_ShowsEveryClass()
    {
        var raidEvent = MakeCompositionEvent(1, 1);

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, []);

        embed.Fields!.Should().Contain(f => f.Name.Contains("Evoker"));
    }

    [Fact]
    public async Task BuildSignupCallAsync_GuildBranchFoundButBranchNotFound_ShowsEveryClass()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(10, default)).ReturnsAsync(new GuildBranch { Id = 10, GuildId = GuildId, BranchId = 1 });
        _branchRepository.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync((Branch?)null);
        var raidEvent = MakeCompositionEvent(1, 1);

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, []);

        embed.Fields!.Should().Contain(f => f.Name.Contains("Evoker"));
    }

    [Fact]
    public void ClassEmoji_UnknownClassId_ReturnsEmptyString()
    {
        var result = _sut.ClassEmoji(999);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSignupCallAsync_TentativeAndDeclinedNamesListedInDedicatedFields()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[]
        {
            MakeSignup("1", SignupStatus.Tentative, "Sylvanas", 3, "Marksmanship"),
            MakeSignup("2", SignupStatus.Declined, playerName: "Bhahlou"),
        };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        embed.Fields!.Should().Contain(f => f.Name.StartsWith("Tentative (1)") && f.Value == "Sylvanas");
        embed.Fields!.Should().Contain(f => f.Name.StartsWith("Declined (1)") && f.Value == "Bhahlou");
    }

    [Fact]
    public async Task BuildSignupCallAsync_DeclinedWithNoPlayerName_FallsBackToDiscordId()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[] { MakeSignup("99", SignupStatus.Declined) };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        embed.Fields!.Should().Contain(f => f.Name.StartsWith("Declined (1)") && f.Value == "99");
    }

    [Fact]
    public async Task BuildSignupCallAsync_MultipleDeclined_SortedCaseInsensitivelyByName()
    {
        var raidEvent = MakeCompositionEvent(1, 1);
        var signups = new[]
        {
            MakeSignup("1", SignupStatus.Declined, playerName: "zaela"),
            MakeSignup("2", SignupStatus.Declined, playerName: "Anduin"),
        };

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, signups);

        embed.Fields!.Should().Contain(f => f.Name.StartsWith("Declined (2)") && f.Value == "Anduin\nzaela");
    }

    [Fact]
    public async Task BuildSignupCallAsync_NoTentativeOrDeclined_FieldsShowDash()
    {
        var raidEvent = MakeCompositionEvent(1, 1);

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, []);

        embed.Fields!.Should().Contain(f => f.Name.StartsWith("Tentative (0)") && f.Value == "-");
        embed.Fields!.Should().Contain(f => f.Name.StartsWith("Declined (0)") && f.Value == "-");
    }

    [Fact]
    public async Task BuildSignupCallAsync_ReturnsThreeButtonsEncodingBranchEventAndStatus()
    {
        var raidEvent = MakeCompositionEvent(1, 1);

        var embed = await _sut.BuildSignupCallAsync(GuildId, 10, raidEvent, []);

        embed.Buttons.Should().HaveCount(3);
        embed.Buttons!.Should().Contain(b => b.Label == "Accepted" && b.CustomId == "raidsignup:10:5:accepted" && b.Style == DiscordEmbedButtonStyle.Success);
        embed.Buttons!.Should().Contain(b => b.Label == "Tentative" && b.CustomId == "raidsignup:10:5:tentative" && b.Style == DiscordEmbedButtonStyle.Secondary);
        embed.Buttons!.Should().Contain(b => b.Label == "Declined" && b.CustomId == "raidsignup:10:5:declined" && b.Style == DiscordEmbedButtonStyle.Danger);
    }

    [Fact]
    public async Task BuildSignupCallAsync_ReturnsTitleColorAndUrl()
    {
        _configuration.Setup(c => c["FrontendUrl"]).Returns("https://app");
        var sut = new RaidNotificationContentBuilder(_guilds.Object, _guildBranchesRepository.Object, _branchRepository.Object, _discordBotService.Object, _configuration.Object);
        var raidEvent = MakeCompositionEvent(1, 1);

        var embed = await sut.BuildSignupCallAsync(GuildId, 10, raidEvent, []);

        embed.Title.Should().Be("Split 1");
        embed.ColorHex.Should().Be(0x5865F2);
        embed.Url.Should().Be($"https://app/guilds/{GuildId}/10/raids/5");
        embed.Author.Should().BeNull();
    }

    // ── BuildPlayerAddedDmAsync ───────────────────────────────────────────────

    [Fact]
    public async Task BuildPlayerAddedDmAsync_InitialPublish_UsesRaidPublishedTitle()
    {
        var character = new RaidCharacterRef("Arthas", null);

        var embed = await _sut.BuildPlayerAddedDmAsync(GuildId, MakeEvent(), character, isInitialPublish: true);

        embed.Title.Should().Be("Split 1 published");
        embed.ColorHex.Should().Be(0x57F287);
        embed.Description.Should().Be($"You've been added to **Split 1** ({RaidNotificationText.DiscordTimestamp(EventStartsAtUtc)}) with **Arthas**.");
    }

    [Fact]
    public async Task BuildPlayerAddedDmAsync_NotInitialPublish_UsesAddedToRaidTitle()
    {
        var character = new RaidCharacterRef("Arthas", null);

        var embed = await _sut.BuildPlayerAddedDmAsync(GuildId, MakeEvent(), character, isInitialPublish: false);

        embed.Title.Should().Be("Added to the raid");
        embed.ColorHex.Should().Be(0x57F287);
    }

    // ── BuildPlayerRemovedDmAsync ─────────────────────────────────────────────

    [Fact]
    public async Task BuildPlayerRemovedDmAsync_ReturnsRemovedTitleAndDescription()
    {
        var character = new RaidCharacterRef("Arthas", null);

        var embed = await _sut.BuildPlayerRemovedDmAsync(GuildId, MakeEvent(), character);

        embed.Title.Should().Be("Removed from the raid");
        embed.ColorHex.Should().Be(0xED4245);
        embed.Description.Should().Be($"You've been removed from **Split 1** ({RaidNotificationText.DiscordTimestamp(EventStartsAtUtc)}) with **Arthas**.");
    }

    // ── BuildPlayerSpecChangedDmAsync ─────────────────────────────────────────

    [Fact]
    public async Task BuildPlayerSpecChangedDmAsync_CharacterLabelOmitsSpecIcon_SpecsShowOwnIcons()
    {
        _emojiService.Setup(e => e.GetMarkdown("spec_deathknight_blood")).Returns("<:spec_deathknight_blood:2>");
        _emojiService.Setup(e => e.GetMarkdown("spec_deathknight_frost")).Returns("<:spec_deathknight_frost:3>");
        var character = new RaidCharacterRef("Arthas", 6, "Frost");

        var embed = await _sut.BuildPlayerSpecChangedDmAsync(GuildId, MakeEvent(), character, "Blood", "Frost");

        embed.Title.Should().Be("Spec changed");
        embed.ColorHex.Should().Be(0xFEE75C);
        embed.Description.Should().Be(
            $"Your spec for **Split 1** ({RaidNotificationText.DiscordTimestamp(EventStartsAtUtc)}) changed on **Arthas**: <:spec_deathknight_blood:2> **Blood** → <:spec_deathknight_frost:3> **Frost**.");
    }

    // ── BuildRaidCancelledDmAsync ─────────────────────────────────────────────

    [Fact]
    public async Task BuildRaidCancelledDmAsync_ReturnsCancelledTitleAndDescription()
    {
        var character = new RaidCharacterRef("Arthas", null);

        var embed = await _sut.BuildRaidCancelledDmAsync(GuildId, MakeEvent(), character);

        embed.Title.Should().Be("Raid cancelled");
        embed.ColorHex.Should().Be(0xED4245);
        embed.Description.Should().Be($"The raid **Split 1** ({RaidNotificationText.DiscordTimestamp(EventStartsAtUtc)}) you were in with **Arthas** has been cancelled.");
    }

    // ── BuildRaidEventUrl ─────────────────────────────────────────────────────

    [Fact]
    public void BuildRaidEventUrl_FrontendUrlConfigured_ReturnsDeepLink()
    {
        _configuration.Setup(c => c["FrontendUrl"]).Returns("https://app");
        var sut = new RaidNotificationContentBuilder(_guilds.Object, _guildBranchesRepository.Object, _branchRepository.Object, _discordBotService.Object, _configuration.Object);
        var raidEvent = MakeCompositionEvent(groupCount: 1, slotsPerGroup: 1);

        var url = sut.BuildRaidEventUrl(raidEvent);

        url.Should().Be($"https://app/guilds/{GuildId}/10/raids/5");
    }

    [Fact]
    public void BuildRaidEventUrl_FrontendUrlUnconfigured_ReturnsNull()
    {
        var raidEvent = MakeCompositionEvent(groupCount: 1, slotsPerGroup: 1);

        var url = _sut.BuildRaidEventUrl(raidEvent);

        url.Should().BeNull();
    }
}
