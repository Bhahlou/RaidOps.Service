using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Raids.Signups.Commands;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot.Commands;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Commands;

public class RaidSignupPickerModuleTests
{
    private readonly Mock<ICommandDispatcher> _commandDispatcher = new();
    private readonly Mock<IQueryDispatcher> _queryDispatcher = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IEmojiService> _emojiService = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly RaidSignupPickerModule _sut;

    private const ulong GuildIdUlong = 42UL;
    private const ulong UserIdUlong = 7UL;
    private const string GuildId = "42";
    private const string RequesterId = "7";
    private const int GuildBranchId = 10;
    private const int EventId = 5;
    private const int CharacterId = 1;

    private const string MessageJson = """{"id":"1","channel_id":"1","content":"","type":0,"timestamp":"2025-01-01T00:00:00+00:00","edited_timestamp":null,"tts":false,"mention_everyone":false,"mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"author":{"id":"1","username":"bot","discriminator":"0","global_name":"bot","avatar":null}}""";

    public RaidSignupPickerModuleTests()
    {
        _discordBotService.Setup(d => d.Emojis).Returns(_emojiService.Object);
        _sut = new RaidSignupPickerModule(_commandDispatcher.Object, _queryDispatcher.Object, _discordBotService.Object, _configuration.Object);
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse { Language = "en" }));
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
    }

    private (Mock<IRestRequestHandler> Handler, Func<string?> Body) Attach(Guild? guild, params string[] selectedValues)
    {
        var (rest, handler) = NetCordTestHelpers.MakeFakeRestClient();

        string? lastBody = null;
        handler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default))
            .Returns((HttpRequestMessage req, CancellationToken ct) =>
            {
                lastBody = req.Content?.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                return Task.FromResult(NetCordTestHelpers.JsonResponse(MessageJson));
            });

        var context = NetCordTestHelpers.MakeStringMenuInteractionContext(UserIdUlong, guild, rest, selectedValues);
        NetCordTestHelpers.SetModuleContext(_sut, context);
        return (handler, () => lastBody);
    }

    private static Guild MakeGuild() =>
        NetCordTestHelpers.MakeGuild(GuildIdUlong, GuildIdUlong, new Dictionary<ulong, GuildUser>());

    private static RaidSignupCharacterResponse MakeCharacter(int characterId, int classId, params (int SpecId, string SpecName)[] specs) => new()
    {
        CharacterId = characterId,
        CharacterName = "Arthas",
        ClassId = classId,
        BranchName = "Classic Era",
        RealmSlug = "silvermoon",
        RaidSpecs = specs.Select(s => new RaidSignupSpecResponse { SpecId = s.SpecId, SpecName = s.SpecName, IsMain = false }).ToList(),
    };

    private void SetupCharacters(params RaidSignupCharacterResponse[] characters) =>
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetMyRosterCharactersQuery, List<RaidSignupCharacterResponse>>(It.IsAny<GetMyRosterCharactersQuery>(), default))
            .ReturnsAsync(Result<List<RaidSignupCharacterResponse>>.Ok(characters.ToList()));

    // ══════════════════════ HandleCharacterAsync ══════════════════════════════

    [Fact]
    public async Task HandleCharacterAsync_NoGuild_RespondsWithGuildOnlyMessageAndNeverDispatches()
    {
        var (_, body) = Attach(guild: null, "1");

        await _sut.HandleCharacterAsync(GuildBranchId, EventId, "accepted");

        body()!.Should().Contain("This can only be used in a Discord server.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleCharacterAsync_UnrecognizedStatus_RespondsWithInvalidActionAndNeverDispatches()
    {
        var (_, body) = Attach(MakeGuild(), "1");

        await _sut.HandleCharacterAsync(GuildBranchId, EventId, "bogus");

        body()!.Should().Contain("Invalid signup action.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleCharacterAsync_NoSelectedValue_RespondsWithNoCharacterSelectedAndNeverDispatches()
    {
        var (_, body) = Attach(MakeGuild());

        await _sut.HandleCharacterAsync(GuildBranchId, EventId, "accepted");

        body()!.Should().Contain("No character selected.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleCharacterAsync_NonNumericSelectedValue_RespondsWithNoCharacterSelected()
    {
        var (_, body) = Attach(MakeGuild(), "notanumber");

        await _sut.HandleCharacterAsync(GuildBranchId, EventId, "accepted");

        body()!.Should().Contain("No character selected.");
    }

    [Fact]
    public async Task HandleCharacterAsync_SelectedCharacterHasMultipleSpecs_RespondsWithSpecPickerAndNeverDispatches()
    {
        var (_, body) = Attach(MakeGuild(), CharacterId.ToString());
        SetupCharacters(MakeCharacter(CharacterId, 6, (71, "Blood"), (72, "Frost")));

        await _sut.HandleCharacterAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
        body()!.Should().Contain($"raidsignup-pickspec:{GuildBranchId}:{EventId}:{CharacterId}:accepted");
    }

    [Fact]
    public async Task HandleCharacterAsync_SelectedCharacterHasOneSpec_DispatchesWithAutoFilledSpec()
    {
        Attach(MakeGuild(), CharacterId.ToString());
        SetupCharacters(MakeCharacter(CharacterId, 6, (71, "Blood")));

        await _sut.HandleCharacterAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.Is<SetMyRaidSignupCommand>(c => c.CharacterId == CharacterId && c.SpecId == 71), default), Times.Once);
    }

    [Fact]
    public async Task HandleCharacterAsync_SelectedCharacterNoLongerFound_DispatchesWithNullSpecLeftToValidation()
    {
        Attach(MakeGuild(), "999");
        SetupCharacters(MakeCharacter(CharacterId, 6, (71, "Blood")));

        await _sut.HandleCharacterAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.Is<SetMyRaidSignupCommand>(c => c.CharacterId == 999 && c.SpecId == null), default), Times.Once);
    }

    [Fact]
    public async Task HandleCharacterAsync_RosterCharactersQueryFails_DispatchesWithNullSpecLeftToValidation()
    {
        Attach(MakeGuild(), CharacterId.ToString());
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetMyRosterCharactersQuery, List<RaidSignupCharacterResponse>>(It.IsAny<GetMyRosterCharactersQuery>(), default))
            .ReturnsAsync(Result<List<RaidSignupCharacterResponse>>.Fail(ResponseDetail.Forbidden));

        await _sut.HandleCharacterAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.Is<SetMyRaidSignupCommand>(c => c.CharacterId == CharacterId && c.SpecId == null), default), Times.Once);
    }

    // ══════════════════════ HandleSpecAsync ═══════════════════════════════════

    [Fact]
    public async Task HandleSpecAsync_NoGuild_RespondsWithGuildOnlyMessageAndNeverDispatches()
    {
        var (_, body) = Attach(guild: null, "71");

        await _sut.HandleSpecAsync(GuildBranchId, EventId, CharacterId, "accepted");

        body()!.Should().Contain("This can only be used in a Discord server.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleSpecAsync_UnrecognizedStatus_RespondsWithInvalidActionAndNeverDispatches()
    {
        var (_, body) = Attach(MakeGuild(), "71");

        await _sut.HandleSpecAsync(GuildBranchId, EventId, CharacterId, "bogus");

        body()!.Should().Contain("Invalid signup action.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleSpecAsync_NoSelectedValue_RespondsWithNoSpecSelectedAndNeverDispatches()
    {
        var (_, body) = Attach(MakeGuild());

        await _sut.HandleSpecAsync(GuildBranchId, EventId, CharacterId, "accepted");

        body()!.Should().Contain("No spec selected.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleSpecAsync_NonNumericSelectedValue_RespondsWithNoSpecSelected()
    {
        var (_, body) = Attach(MakeGuild(), "notanumber");

        await _sut.HandleSpecAsync(GuildBranchId, EventId, CharacterId, "accepted");

        body()!.Should().Contain("No spec selected.");
    }

    [Fact]
    public async Task HandleSpecAsync_ValidSelection_DispatchesWithParsedSpecId()
    {
        Attach(MakeGuild(), "71");
        SetupCharacters(MakeCharacter(CharacterId, 6, (71, "Blood")));

        await _sut.HandleSpecAsync(GuildBranchId, EventId, CharacterId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(
            It.Is<SetMyRaidSignupCommand>(c => c.CharacterId == CharacterId && c.SpecId == 71 && c.RequesterDiscordId == RequesterId && c.GuildBranchId == GuildBranchId && c.EventId == EventId),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleSpecAsync_Success_RespondsWithSavedMessage()
    {
        var (_, body) = Attach(MakeGuild(), "71");
        SetupCharacters(MakeCharacter(CharacterId, 6, (71, "Blood")));

        await _sut.HandleSpecAsync(GuildBranchId, EventId, CharacterId, "accepted");

        body()!.Should().Contain("Response saved!");
    }

    [Fact]
    public async Task HandleSpecAsync_RosterCharactersQueryFails_DispatchesWithParsedSpecId()
    {
        Attach(MakeGuild(), "71");
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetMyRosterCharactersQuery, List<RaidSignupCharacterResponse>>(It.IsAny<GetMyRosterCharactersQuery>(), default))
            .ReturnsAsync(Result<List<RaidSignupCharacterResponse>>.Fail(ResponseDetail.Forbidden));

        await _sut.HandleSpecAsync(GuildBranchId, EventId, CharacterId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(
            It.Is<SetMyRaidSignupCommand>(c => c.CharacterId == CharacterId && c.SpecId == 71),
            default), Times.Once);
    }
}
