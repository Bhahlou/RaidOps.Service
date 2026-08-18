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

public class RaidSignupInteractionModuleTests
{
    private readonly Mock<ICommandDispatcher> _commandDispatcher = new();
    private readonly Mock<IQueryDispatcher> _queryDispatcher = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IEmojiService> _emojiService = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly RaidSignupInteractionModule _sut;

    private const ulong GuildIdUlong = 42UL;
    private const ulong UserIdUlong = 7UL;
    private const string GuildId = "42";
    private const string RequesterId = "7";
    private const int GuildBranchId = 10;
    private const int EventId = 5;

    private const string MessageJson = """{"id":"1","channel_id":"1","content":"","type":0,"timestamp":"2025-01-01T00:00:00+00:00","edited_timestamp":null,"tts":false,"mention_everyone":false,"mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"author":{"id":"1","username":"bot","discriminator":"0","global_name":"bot","avatar":null}}""";

    public RaidSignupInteractionModuleTests()
    {
        _discordBotService.Setup(d => d.Emojis).Returns(_emojiService.Object);
        _sut = new RaidSignupInteractionModule(_commandDispatcher.Object, _queryDispatcher.Object, _discordBotService.Object, _configuration.Object);
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse { Language = "en" }));
    }

    private (Mock<IRestRequestHandler> Handler, Func<string?> Body) Attach(Guild? guild)
    {
        var (rest, handler) = NetCordTestHelpers.MakeFakeRestClient();

        string? lastBody = null;
        handler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default))
            .Returns((HttpRequestMessage req, CancellationToken ct) =>
            {
                lastBody = req.Content?.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                return Task.FromResult(NetCordTestHelpers.JsonResponse(MessageJson));
            });

        var context = NetCordTestHelpers.MakeButtonInteractionContext(UserIdUlong, guild, rest);
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

    // ── No guild (DM context) ────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoGuild_RespondsWithGuildOnlyMessageAndNeverDispatches()
    {
        var (_, body) = Attach(guild: null);

        await _sut.HandleAsync(GuildBranchId, EventId, "accepted");

        body()!.Should().Contain("This can only be used in a Discord server.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    // ── Invalid status ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_UnrecognizedStatus_RespondsWithInvalidActionAndNeverDispatches()
    {
        var (_, body) = Attach(MakeGuild());

        await _sut.HandleAsync(GuildBranchId, EventId, "bogus");

        body()!.Should().Contain("Invalid signup action.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    // ── Declined — no character resolution ─────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Declined_NeverQueriesCharactersAndDispatchesWithNullCharacter()
    {
        var (_, body) = Attach(MakeGuild());
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        await _sut.HandleAsync(GuildBranchId, EventId, "declined");

        _queryDispatcher.Verify(q => q.DispatchAsync<GetMyRosterCharactersQuery, List<RaidSignupCharacterResponse>>(It.IsAny<GetMyRosterCharactersQuery>(), default), Times.Never);
        _commandDispatcher.Verify(d => d.DispatchAsync(
            It.Is<SetMyRaidSignupCommand>(c => c.Status == SignupStatus.Declined && c.CharacterId == null && c.SpecId == null && c.RequesterDiscordId == RequesterId),
            default), Times.Once);
        body()!.Should().Contain("Response saved!");
    }

    // ── Accepted/Tentative — zero characters ────────────────────────────────────

    [Theory]
    [InlineData("accepted")]
    [InlineData("tentative")]
    public async Task HandleAsync_NoRosterCharacters_DispatchesWithNullCharacterLeavingValidationToTheCommand(string status)
    {
        Attach(MakeGuild());
        SetupCharacters();
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.CharacterRequiredForSignup));

        await _sut.HandleAsync(GuildBranchId, EventId, status);

        _commandDispatcher.Verify(d => d.DispatchAsync(It.Is<SetMyRaidSignupCommand>(c => c.CharacterId == null), default), Times.Once);
    }

    // ── Accepted/Tentative — exactly one character ──────────────────────────────

    [Fact]
    public async Task HandleAsync_OneCharacterNoSpecs_DispatchesWithCharacterIdAndNullSpec()
    {
        Attach(MakeGuild());
        SetupCharacters(MakeCharacter(1, 6));
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.SpecRequiredForSignup));

        await _sut.HandleAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.Is<SetMyRaidSignupCommand>(c => c.CharacterId == 1 && c.SpecId == null), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OneCharacterOneSpec_AutoFillsCharacterAndSpecThenDispatches()
    {
        Attach(MakeGuild());
        SetupCharacters(MakeCharacter(1, 6, (71, "Blood")));
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        await _sut.HandleAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.Is<SetMyRaidSignupCommand>(c => c.CharacterId == 1 && c.SpecId == 71), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OneCharacterMultipleSpecs_RespondsWithSpecPickerAndNeverDispatches()
    {
        var (_, body) = Attach(MakeGuild());
        SetupCharacters(MakeCharacter(1, 6, (71, "Blood"), (72, "Frost")));

        await _sut.HandleAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
        body()!.Should().Contain("Declare it on RaidOps");
        body()!.Should().Contain("raidsignup-pickspec:10:5:1:accepted");
    }

    // ── Accepted/Tentative — several characters ─────────────────────────────────

    [Fact]
    public async Task HandleAsync_MultipleCharacters_RespondsWithCharacterPickerAndNeverDispatches()
    {
        var (_, body) = Attach(MakeGuild());
        SetupCharacters(MakeCharacter(1, 6, (71, "Blood")), MakeCharacter(2, 8, (81, "Frost")));

        await _sut.HandleAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
        body()!.Should().Contain("Character not in the list?");
        body()!.Should().Contain("raidsignup-pickchar:10:5:accepted");
    }

    [Fact]
    public async Task HandleAsync_MultipleCharactersOneWithZeroSpecsAndUnmappedClass_UsesNoEmojiForThatOption()
    {
        Attach(MakeGuild());
        SetupCharacters(MakeCharacter(1, 999), MakeCharacter(2, 8, (81, "Frost")));

        await _sut.HandleAsync(GuildBranchId, EventId, "accepted");

        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MultipleCharactersOneWithZeroSpecsAndUnresolvedClassEmoji_StillRespondsWithPicker()
    {
        var (_, body) = Attach(MakeGuild());
        SetupCharacters(MakeCharacter(1, 6), MakeCharacter(2, 8, (81, "Frost")));

        await _sut.HandleAsync(GuildBranchId, EventId, "accepted");

        body()!.Should().Contain("raidsignup-pickchar:10:5:accepted");
    }

    [Fact]
    public async Task HandleAsync_MultipleCharactersOneWithZeroSpecsAndResolvedClassEmoji_StillRespondsWithPicker()
    {
        _emojiService.Setup(e => e.GetId("class_deathknight")).Returns(111UL);
        var (_, body) = Attach(MakeGuild());
        SetupCharacters(MakeCharacter(1, 6), MakeCharacter(2, 8, (81, "Frost")));

        await _sut.HandleAsync(GuildBranchId, EventId, "accepted");

        body()!.Should().Contain("raidsignup-pickchar:10:5:accepted");
    }

    // ── Language ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_GuildLanguageFrench_UsesFrenchTextThroughout()
    {
        var (_, body) = Attach(MakeGuild());
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse { Language = "fr" }));

        await _sut.HandleAsync(GuildBranchId, EventId, "bogus");

        body()!.Should().Contain("Action d");
    }
}
