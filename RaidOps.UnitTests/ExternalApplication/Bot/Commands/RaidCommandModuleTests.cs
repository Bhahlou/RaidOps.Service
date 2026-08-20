using System.Net.Http;
using FluentAssertions;
using Moq;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.ExternalApplication.Implementations.Bot.Commands;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Commands;

public class RaidCommandModuleTests
{
    private readonly Mock<ICommandDispatcher> _commandDispatcher = new();
    private readonly Mock<IQueryDispatcher> _queryDispatcher = new();
    private readonly RaidCommandModule _sut;

    private const ulong GuildIdUlong = 42UL;
    private const ulong UserIdUlong = 7UL;
    private const string GuildId = "42";
    private const string RequesterId = "7";

    private const string MessageJson = """{"id":"1","channel_id":"1","content":"","type":0,"timestamp":"2025-01-01T00:00:00+00:00","edited_timestamp":null,"tts":false,"mention_everyone":false,"mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"author":{"id":"1","username":"bot","discriminator":"0","global_name":"bot","avatar":null}}""";

    public RaidCommandModuleTests()
    {
        _sut = new RaidCommandModule(_commandDispatcher.Object, _queryDispatcher.Object);
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse { Language = "en" }));
    }

    private (Mock<IRestRequestHandler> Handler, Func<string?> Body) Attach(Guild? guild)
    {
        var (rest, handler) = NetCordTestHelpers.MakeFakeRestClient();

        string? lastBody = null;
        handler.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>(), default))
            .Returns((HttpRequestMessage req, CancellationToken _) =>
            {
                lastBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return Task.FromResult(NetCordTestHelpers.JsonResponse(MessageJson));
            });

        var context = NetCordTestHelpers.MakeSlashCommandContext(UserIdUlong, guild, rest);
        NetCordTestHelpers.SetModuleContext(_sut, context);
        return (handler, () => lastBody);
    }

    private static Guild MakeGuild() =>
        NetCordTestHelpers.MakeGuild(GuildIdUlong, GuildIdUlong, new Dictionary<ulong, GuildUser>());

    // ── No guild (DM context) ────────────────────────────────────────────────

    [Fact]
    public async Task InviteAsync_NoGuild_RespondsWithGuildOnlyMessageAndNeverDispatches()
    {
        var (_, body) = Attach(guild: null);

        await _sut.InviteAsync("7:5", null);

        body().Should().NotBeNull();
        body()!.Should().Contain("This command can only be used in a Discord server.");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerRaidGroupingCommand>(), default), Times.Never);
        _queryDispatcher.Verify(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default), Times.Never);
    }

    // ── Malformed raid selection ──────────────────────────────────────────────

    [Theory]
    [InlineData("notanumber")]
    [InlineData("7")]
    [InlineData("7:notanumber")]
    [InlineData("notanumber:5")]
    [InlineData("7:5:9")]
    public async Task InviteAsync_MalformedRaidSelection_RespondsWithInvalidSelectionAndNeverDispatchesCommand(string raid)
    {
        var (_, body) = Attach(MakeGuild());

        await _sut.InviteAsync(raid, null);

        body()!.Should().Contain("Invalid raid selection");
        _commandDispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerRaidGroupingCommand>(), default), Times.Never);
    }

    // ── Valid selection ───────────────────────────────────────────────────────

    [Fact]
    public async Task InviteAsync_ValidSelection_DispatchesCommandWithParsedIdsAndRequester()
    {
        var (_, body) = Attach(MakeGuild());
        _commandDispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<TriggerRaidGroupingCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        await _sut.InviteAsync("10:5", null);

        _commandDispatcher.Verify(d => d.DispatchAsync(
            It.Is<TriggerRaidGroupingCommand>(c =>
                c.GuildId == GuildId &&
                c.GuildBranchId == 10 &&
                c.EventId == 5 &&
                c.RequesterDiscordId == RequesterId &&
                c.CharacterName == null),
            default), Times.Once);
        body()!.Should().Contain("Grouping message sent!");
    }

    [Fact]
    public async Task InviteAsync_CharacterProvided_PassedThroughToDispatchedCommand()
    {
        Attach(MakeGuild());
        _commandDispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<TriggerRaidGroupingCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        await _sut.InviteAsync("10:5", "Jaina");

        _commandDispatcher.Verify(d => d.DispatchAsync(
            It.Is<TriggerRaidGroupingCommand>(c => c.CharacterName == "Jaina"),
            default), Times.Once);
    }

    [Fact]
    public async Task InviteAsync_CommandFails_RespondsWithMappedFailureReason()
    {
        var (_, body) = Attach(MakeGuild());
        _commandDispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<TriggerRaidGroupingCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.Forbidden));

        await _sut.InviteAsync("10:5", null);

        body()!.Should().Contain("you must be an officer of this guild to use this command.");
    }

    [Fact]
    public async Task InviteAsync_LanguageQueryFails_FallsBackToEnglish()
    {
        var (_, body) = Attach(MakeGuild());
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Fail(ResponseDetail.GuildNotFound));
        _commandDispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<TriggerRaidGroupingCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        await _sut.InviteAsync("10:5", null);

        body()!.Should().Contain("Grouping message sent!");
    }

    [Fact]
    public async Task InviteAsync_GuildLanguageFrench_UsesFrenchTextThroughout()
    {
        var (_, body) = Attach(MakeGuild());
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse { Language = "fr" }));

        await _sut.InviteAsync("bogus", null);

        // Non-ASCII characters come back \u-escaped in the raw JSON body — assert on the ASCII tail.
        body()!.Should().Contain("raid invalide");
    }
}
