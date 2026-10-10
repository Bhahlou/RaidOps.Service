using FluentAssertions;
using Moq;
using NetCord;
using NetCord.Gateway;
using NetCord.JsonModels;
using NetCord.Rest;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.ExternalApplication.Implementations.Bot.Commands;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Commands;

public class RaidGroupingAutocompleteProviderTests
{
    private readonly Mock<IQueryDispatcher> _queryDispatcher = new();
    private readonly RaidGroupingAutocompleteProvider _sut;

    private const ulong GuildIdUlong = 42UL;
    private const string GuildId = "42";

    public RaidGroupingAutocompleteProviderTests()
    {
        _sut = new RaidGroupingAutocompleteProvider(_queryDispatcher.Object);
    }

    private static Guild MakeGuild() =>
        NetCordTestHelpers.MakeGuild(GuildIdUlong, GuildIdUlong, new Dictionary<ulong, GuildUser>());

    private static NetCord.Services.ApplicationCommands.AutocompleteInteractionContext MakeContext(Guild? guild)
    {
        var (rest, _) = NetCordTestHelpers.MakeFakeRestClient();
        return NetCordTestHelpers.MakeAutocompleteContext(guild, rest);
    }

    private static ApplicationCommandInteractionDataOption MakeOption(string? input) =>
        new(new JsonApplicationCommandInteractionDataOption { Name = "raid", Value = input });

    private static RaidEventChoiceResponse MakeChoice(int id, int guildBranchId, string name, string branchName = "Classic Era") => new()
    {
        Id = id,
        GuildBranchId = guildBranchId,
        Name = name,
        StartsAtLocal = new DateTime(2026, 2, 1, 21, 0, 0),
        BranchName = branchName,
    };

    // ── No guild (DM context) ────────────────────────────────────────────────

    [Fact]
    public async Task GetChoicesAsync_NoGuild_ReturnsNullAndNeverDispatches()
    {
        var choices = await _sut.GetChoicesAsync(MakeOption(null), MakeContext(guild: null));

        choices.Should().BeNull();
        _queryDispatcher.Verify(
            q => q.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(It.IsAny<GetUpcomingPublishedRaidEventChoicesQuery>(), default),
            Times.Never);
    }

    // ── Query failure ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetChoicesAsync_QueryFails_ReturnsNull()
    {
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(It.IsAny<GetUpcomingPublishedRaidEventChoicesQuery>(), default))
            .ReturnsAsync(Result<List<RaidEventChoiceResponse>>.Fail(ResponseDetail.GuildNotFound));

        var choices = await _sut.GetChoicesAsync(MakeOption(null), MakeContext(MakeGuild()));

        choices.Should().BeNull();
    }

    // ── Success ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetChoicesAsync_QueriesForTheContextGuild()
    {
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(It.IsAny<GetUpcomingPublishedRaidEventChoicesQuery>(), default))
            .ReturnsAsync(Result<List<RaidEventChoiceResponse>>.Ok([]));

        await _sut.GetChoicesAsync(MakeOption(null), MakeContext(MakeGuild()));

        _queryDispatcher.Verify(
            q => q.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(
                It.Is<GetUpcomingPublishedRaidEventChoicesQuery>(query => query.GuildId == GuildId), default),
            Times.Once);
    }

    [Fact]
    public async Task GetChoicesAsync_FormatsNameDateAndBranchIntoTheLabel_AndEncodesBranchAndEventIdsIntoTheValue()
    {
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(It.IsAny<GetUpcomingPublishedRaidEventChoicesQuery>(), default))
            .ReturnsAsync(Result<List<RaidEventChoiceResponse>>.Ok([MakeChoice(5, 10, "Split 1", "Classic Era")]));

        var choices = (await _sut.GetChoicesAsync(MakeOption(null), MakeContext(MakeGuild())))!.ToList();

        choices.Should().ContainSingle();
        choices[0].Name.Should().Be("Split 1 — 01/02 21:00 (Classic Era)");
        choices[0].StringValue.Should().Be("10:5");
    }

    [Fact]
    public async Task GetChoicesAsync_FiltersByInputCaseInsensitively()
    {
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(It.IsAny<GetUpcomingPublishedRaidEventChoicesQuery>(), default))
            .ReturnsAsync(Result<List<RaidEventChoiceResponse>>.Ok([MakeChoice(1, 10, "Split 1"), MakeChoice(2, 10, "MC Farm")]));

        var choices = (await _sut.GetChoicesAsync(MakeOption("split"), MakeContext(MakeGuild())))!.ToList();

        choices.Should().ContainSingle();
        choices[0].Name.Should().StartWith("Split 1");
    }

    [Fact]
    public async Task GetChoicesAsync_NoInput_ReturnsEveryUpcomingChoice()
    {
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(It.IsAny<GetUpcomingPublishedRaidEventChoicesQuery>(), default))
            .ReturnsAsync(Result<List<RaidEventChoiceResponse>>.Ok([MakeChoice(1, 10, "Split 1"), MakeChoice(2, 10, "MC Farm")]));

        var choices = (await _sut.GetChoicesAsync(MakeOption(null), MakeContext(MakeGuild())))!.ToList();

        choices.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetChoicesAsync_MoreThan25Matches_TakesTheFirst25()
    {
        var many = Enumerable.Range(1, 30).Select(i => MakeChoice(i, 10, $"Split {i}")).ToList();
        _queryDispatcher
            .Setup(q => q.DispatchAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>(It.IsAny<GetUpcomingPublishedRaidEventChoicesQuery>(), default))
            .ReturnsAsync(Result<List<RaidEventChoiceResponse>>.Ok(many));

        var choices = (await _sut.GetChoicesAsync(MakeOption(null), MakeContext(MakeGuild())))!.ToList();

        choices.Should().HaveCount(25);
    }
}
