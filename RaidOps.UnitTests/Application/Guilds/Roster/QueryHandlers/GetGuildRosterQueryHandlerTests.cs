using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Roster.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Roster.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Roster.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetGuildRosterQueryHandler"/>.
/// </summary>
public class GetGuildRosterQueryHandlerTests
{
    private readonly Mock<IGuildsRepository>          _guilds      = new();
    private readonly Mock<IGuildAccessService>        _access      = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly GetGuildRosterQueryHandler       _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly GetGuildRosterQuery Query = new()
    {
        GuildId            = GuildId,
        RequesterDiscordId = RequesterId,
    };

    public GetGuildRosterQueryHandlerTests()
    {
        _sut = new GetGuildRosterQueryHandler(_guilds.Object, _access.Object, _memberships.Object);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotFound()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = false });

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_RequesterBelowRosterAccess_ReturnsRosterAccessDenied()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(Query, default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RosterAccessDenied);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsMembersOrderedByRankThenName()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _memberships.Setup(m => m.GetByGuildIdAsync(GuildId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Zed", rank: CharacterRank.Main),
            BuildMembership(id: 2, name: "Aaron", rank: CharacterRank.Alt),
            BuildMembership(id: 3, name: "Bob", rank: CharacterRank.Main),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(m => m.CharacterName).Should().Equal("Bob", "Zed", "Aaron");
    }

    [Fact]
    public async Task HandleAsync_Success_MapsCharacterAndMainSpecFields()
    {
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _memberships.Setup(m => m.GetByGuildIdAsync(GuildId, default)).ReturnsAsync(
        [
            BuildMembership(id: 1, name: "Arthas", rank: CharacterRank.Split),
        ]);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        var member = result.Value!.Single();
        member.CharacterId.Should().Be(1);
        member.ClassName.Should().Be("Death Knight");
        member.ClassColor.Should().Be("#C41F3B");
        member.RealmName.Should().Be("Kazzak");
        member.Level.Should().Be(80);
        member.ItemLevel.Should().Be(620);
        member.MainSpecName.Should().Be("Frost");
        member.CharacterRank.Should().Be(CharacterRank.Split);
    }

    private static GuildMembership BuildMembership(int id, string name, CharacterRank rank)
    {
        var frostSpec = new Spec { Id = 1, Name = "Frost" };
        return new GuildMembership
        {
            CharacterId   = id,
            GuildId       = GuildId,
            CharacterRank = rank,
            JoinedAt      = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Character = new Character
            {
                Id       = id,
                Name     = name,
                ClassId  = 6,
                Class    = new WowClass { Id = 6, Name = "Death Knight", Color = "C41F3B" },
                RealmId  = 1,
                Realm    = new Realm { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu" },
                RaidSpecs = [new CharacterRaidSpec { CharacterId = id, SpecId = 1, IsMain = true, Spec = frostSpec }],
                ExpansionStates =
                [
                    new CharacterExpansionState
                    {
                        CharacterId = id, IsActive = true, Level = 80, ItemLevel = 620,
                    },
                ],
            },
        };
    }
}
