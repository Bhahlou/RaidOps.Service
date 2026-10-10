using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Calendar.Availability.Services;

public class AbsenceNotificationContentBuilderTests
{
    private readonly Mock<IGuildsRepository> _guilds = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly AbsenceNotificationContentBuilder _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "42";

    public AbsenceNotificationContentBuilderTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new AbsenceNotificationContentBuilder(_guilds.Object, _discordBotService.Object);
    }

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

    // ── BuildAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_MemberFound_PopulatesAuthorFromNickname()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Language = "en" });
        var member = NetCordTestHelpers.MakeGuildUser(42, 1, [], username: "arthas", nickname: "Le Roi Liche", guildAvatarHash: "hash123");
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Returns(member);

        var fields = new List<DiscordEmbedField> { new("Dates", "1/1/2026") };
        var embed = await _sut.BuildAsync(GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, fields);

        embed.Title.Should().Be("New absence");
        embed.ColorHex.Should().Be(0xFEE75C);
        embed.Description.Should().Be("<@42> added a new absence.");
        embed.Fields.Should().BeSameAs(fields);
        embed.Author.Should().NotBeNull();
        embed.Author!.Name.Should().Be("Le Roi Liche");
        embed.Author.IconUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildAsync_MemberHasNoNickname_FallsBackToGlobalName()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G" });
        var member = NetCordTestHelpers.MakeGuildUser(42, 1, [], username: "arthas", globalName: "Arthas Menethil");
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Returns(member);

        var embed = await _sut.BuildAsync(GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, []);

        embed.Author!.Name.Should().Be("Arthas Menethil");
    }

    [Fact]
    public async Task BuildAsync_MemberHasOnlyUsername_UsesUsername()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G" });
        var member = NetCordTestHelpers.MakeGuildUser(42, 1, [], username: "arthas");
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Returns(member);

        var embed = await _sut.BuildAsync(GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, []);

        embed.Author!.Name.Should().Be("arthas");
    }

    [Fact]
    public async Task BuildAsync_MemberNotFound_AuthorIsNull()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G" });
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Returns((NetCord.GuildUser?)null);

        var embed = await _sut.BuildAsync(GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, []);

        embed.Author.Should().BeNull();
    }

    [Fact]
    public async Task BuildAsync_GuildServiceThrowsInvalidOperationException_AuthorIsNullNoExceptionPropagates()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G" });
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Throws<InvalidOperationException>();

        var embed = await _sut.BuildAsync(GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, []);

        embed.Author.Should().BeNull();
    }

    // ── BuildPatternAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task BuildPatternAsync_Success_UsesPatternTitleAndDescription()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Language = "en" });
        _guildService.Setup(s => s.GetUser(GuildId, RequesterId, default)).Returns((NetCord.GuildUser?)null);

        var days = new List<PatternDayNotification> { new(0, DayAvailabilityStatus.Absent, null, null, null) };
        var embed = await _sut.BuildPatternAsync(GuildId, RequesterId, GuildNotificationEventType.AbsenceAdded, new DateOnly(2026, 6, 29), 7, days);

        embed.Title.Should().Be("New recurring absences");
        embed.ColorHex.Should().Be(0xFEE75C);
        embed.Description.Should().StartWith("<@42> added a new recurring absence pattern:");
        embed.Fields.Should().BeNull();
    }
}
